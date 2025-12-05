using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class Leader : MonoBehaviour, IDropHandler
{
    public TextMeshProUGUI hpText;
    public int maxHp = 30;
    public int currentHp;
    public bool isPlayer = false; // Inspectorで設定
    [Header("新UI")]
    public Image hpFillImage; // ★追加：緑の液体のImage（Filledタイプ）
    public bool isPlayerLeader = true; // 自分か敵か設定しておく
    void Start()
    {
        currentHp = maxHp;
        UpdateHP();
        UpdateHPBar();
    }

    void UpdateHP()
    {
        if (hpText != null)
        {
            hpText.text = "HP: " + currentHp;
        }
    }

    // ダメージ処理（回復も対応）
    public void TakeDamage(int damage)
    {
        // ダメージ音（回復以外）
        if (damage > 0) GameManager.instance.PlaySE(GameManager.instance.seDamage);
        GameManager.instance.SpawnDamageText(transform.position, damage);
        currentHp -= damage;
        
        // 最大値を超えないように（回復用）
        if (currentHp > maxHp) currentHp = maxHp;
        if (currentHp < 0) currentHp = 0;

        UpdateHP();
        UpdateHPBar();

        // 死亡判定
        if (currentHp <= 0)
        {
            if (isPlayer)
            {
                GameManager.instance.GameEnd(false); // プレイヤーの負け
            }
            else
            {
                GameManager.instance.GameEnd(true); // プレイヤーの勝ち
            }
        }
    }

    // ドロップ判定（攻撃 or スペル）
    public void OnDrop(PointerEventData eventData)
    {
        // パターンA：ユニットからの攻撃
        UnitMover attacker = eventData.pointerDrag.GetComponent<UnitMover>();
        if (attacker != null && attacker.canAttack)
        {
            // 鉄壁などのルール確認
            if (GameManager.instance.CanAttackLeader(attacker)) 
            {
                attacker.Attack(this);
            }
            return;
        }

        // パターンB：スペルカードの使用
        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        if (card != null && card.cardData.type == CardType.SPELL)
        {
            // マナチェック
            if (GameManager.instance.TryUseMana(card.cardData.cost))
            {
                // 自分自身(this)をターゲットとして渡して発動！
                GameManager.instance.ProcessAbilities(card.cardData, EffectTrigger.SPELL_USE, null, this);
                
                Destroy(card.gameObject);
            }
        }
    }

    void UpdateHPBar()
    {
        if (hpFillImage != null)
        {
            // 割合計算
            // currentHpが負の数になってもエラーにならないようMathf.Maxで0止め推奨ですが、そのままでもOK
            float ratio = (float)Mathf.Max(0, currentHp) / maxHp;
            hpFillImage.fillAmount = ratio;

            // ★追加：HPの色変更ロジック
            if (currentHp <= 5)
            {
                // 危険域：赤色
                hpFillImage.color = Color.red;
            }
            else if (currentHp <= 10)
            {
                // 注意域：黄色
                hpFillImage.color = Color.yellow;
            }
            else
            {
                // 通常：元の色（白を指定すると画像本来の色になる）
                hpFillImage.color = Color.white;
            }
        }
        
        if (hpText != null) hpText.text = currentHp.ToString();
    }
}