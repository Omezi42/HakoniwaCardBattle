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

    // ★修正：クリック処理
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return; 

        // 左クリック
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount == 2)
            {
                // ダブルクリック：デッキに追加
                if (DeckEditManager.instance != null) 
                    DeckEditManager.instance.AddCardToDeck(cardData);
            }
            else
            {
                // シングルクリック：詳細表示
                if (SimpleCardModal.instance != null) 
                    SimpleCardModal.instance.Open(cardData);
            }
        }
        // 右クリック：デッキに追加
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (DeckEditManager.instance != null)
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
        canvasGroup.blocksRaycasts = false; 
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

        if (transform.parent == transform.root)
        {
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.localPosition = Vector3.zero;
        }
        
        isDraggingCard = false;
    }
}