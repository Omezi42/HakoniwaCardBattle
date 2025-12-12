using UnityEngine;
using System.Collections.Generic;

public enum CardType { UNIT, SPELL, BUILD }
public enum JobType { NEUTRAL, KNIGHT, MAGE, PRIEST, ROGUE }
public enum Rarity { COMMON, RARE, EPIC, LEGEND }

// --- 新しい定義 ---

// 1. トリガー（発動条件）
public enum EffectTrigger
{
    ON_SUMMON,      // 召喚時（ファンファーレ）
    ON_TURN_END,    // ターン終了時
    ON_ATTACK,      // 攻撃時
    SPELL_USE,      // スペルとして使用した時
    PASSIVE         // 常在効果（守護など）
}

// 2. ターゲット（対象）
public enum EffectTarget
{
    NONE,           // 対象なし（自分自身など）
    SELF,           // 自分
    FRONT_ENEMY,    // 正面の敵
    ALL_ENEMIES,    // 敵ユニット全体
    RANDOM_ENEMY,   // ランダムな敵ユニット
    ENEMY_LEADER,   // 敵リーダー
    ALL_ALLIES,     // 味方ユニット全体
    FRONT_ALLY,     // 正面の味方
    PLAYER_LEADER,   // 自分リーダー
    SELECT_ENEMY_UNIT,   // 敵ユニットを選んで発動
    SELECT_ENEMY_LEADER, // 敵リーダーを選んで発動
    SELECT_ANY_ENEMY,     // 敵なら誰でもOK
    SELECT_UNDAMAGED_ENEMY, // 未ダメージの敵を選んで発動
}

// 3. エフェクト（効果内容）
public enum EffectType
{
    DAMAGE,
    HEAL,
    BUFF_ATTACK,
    BUFF_HEALTH,
    GAIN_MANA,
    DESTROY,
    TAUNT,
    STEALTH,
    QUICK,          // 疾風 (移動と攻撃が両方できる)
    DRAW_CARD,
    HASTE,          // ★追加：速攻 (召喚酔いなし)
    FORCE_MOVE,     // ★追加：強制移動 (煙玉など)
    RETURN_TO_HAND, // ★追加：バウンス (念のため)
}

// これらをまとめた「能力データ」クラス
[System.Serializable] // これを書くとInspectorで編集できる！
public class CardAbility
{
    public EffectTrigger trigger; // いつ？
    public EffectTarget target;   // 誰に？
    public EffectType effect;     // 何を？
    public int value;             // どれくらい？（ダメージ量など）
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Hakoniwa/CardData")]
public class CardData : ScriptableObject
{
    [Header("基本情報")]
    public string id;
    public string cardName;
    public CardType type;
    public JobType job;
    public Rarity rarity;

    [Header("パラメータ")]
    public int cost;
    public int attack;
    public int health;
    public int maxInDeck;

    // ★追加：ビルド用のパラメータ
    [Tooltip("ビルドの持続ターン数")]
    public int duration; 

    [Header("詳細")]
    [TextArea(2, 4)]
    public string description;
    public string scriptKey;

    [Header("新・スキルシステム")]
    public List<CardAbility> abilities = new List<CardAbility>();

    public Sprite cardIcon;
}