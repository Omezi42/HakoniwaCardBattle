using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections.Generic;
using System.IO;

public class CardDescriptionUpdater : EditorWindow
{
    [MenuItem("Hakoniwa/Update All Card Descriptions")]
    public static void UpdateAllDescriptions()
    {
        if (!EditorUtility.DisplayDialog("一括更新の確認", "「Assets/Resources/CardsData」内の全てのカードデータの説明文を更新します。", "実行する", "キャンセル")) return;
        
        string folderPath = "Assets/Resources/CardsData";
        if (!Directory.Exists(folderPath)) 
        { 
            Debug.LogError("フォルダなし"); 
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
                string newDesc = GenerateDescription(card.abilities, card.type);
                if (card.description != newDesc) 
                { 
                    Undo.RecordObject(card, "Update Desc"); 
                    card.description = newDesc; 
                    EditorUtility.SetDirty(card); 
                    count++; 
                }
            }
        }
        AssetDatabase.SaveAssets(); 
        AssetDatabase.Refresh(); 
        EditorUtility.DisplayDialog("完了", $"{count} 枚更新", "OK");
    }

    static string GenerateDescription(List<CardAbility> abilities, CardType type)
    {
        if (abilities == null || abilities.Count == 0) return "";
        
        StringBuilder descriptionBuilder = new StringBuilder();
        
        for (int i = 0; i < abilities.Count; i++)
        {
            var currentAbility = abilities[i];
            var prevAbility = (i > 0) ? abilities[i - 1] : null;
            var nextAbility = (i + 1 < abilities.Count) ? abilities[i + 1] : null;

            bool isTriggerStart = (prevAbility == null) || (currentAbility.trigger != prevAbility.trigger);

            if (currentAbility.trigger == EffectTrigger.PASSIVE)
            {
                ProcessPassiveAbility(descriptionBuilder, currentAbility, nextAbility, isTriggerStart);
            }
            else
            {
                ProcessActiveAbility(descriptionBuilder, currentAbility, prevAbility, nextAbility, isTriggerStart, type);
            }
        }
        return descriptionBuilder.ToString();
    }

    static void ProcessPassiveAbility(StringBuilder sb, CardAbility current, CardAbility next, bool isTriggerStart)
    {
        if (isTriggerStart && sb.Length > 0) sb.Append("\n");

        if (current.effect == EffectType.SPELL_DAMAGE_PLUS)
        {
            sb.Append($"魔導+{current.value}");
        }
        else
        {
            sb.Append(GetPassiveName(current.effect));
        }

        if (next != null && next.trigger == EffectTrigger.PASSIVE)
        {
            sb.Append("、");
        }
    }

    static void ProcessActiveAbility(StringBuilder sb, CardAbility current, CardAbility prev, CardAbility next, bool isTriggerStart, CardType cardType)
    {
        if (isTriggerStart)
        {
            if (sb.Length > 0) sb.Append("\n");

            if (current.trigger != EffectTrigger.SPELL_USE || cardType != CardType.SPELL)
            {
                sb.Append($"【{GetTriggerName(current.trigger)}】");
            }
        }

        bool showTarget = false;
        if (current.target != EffectTarget.NONE)
        {
            if (isTriggerStart) showTarget = true;
            else if (prev != null && current.target != prev.target) showTarget = true;
        }

        if (showTarget)
        {
            string noun = GetTargetNoun(current.target);
            string particle = GetParticleForEffect(current.effect);
            sb.Append($"{noun}{particle}");
        }

        bool isNextSameTrigger = (next != null) && (next.trigger == current.trigger);
        
        if (isNextSameTrigger)
        {
            sb.Append(GetContinuativeEffectText(current.effect, current.value));
            sb.Append("、");
        }
        else
        {
            sb.Append(GetTerminalEffectText(current.effect, current.value));
            sb.Append("。");
        }
    }

    static string GetPassiveName(EffectType effect) 
    { 
        switch (effect) 
        { 
            case EffectType.TAUNT: return "守護"; 
            case EffectType.STEALTH: return "潜伏";
            case EffectType.QUICK: return "疾風"; 
            case EffectType.HASTE: return "速攻"; 
            case EffectType.PIERCE: return "貫通"; 
            case EffectType.SPELL_DAMAGE_PLUS: return "魔導"; 
            default: return ""; 
        } 
    }

    static string GetTriggerName(EffectTrigger trigger) 
    { 
        switch (trigger) 
        { 
            case EffectTrigger.ON_SUMMON: return "召喚時"; 
            case EffectTrigger.ON_TURN_END: return "ターン終了時"; 
            case EffectTrigger.ON_ATTACK: return "攻撃時"; 
            case EffectTrigger.ON_DEATH: return "死亡時"; 
            case EffectTrigger.ON_MOVE: return "移動時"; 
            case EffectTrigger.SPELL_USE: return "スペル使用時"; 
            default: return trigger.ToString(); 
        } 
    }

    static string GetTargetNoun(EffectTarget target) 
    { 
        switch (target) 
        { 
            case EffectTarget.FRONT_ENEMY: return "正面の敵モンスター"; 
            case EffectTarget.ALL_ENEMIES: return "すべての敵モンスター"; 
            case EffectTarget.RANDOM_ENEMY: return "ランダムな敵モンスター1体"; 
            case EffectTarget.ENEMY_LEADER: return "敵リーダー"; 
            case EffectTarget.ALL_ALLIES: return "味方全体"; 
            case EffectTarget.FRONT_ALLY: return "正面の味方"; 
            case EffectTarget.PLAYER_LEADER: return "味方リーダー"; 
            case EffectTarget.SELECT_ENEMY_UNIT: return "選択した敵モンスター"; 
            case EffectTarget.SELECT_ENEMY_LEADER: return "敵リーダー"; 
            case EffectTarget.SELECT_ANY_ENEMY: return "選択した敵"; 
            case EffectTarget.SELECT_UNDAMAGED_ENEMY: return "無傷の敵モンスター"; 
            case EffectTarget.SELECT_DAMAGED_ENEMY: return "傷を負った敵モンスター";
            case EffectTarget.SELF: return "自身"; 
            case EffectTarget.SELECT_ALLY_UNIT: return "選択した味方"; 
            case EffectTarget.SELECT_ANY_UNIT: return "選択したユニット"; 
            case EffectTarget.RANDOM_ALLY: return "ランダムな味方1体"; 
            case EffectTarget.ALL_UNITS: return "全ユニット"; 
            default: return ""; 
        } 
    }

    static string GetParticleForEffect(EffectType effect) 
    { 
        switch (effect) 
        { 
            case EffectType.HEAL: 
            case EffectType.DESTROY: 
            case EffectType.RETURN_TO_HAND: 
            case EffectType.FORCE_MOVE: 
                return "を"; 
            case EffectType.GAIN_MANA: 
            case EffectType.BUFF_ATTACK: 
            case EffectType.BUFF_HEALTH: 
                return "の"; 
            case EffectType.DRAW_CARD: 
                return "は"; 
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
            case EffectType.STEALTH: return "潜伏を付与する"; 
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
            case EffectType.STEALTH: return "潜伏を付与し"; 
            case EffectType.QUICK: return "疾風を付与し"; 
            case EffectType.HASTE: return "速攻を付与し"; 
            case EffectType.PIERCE: return "貫通を付与し"; 
            default: return ""; 
        } 
    }
}