using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // ★Image操作用に必要

public class DropPlace : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject unitPrefab;
    public bool isEnemySlot = false;

    // ★追加：色変更用の変数
    private Image myImage;
    private Color defaultColor;
    public Color highlightColor = new Color(1f, 1f, 0.5f, 1f); // 薄い黄色

    void Start()
    {
        myImage = GetComponent<Image>();
        if (myImage != null)
        {
            myImage.alphaHitTestMinimumThreshold = 0.1f;
            defaultColor = myImage.color; // 元の色を覚えておく
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        // 1. 手札のカードが来た場合
        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        if (card != null && !isEnemySlot && transform.childCount == 0)
        {
            if (card.cardData.type == CardType.UNIT && GameManager.instance.currentMana >= card.cardData.cost)
            {
                card.transform.localScale = Vector3.one * 1.1f;
                // ★追加：グリッドを光らせる
                if (myImage != null) myImage.color = highlightColor;
            }
        }

        // 2. 盤面のユニットが来た場合（移動）
        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();
        if (unit != null && !isEnemySlot && transform.childCount == 0)
        {
            if (unit.canMove && IsNeighbor(unit))
            {
                unit.transform.localScale = Vector3.one * 1.1f;
                // ★追加：グリッドを光らせる
                if (myImage != null) myImage.color = highlightColor;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // ★追加：色を元に戻す
        if (myImage != null) myImage.color = defaultColor;

        if (eventData.pointerDrag == null) return;
        eventData.pointerDrag.transform.localScale = Vector3.one;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // ★追加：ドロップ時も色を戻す
        if (myImage != null) myImage.color = defaultColor;

        eventData.pointerDrag.transform.localScale = Vector3.one;

        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();

        if (card != null)
        {
            HandleSummon(card);
        }
        else if (unit != null)
        {
            HandleMove(unit);
        }
    }

    // ... (IsNeighbor, HandleSummon, HandleMove は既存のまま) ...
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
                unitMover.PlaySummonAnimation();

                if (BattleLogManager.instance != null)
                    BattleLogManager.instance.AddLog($"{card.cardData.cardName} を召喚した", true);
            }
            else if (card.cardData.type == CardType.SPELL)
            {
                AbilityManager.instance.ProcessAbilities(card.cardData, EffectTrigger.SPELL_USE, null);
            }
            
            GameManager.instance.PlaySE(GameManager.instance.seSummon);
            Destroy(card.gameObject);
        }
    }

    void HandleMove(UnitMover unit)
    {
        if (isEnemySlot) return;
        if (transform.childCount > 0) return;
        if (!unit.canMove) return;

        SlotInfo mySlot = GetComponent<SlotInfo>();
        SlotInfo unitSlot = unit.originalParent.GetComponent<SlotInfo>();

        if (mySlot != null && unitSlot != null)
        {
            int distance = Mathf.Abs(mySlot.x - unitSlot.x) + Mathf.Abs(mySlot.y - unitSlot.y);

            if (distance == 1)
            {
                unit.MoveToSlot(this.transform);
                Debug.Log($"移動しました: ({unitSlot.x},{unitSlot.y}) -> ({mySlot.x},{mySlot.y})");
            }
        }
    }
}