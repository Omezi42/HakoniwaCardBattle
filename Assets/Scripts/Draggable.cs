using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private CardView cardView;
    private UnityEngine.UI.GraphicRaycaster graphicRaycaster;

    private const float SPELL_CAST_THRESHOLD = 0.5f; 

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        cardView = GetComponent<CardView>();
        graphicRaycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail();
        
        originalParent = transform.parent;
        transform.SetParent(transform.root);
        
        canvasGroup.blocksRaycasts = false;
        
        // ★追加：ドラッグ中は半透明にする
        canvasGroup.alpha = 0.6f;

        if (graphicRaycaster != null) graphicRaycaster.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // ... (位置調整やスペル拡大処理はそのまま) ...
        RectTransform rect = GetComponent<RectTransform>();
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

        if (cardView != null)
        {
            GameManager.instance.PreviewMana(cardView.cardData.cost);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        if (graphicRaycaster != null) graphicRaycaster.enabled = true;
        
        // ★追加：透明度を元に戻す
        canvasGroup.alpha = 1.0f;
        
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
        }

        GameManager.instance.ResetManaPreview();
    }
}