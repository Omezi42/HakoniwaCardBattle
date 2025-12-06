using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private CardView cardView;

    // ★変更：画面の高さの5割（0.5）より上なら発動
    private const float SPELL_CAST_THRESHOLD = 0.5f; 

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        cardView = GetComponent<CardView>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.OnClickCloseDetail();
        }
        originalParent = transform.parent;
        transform.SetParent(transform.root);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;

        // スペルカードの場合、発動エリアに入ったら拡大する
        if (cardView != null && cardView.cardData.type == CardType.SPELL)
        {
            if (Input.mousePosition.y > Screen.height * SPELL_CAST_THRESHOLD)
            {
                // ★変更：発動エリアでの拡大率を1.2倍に
                transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                // 手札エリア：通常サイズ
                transform.localScale = Vector3.one;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        transform.localScale = Vector3.one; // サイズを戻す

        // スペルカードの場合の特殊処理
        if (cardView != null && cardView.cardData.type == CardType.SPELL)
        {
            if (Input.mousePosition.y > Screen.height * SPELL_CAST_THRESHOLD)
            {
                GameManager.instance.StartSpellCast(cardView);
                return; 
            }
        }

        // ユニットの場合や、スペルを発動しなかった場合の戻り処理
        if (transform.parent == transform.root || transform.parent == originalParent.root)
        {
            transform.SetParent(originalParent);
            transform.localPosition = Vector3.zero;
        }
    }
}