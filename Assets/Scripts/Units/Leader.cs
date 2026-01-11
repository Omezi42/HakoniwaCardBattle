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

    [Header("攻撃・効果対象エリア")]
    public Transform atkArea; // ★追加：ここにATKAreaを登録
    void Start()
    {
        // ★Request: Initial HP 30
        currentHp = 30;
        maxHp = 30;

        UpdateHP();
        UpdateHPBar();
    }

    void UpdateHP()
    {
        if (hpText != null)
        {
            hpText.text = "体力: " + currentHp;
        }
    }

    // ダメージ処理（回復も対応）
    public void TakeDamage(int damage, bool showText = true)
    {
        // ダメージ音（回復以外）
        if (damage > 0) GameManager.instance.PlaySE(GameManager.instance.seDamage);
        
        // ★FIX: Allow suppressing text (for Unit attacks)
        if (showText) 
        {
             Vector3 spawnPos = (atkArea != null) ? atkArea.position : transform.position;
             GameManager.instance.SpawnDamageText(spawnPos, damage);
        }
        
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
    }

    public void UpdateHPBar()
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
    public void SetIcon(Sprite icon)
    {
        // Try to find Image component on "FaceImage" child
        var faceTransform = transform.Find("FaceImage");
        if (faceTransform != null)
        {
            var img = faceTransform.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = icon;
                return;
            }
        }

        // Fallback: Check self
        var selfImg = GetComponent<Image>();
        if (selfImg != null)
        {
            selfImg.sprite = icon;
        }
    }
}