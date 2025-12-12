using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeckDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public CardData cardData;     
    public Transform originalParent; 
    private CanvasGroup canvasGroup;
    private ScrollRect scrollRect; 
    private bool isDraggingCard = false; 

    private int originalSiblingIndex;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if(canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(CardData data)
    {
        this.cardData = data;
        scrollRect = GetComponentInParent<ScrollRect>();
    }

    // クリック処理（そのまま）
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return; 
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount == 2) DeckEditManager.instance.AddCardToDeck(cardData);
            else if (SimpleCardModal.instance != null) SimpleCardModal.instance.Open(cardData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            DeckEditManager.instance.AddCardToDeck(cardData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (scrollRect != null)
        {
            if (Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y))
            {
                isDraggingCard = false;
                ExecuteEvents.Execute(scrollRect.gameObject, eventData, ExecuteEvents.beginDragHandler);
                return;
            }
        }

        isDraggingCard = true;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        transform.SetParent(transform.root); 
        canvasGroup.blocksRaycasts = false; // これによりDropZoneの判定が有効になる
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingCard)
        {
            if (scrollRect != null) ExecuteEvents.Execute(scrollRect.gameObject, eventData, ExecuteEvents.dragHandler);
            return;
        }
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingCard)
        {
            if (scrollRect != null) ExecuteEvents.Execute(scrollRect.gameObject, eventData, ExecuteEvents.endDragHandler);
            return;
        }

        canvasGroup.blocksRaycasts = true;

        // ★削除：以前あった「高さ判定 (Input.mousePosition.y > ...)」は削除しました。
        // これにより、DeckDropZoneの上で離さないと追加されなくなります。

        // 元の場所に戻る（追加処理はDropZone側で行われるため、見た目を戻すだけでOK）
        if (transform.parent == transform.root)
        {
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.localPosition = Vector3.zero;
        }
        
        isDraggingCard = false;
    }
}