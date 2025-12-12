using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DropPlace : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject unitPrefab;
    public bool isEnemySlot = false;

    private Image myImage;
    private Color defaultColor;
    public Color highlightColor = new Color(1f, 1f, 0.5f, 1f);

    void Start()
    {
        myImage = GetComponent<Image>();
        if (myImage != null)
        {
            // ★重要：画像の透明部分を当たり判定から除外
            // （これを行うには画像のImport Settingsで "Read/Write Enabled" をONにする必要があります）
            myImage.alphaHitTestMinimumThreshold = 0.1f;
            defaultColor = myImage.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        if (card != null && !isEnemySlot && transform.childCount == 0)
        {
            if (card.cardData.type == CardType.UNIT && GameManager.instance.currentMana >= card.cardData.cost)
            {
                card.transform.localScale = Vector3.one * 1.1f;
                if (myImage != null) myImage.color = highlightColor;
            }
        }

        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();
        if (unit != null && !isEnemySlot && transform.childCount == 0)
        {
            if (unit.canMove && IsNeighbor(unit))
            {
                unit.transform.localScale = Vector3.one * 1.1f;
                if (myImage != null) myImage.color = highlightColor;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (myImage != null) myImage.color = defaultColor;

        if (eventData.pointerDrag == null) return;
        // ドラッグ中のオブジェクトのサイズを戻す
        // （ただしDraggable側で制御しているので、ここでは何もしなくて良い場合もあるが念のため）
        // eventData.pointerDrag.transform.localScale = Vector3.one; 
        // ※OnDragでサイズ制御している場合は競合するので削除推奨、今回はハイライトだけ戻す
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (myImage != null) myImage.color = defaultColor;

        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();
        
        if (card != null)
        {
            // ドロップ時はサイズを戻す
            card.transform.localScale = Vector3.one;
            HandleSummon(card);
        }
        else if (unit != null)
        {
            unit.transform.localScale = Vector3.one;
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
                unitMover.PlaySummonAnimation();

                if (BattleLogManager.instance != null)
                    BattleLogManager.instance.AddLog($"{card.cardData.cardName} を召喚した", true);
            }
            else if (card.cardData.type == CardType.SPELL)
            {
                // ここには来ないはず（Draggableで処理）だが念のため
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