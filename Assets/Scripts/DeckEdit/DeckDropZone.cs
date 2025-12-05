using UnityEngine;
using UnityEngine.EventSystems;

// ゾーンの種類：「所持リスト(Inventory)」か「デッキ(Deck)」か
public enum ZoneType { Inventory, Deck }

public class DeckDropZone : MonoBehaviour, IDropHandler
{
    public ZoneType zoneType; // Inspectorで設定する

    public void OnDrop(PointerEventData eventData)
    {
        // ドラッグされてきたカードを取得
        DeckDraggable card = eventData.pointerDrag.GetComponent<DeckDraggable>();
        
        if (card != null)
        {
            // マネージャーに「カードが移動したよ！」と報告する
            DeckEditManager.instance.OnCardDrop(card.cardData, zoneType);
        }
    }
}