using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private CardView cardView;
    private GraphicRaycaster graphicRaycaster;

    private const float SPELL_CAST_THRESHOLD = 0.5f; 

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        cardView = GetComponent<CardView>();
        // GraphicRaycasterの取得はStartで行う
    }

    void Start()
    {
        // CardViewがAwakeでAddComponentするので、Startで取得する
        graphicRaycaster = GetComponent<GraphicRaycaster>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail();
        
        originalParent = transform.parent;
        transform.SetParent(transform.root);
        
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;

        // ★重要：自身のRaycasterを切らないと下のオブジェクトにイベントが届かない
        if (graphicRaycaster != null) graphicRaycaster.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // ★修正：Pivot(0.5, 0)の下端基準に合わせて、マウスが中心に来るようにずらす
        RectTransform rect = GetComponent<RectTransform>();
        // 高さ * スケール * 0.5 (半分) だけ下にずらす
        float yOffset = rect.rect.height * transform.localScale.y * 0.5f; 
        transform.position = eventData.position - new Vector2(0, yOffset);

        if (cardView != null && cardView.cardData.type == CardType.SPELL)
        {
            if (Input.mousePosition.y > Screen.height * SPELL_CAST_THRESHOLD)
            {
                transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }

        if (cardView != null && GameManager.instance != null)
        {
            GameManager.instance.PreviewMana(cardView.cardData.cost);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1.0f;
        
        // ★重要：Raycasterを戻す
        if (graphicRaycaster != null) graphicRaycaster.enabled = true;
        
        transform.localScale = Vector3.one; 

        if (cardView != null && cardView.cardData.type == CardType.SPELL)
        {
            if (Input.mousePosition.y > Screen.height * SPELL_CAST_THRESHOLD)
            {
                GameManager.instance.StartSpellCast(cardView);
                return; 
            }
        }

        if (transform.parent == transform.root || transform.parent == originalParent.root)
        {
            transform.SetParent(originalParent);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        if (GameManager.instance != null) GameManager.instance.ResetManaPreview();
    }
}