using UnityEngine;
using UnityEngine.EventSystems;

public class DropPlace : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject unitPrefab;
    public bool isEnemySlot = false;

    void Start() // ★追加
    {
        // 画像の透明部分（Alpha < 0.1）を当たり判定から除外する
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return; // 何もドラッグしていなければ無視

        // 1. 手札のカードが来た場合
        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        if (card != null && !isEnemySlot && transform.childCount == 0)
        {
            // ユニットカードで、かつマナが足りているなら拡大
            if (card.cardData.type == CardType.UNIT && GameManager.instance.currentMana >= card.cardData.cost)
            {
                card.transform.localScale = Vector3.one * 1.1f;
            }
        }

        // 2. 盤面のユニットが来た場合（移動）
        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();
        if (unit != null && !isEnemySlot && transform.childCount == 0)
        {
            // 移動可能なら拡大
            if (unit.canMove && IsNeighbor(unit))
            {
                unit.transform.localScale = Vector3.one * 1.1f;
            }
        }
    }

    // --- マウスがスロットから出た時（元に戻す） ---
    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        // ドラッグ中のオブジェクトのスケールを戻す
        eventData.pointerDrag.transform.localScale = Vector3.one;
    }

    // --- 既存のOnDrop ---
    public void OnDrop(PointerEventData eventData)
    {
        // ドロップ時もサイズを戻しておく（念のため）
        eventData.pointerDrag.transform.localScale = Vector3.one;
        // 誰が落ちてきた？
        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();

        // パターンA：手札からの「召喚」
        if (card != null)
        {
            // ★追加：ユニットカードの場合のみ受け入れる
            if (card.cardData.type == CardType.UNIT)
            {
                HandleSummon(card);
            }
        }
        // パターンB：盤面上の「移動」
        else if (unit != null)
        {
            HandleMove(unit);
        }
    }

    bool IsNeighbor(UnitMover unit)
    {
        SlotInfo mySlot = GetComponent<SlotInfo>();
        SlotInfo unitSlot = unit.originalParent.GetComponent<SlotInfo>();
        if (mySlot != null && unitSlot != null)
        {
            int distance = Mathf.Abs(mySlot.x - unitSlot.x) + Mathf.Abs(mySlot.y - unitSlot.y);
            return distance == 1;
        }
        return false;
    }

    void HandleSummon(CardView card)
    {
        if (isEnemySlot) return;
        if (transform.childCount > 0) return;

        bool canPay = GameManager.instance.TryUseMana(card.cardData.cost);
        if (canPay)
        {
            if (card.cardData.type == CardType.UNIT)
            {
                GameObject newUnit = Instantiate(unitPrefab, transform);
                newUnit.GetComponent<UnitView>().SetUnit(card.cardData);
                UnitMover unitMover = newUnit.GetComponent<UnitMover>();
                
                unitMover.Initialize(card.cardData, !isEnemySlot);

                AbilityManager.instance.ProcessAbilities(card.cardData, EffectTrigger.ON_SUMMON, unitMover);

                // ★変更：砂煙をやめて、召喚アニメーションを再生
                // GameManager.instance.PlayDustEffect(newUnit.transform.position); // ←削除
                
                // ★追加
                if (BattleLogManager.instance != null)
                    BattleLogManager.instance.AddLog($"{card.cardData.cardName} を召喚した", true);

                unitMover.PlaySummonAnimation(); // ←追加
            }
            
            GameManager.instance.PlaySE(GameManager.instance.seSummon);
            Destroy(card.gameObject);
        }
    }

    // --- 移動処理 ---
    void HandleMove(UnitMover unit)
    {
        if (isEnemySlot) return;
        if (transform.childCount > 0) return;

        // 移動権チェック
        if (!unit.canMove) return;
        unit.transform.localScale = Vector3.one;

        SlotInfo mySlot = GetComponent<SlotInfo>();
        SlotInfo unitSlot = unit.originalParent.GetComponent<SlotInfo>();

        if (mySlot != null && unitSlot != null)
        {
            int distance = Mathf.Abs(mySlot.x - unitSlot.x) + Mathf.Abs(mySlot.y - unitSlot.y);

            // 隣（距離1）なら移動OK
            if (distance == 1)
            {
                unit.MoveToSlot(this.transform);
                
                Debug.Log($"移動しました: ({unitSlot.x},{unitSlot.y}) -> ({mySlot.x},{mySlot.y})");
            }
        }
    }
}