using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DeckFigure : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image iconImage;

    private CardData myData;
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private Vector3 startPos;
    
    // ★追加：元の並び順を記憶する変数
    private int originalSiblingIndex;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(CardData data, int count)
    {
        myData = data;
        if (data.cardIcon != null) iconImage.sprite = data.cardIcon;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (SimpleCardModal.instance != null) SimpleCardModal.instance.Open(myData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (DeckEditManager.instance != null) DeckEditManager.instance.AddCardToDeck(myData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        startPos = transform.position;
        
        // ★追加：ドラッグ開始時の並び順を記憶
        originalSiblingIndex = transform.GetSiblingIndex();

        transform.SetParent(transform.root); 
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // ★修正：判定ラインを画面下部40%（0.4）に変更
        // これより下に行ったら削除、上ならキャンセル
        if (Input.mousePosition.y < Screen.height * 0.4f)
        {
            if (DeckEditManager.instance != null)
            {
                DeckEditManager.instance.RemoveCardFromDeck(myData);
            }
            Destroy(gameObject); 
        }
        else
        {
            // 削除キャンセル：元の親に戻す
            transform.SetParent(originalParent);
            
            // ★追加：記憶しておいた順番に戻す（これで勝手に並び替わらない！）
            transform.SetSiblingIndex(originalSiblingIndex);
            
            transform.position = startPos;
        }
    }
}