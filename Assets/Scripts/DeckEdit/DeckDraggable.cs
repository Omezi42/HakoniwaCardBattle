using UnityEngine;
using UnityEngine.EventSystems;

public class DeckDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardData cardData;     // このカードのデータ
    public Transform originalParent; // 元いた場所（キャンセル時に戻る用）
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // 外部からデータをセットするための関数
    public void Setup(CardData data)
    {
        this.cardData = data;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        // ドラッグ中はリストから抜けて、画面の最前面に移動
        transform.SetParent(transform.root); 
        canvasGroup.blocksRaycasts = false; // マウスの裏にあるドロップエリアを検知できるようにする
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // ドロップ処理が成功していれば、親が変わっているはず。
        // 親が変わっていなければ（ドロップ失敗）、元の場所に戻す
        if (transform.parent == transform.root)
        {
            transform.SetParent(originalParent);
        }
    }
}