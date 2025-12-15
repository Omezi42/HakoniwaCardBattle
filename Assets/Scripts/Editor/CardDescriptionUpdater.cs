using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections.Generic;
using System.IO;

public class CardDescriptionUpdater : EditorWindow
{
    // ... (メニューやファイル検索部分はそのまま) ...
    [MenuItem("Hakoniwa/Update All Card Descriptions")]
    public static void UpdateAllDescriptions()
    {
        if (!EditorUtility.DisplayDialog("一括更新の確認", 
            "「Assets/Resources/CardsData」内の全てのカードデータの説明文を、\n" +
            "現在の能力リスト（Abilities）に基づいて自動生成・上書きします。\n\n" +
            "※手動で書いた独自の説明文も上書きされます。\nよろしいですか？", 
            "実行する", "キャンセル"))
        {
            return;
        }

        string folderPath = "Assets/Resources/CardsData";
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"フォルダが見つかりません: {folderPath}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { folderPath });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);

            if (card != null)
            {
                string newDesc = GenerateDescription(card.abilities);
                if (card.description != newDesc)
                {
                    Undo.RecordObject(card, "Update Card Description");
                    card.description = newDesc;
                    EditorUtility.SetDirty(card);
                    count++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完了", $"{guids.Length}枚中、{count} 枚のカードの説明文を更新しました。", "OK");
    }

    static string GenerateDescription(List<CardAbility> abilities)
    {
        if (abilities == null || abilities.Count == 0) return "";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < abilities.Count; i++)
        {
            var curr = abilities[i];
            var prev = (i > 0) ? abilities[i - 1] : null;
            var next = (i + 1 < abilities.Count) ? abilities[i + 1] : null;

            bool isTriggerStart = (prev == null) || (curr.trigger != prev.trigger);
            
            if (curr.trigger == EffectTrigger.PASSIVE)
            {
                if (isTriggerStart && sb.Length > 0) sb.Append("\n");
                sb.Append(GetPassiveName(curr.effect));
                if (next != null && next.trigger == EffectTrigger.PASSIVE) sb.Append("、");
                continue;
            }

            if (isTriggerStart)
            {
                if (sb.Length > 0) sb.Append("\n");
                if (curr.trigger != EffectTrigger.SPELL_USE) sb.Append($"【{GetTriggerName(curr.trigger)}】");
            }

            bool showTarget = false;
            if (curr.target != EffectTarget.NONE)
            {
                if (isTriggerStart) showTarget = true;
                else if (prev != null && curr.target != prev.target) showTarget = true;
            }

            if (showTarget)
            {
                string noun = GetTargetNoun(curr.target);
                string particle = GetParticleForEffect(curr.effect);
                sb.Append($"{noun}{particle}");
            }

            bool isNextSameTrigger = (next != null) && (next.trigger == curr.trigger);

            if (isNextSameTrigger)
            {
                sb.Append(GetContinuativeEffectText(curr.effect, curr.value));
                sb.Append("、");
            }
            else
            {
                sb.Append(GetTerminalEffectText(curr.effect, curr.value));
                sb.Append("。");
            }
        }
        return sb.ToString();
    }

    static string GetTriggerName(EffectTrigger trigger)
    {
        switch (trigger)
        {
            case EffectTrigger.ON_SUMMON: return "召喚時";
            case EffectTrigger.ON_TURN_END: return "ターン終了時";
            case EffectTrigger.ON_ATTACK: return "攻撃時";
            case EffectTrigger.ON_DEATH: return "死亡時";
            default: return trigger.ToString();
        }
    }

    static string GetTargetNoun(EffectTarget target)
    {
        switch (target)
        {
            case EffectTarget.FRONT_ENEMY: return "正面の敵";
            case EffectTarget.ALL_ENEMIES: return "敵全体";
            case EffectTarget.RANDOM_ENEMY: return "ランダムな敵1体";
            case EffectTarget.ENEMY_LEADER: return "敵リーダー";
            case EffectTarget.ALL_ALLIES: return "味方全体";
            case EffectTarget.FRONT_ALLY: return "正面の味方";
            case EffectTarget.PLAYER_LEADER: return "味方リーダー";
            case EffectTarget.SELECT_ENEMY_UNIT: return "選択した敵";
            case EffectTarget.SELECT_ENEMY_LEADER: return "敵リーダー";
            case EffectTarget.SELECT_ANY_ENEMY: return "選択した敵";
            case EffectTarget.SELECT_UNDAMAGED_ENEMY: return "無傷の敵";
            case EffectTarget.SELF: return "自身";
            default: return "";
        }
    }

    static string GetParticleForEffect(EffectType effect)
    {
        switch (effect)
        {
            // 「〜を」回復する / 破壊する / 手札に戻す
            case EffectType.HEAL: 
            case EffectType.DESTROY:
            case EffectType.RETURN_TO_HAND:
            case EffectType.FORCE_MOVE:
                return "を";

            // ★修正：「〜の」マナ最大値 / 攻撃力 / 体力
            case EffectType.GAIN_MANA:
            case EffectType.BUFF_ATTACK:
            case EffectType.BUFF_HEALTH:
                return "の";

            // 「〜に」ダメージ / 付与
            case EffectType.DAMAGE:
            case EffectType.TAUNT: 
            case EffectType.PIERCE:
            case EffectType.STEALTH:
            case EffectType.QUICK:
            case EffectType.HASTE:
                return "に";

            default: return "に";
        }
    }

    static string GetTerminalEffectText(EffectType effect, int value)
    {
        switch (effect)
        {
            case EffectType.DAMAGE: return $"{value}ダメージを与える";
            case EffectType.HEAL: return $"{value}回復する";
            case EffectType.BUFF_ATTACK: return $"攻撃力を+{value}する";
            case EffectType.BUFF_HEALTH: return $"体力を+{value}する";
            case EffectType.GAIN_MANA: return $"マナ最大値を+{value}する";
            case EffectType.DESTROY: return "破壊する";
            case EffectType.DRAW_CARD: return $"カードを{value}枚引く";
            case EffectType.FORCE_MOVE: return "手札に戻す"; 
            case EffectType.RETURN_TO_HAND: return "手札に戻す";
            
            case EffectType.TAUNT: return "守護を付与する";
            case EffectType.STEALTH: return "隠密を付与する";
            case EffectType.QUICK: return "疾風を付与する";
            case EffectType.HASTE: return "速攻を付与する";
            case EffectType.PIERCE: return "貫通を付与する";
            
            default: return "";
        }
    }

    static string GetContinuativeEffectText(EffectType effect, int value)
    {
        switch (effect)
        {
            case EffectType.DAMAGE: return $"{value}ダメージを与え";
            case EffectType.HEAL: return $"{value}回復し";
            case EffectType.BUFF_ATTACK: return $"攻撃力を+{value}し";
            case EffectType.BUFF_HEALTH: return $"体力を+{value}し";
            case EffectType.GAIN_MANA: return $"マナ最大値を+{value}し";
            case EffectType.DESTROY: return "破壊し";
            case EffectType.DRAW_CARD: return $"カードを{value}枚引き";
            case EffectType.FORCE_MOVE: return "手札に戻し"; 
            case EffectType.RETURN_TO_HAND: return "手札に戻し";
            
            case EffectType.TAUNT: return "守護を付与し";
            case EffectType.STEALTH: return "隠密を付与し";
            case EffectType.QUICK: return "疾風を付与し";
            case EffectType.HASTE: return "速攻を付与し";
            case EffectType.PIERCE: return "貫通を付与し";
            
            default: return "";
        }
    }

    static string GetPassiveName(EffectType effect)
    {
        switch (effect)
        {
            case EffectType.TAUNT: return "守護";
            case EffectType.STEALTH: return "隠密";
            case EffectType.QUICK: return "疾風";
            case EffectType.HASTE: return "速攻";
            case EffectType.PIERCE: return "貫通";
            default: return "";
        }
    }
}