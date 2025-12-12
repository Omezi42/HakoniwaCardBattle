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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return; 

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount == 2)
            {
                if (DeckEditManager.instance != null)
                    DeckEditManager.instance.AddCardToDeck(cardData);
            }
            else
            {
                if (SimpleCardModal.instance != null)
                    SimpleCardModal.instance.Open(cardData);
            }
        }
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
            if (scrollRect != null)
                ExecuteEvents.Execute(scrollRect.gameObject, eventData, ExecuteEvents.dragHandler);
            return;
        }
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingCard)
        {
            if (scrollRect != null)
                ExecuteEvents.Execute(scrollRect.gameObject, eventData, ExecuteEvents.endDragHandler);
            return;
        }

        canvasGroup.blocksRaycasts = true;

        // ★修正：判定ラインを画面下部40%（0.4）に変更
        // これより上に行ったら「デッキに追加」とみなす
        if (Input.mousePosition.y > Screen.height * 0.4f)
        {
            if (DeckEditManager.instance != null)
            {
                DeckEditManager.instance.AddCardToDeck(cardData);
            }
        }

        if (transform.parent == transform.root)
        {
            transform.SetParent(originalParent);
            transform.SetSiblingIndex(originalSiblingIndex);
            transform.localPosition = Vector3.zero;
        }
        
        isDraggingCard = false;
    }
}