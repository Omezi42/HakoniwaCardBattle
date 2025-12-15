using UnityEngine;
using System.Collections.Generic;

public enum CardType { UNIT, SPELL, BUILD }
public enum JobType { NEUTRAL, KNIGHT, MAGE, PRIEST, ROGUE }
public enum Rarity { COMMON, RARE, EPIC, LEGEND }

public enum EffectTrigger
{
    ON_SUMMON, ON_TURN_END, ON_ATTACK, SPELL_USE, PASSIVE, ON_DEATH, ON_MOVE
}

public enum EffectTarget
{
    NONE, SELF, FRONT_ENEMY, ALL_ENEMIES, RANDOM_ENEMY, ENEMY_LEADER, ALL_ALLIES, FRONT_ALLY, PLAYER_LEADER,
    SELECT_ENEMY_UNIT, SELECT_ENEMY_LEADER, SELECT_ANY_ENEMY, SELECT_UNDAMAGED_ENEMY,
    SELECT_ALLY_UNIT, SELECT_ANY_UNIT, RANDOM_ALLY, ALL_UNITS
}

public enum EffectType
{
    DAMAGE, HEAL, BUFF_ATTACK, BUFF_HEALTH, GAIN_MANA, DESTROY, 
    TAUNT, STEALTH, QUICK, DRAW_CARD, HASTE, FORCE_MOVE, RETURN_TO_HAND, PIERCE,
    SPELL_DAMAGE_PLUS // ★追加：魔法ダメージ増加
}

[System.Serializable] 
public class CardAbility
{
    public EffectTrigger trigger;
    public EffectTarget target;
    public EffectType effect;
    public int value;
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
    public int duration; 

    [Header("詳細")]
    [TextArea(2, 4)]
    public string description;
    public string scriptKey;

    [Header("新・スキルシステム")]
    public List<CardAbility> abilities = new List<CardAbility>();

    public Sprite cardIcon;
}