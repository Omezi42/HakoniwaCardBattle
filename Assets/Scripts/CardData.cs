using UnityEngine;
using System.Collections.Generic;

// �J�[�h�̎��
public enum CardType { UNIT, SPELL }
// �E��
public enum JobType { NEUTRAL, KNIGHT, MAGE, PRIEST, ROGUE }
// ���A���e�B
public enum Rarity { COMMON, RARE, LEGEND }

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
}

// 3. エフェクト（効果内容）
public enum EffectType
{
    DAMAGE,         // ダメージを与える
    HEAL,           // 回復する
    BUFF_ATTACK,    // 攻撃力を増やす
    BUFF_HEALTH,    // HPを増やす
    GAIN_MANA,      // マナを増やす
    DESTROY,        // 破壊する
    TAUNT,          // 守護を持つ
    STEALTH,        // 潜伏を持つ
    QUICK,           // 疾風を持つ
    DRAW_CARD,      // カードを引く
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
    [Header("��{���")]
    public string id;           // ID
    public string cardName;     // ���O
    public CardType type;       // ���
    public JobType job;         // �E��
    public Rarity rarity;       // ���A���e�B

    [Header("�p�����[�^")]
    public int cost;            // �R�X�g
    public int attack;          // �U����
    public int health;          // �̗�
    public int maxInDeck;       // �f�b�L��������

    [Header("�ڍ�")]
    [TextArea(2, 4)]
    public string description;  // ������
    public string scriptKey;    // �X�L���p�L�[

    [Header("新・スキルシステム")]
    public List<CardAbility> abilities = new List<CardAbility>(); // 能力のリスト

    // �摜�͌��Unity��Őݒ肵�܂�
    public Sprite cardIcon;
}