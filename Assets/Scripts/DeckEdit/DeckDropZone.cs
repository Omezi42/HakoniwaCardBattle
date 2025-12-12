using UnityEngine;
using UnityEngine.EventSystems;

// ゾーンの種類を明確に分けます
public enum ZoneType 
{ 
    Inventory,  // 所持カード一覧（削除用）
    MainDeck,   // メインの棚（ユニット・スペル用）
    BuildDeck   // ビルドの棚（ビルド用）
}

public class DeckDropZone : MonoBehaviour, IDropHandler
{
    public ZoneType zoneType; // Inspectorで設定する

    public void OnDrop(PointerEventData eventData)
    {
        // ドラッグされてきたオブジェクトから DeckDraggable を取得
        if (eventData.pointerDrag == null) return;
        DeckDraggable card = eventData.pointerDrag.GetComponent<DeckDraggable>();
        
        if (card != null)
        {
            // マネージャーに渡して、適合判定と追加処理を行ってもらう
            DeckEditManager.instance.OnCardDrop(card.cardData, zoneType);
        }
    }
}