using UnityEngine;
using UnityEngine.EventSystems;

public class DropPlace : MonoBehaviour, IDropHandler
{
    public GameObject unitPrefab;
    public bool isEnemySlot = false;

    public void OnDrop(PointerEventData eventData)
    {
        // 誰が落ちてきた？
        CardView card = eventData.pointerDrag.GetComponent<CardView>();
        UnitMover unit = eventData.pointerDrag.GetComponent<UnitMover>();

        // パターンA：手札からの「召喚」
        if (card != null)
        {
            HandleSummon(card);
        }
        // パターンB：盤面上の「移動」
        else if (unit != null)
        {
            HandleMove(unit);
        }
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

                GameManager.instance.ProcessAbilities(card.cardData, EffectTrigger.ON_SUMMON, unitMover);

                // ★変更：砂煙をやめて、召喚アニメーションを再生
                // GameManager.instance.PlayDustEffect(newUnit.transform.position); // ←削除
                unitMover.PlaySummonAnimation(); // ←追加
            }
            else if (card.cardData.type == CardType.SPELL)
            {
                GameManager.instance.ProcessAbilities(card.cardData, EffectTrigger.SPELL_USE, null);
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