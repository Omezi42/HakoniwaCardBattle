using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private CardView cardView;
    private GraphicRaycaster graphicRaycaster;

    // ★修正：スペル発動の閾値を変更
    // 0.3f means: if y > Screen.height * 0.3f (bottom 30%), it triggers.
    // つまり、画面の下30%より上に持っていけば発動する（＝発動エリアが広い）
    private const float SPELL_CAST_THRESHOLD = 0.3f; 

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        cardView = GetComponent<CardView>();
    }

    void Start()
    {
        graphicRaycaster = GetComponent<GraphicRaycaster>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail();
        
        // ★Fix: Validation on Drag Start
        if (GameManager.instance != null && !GameManager.instance.isPlayerTurn) return;
        
        // ★Fix: Check ownership via parent HandArea logic
        if (GameManager.instance != null && GameManager.instance.handArea != null)
        {
             if (!transform.IsChildOf(GameManager.instance.handArea) && transform.parent != GameManager.instance.handArea) return;
        }
        
        originalParent = transform.parent;
        transform.SetParent(transform.root);
        
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;

        if (graphicRaycaster != null) graphicRaycaster.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform rect = GetComponent<RectTransform>();
        float yOffset = rect.rect.height * transform.localScale.y * 0.5f; 
        transform.position = eventData.position - new Vector2(0, yOffset);

        if (IsTargetingCard())
        {
            // 閾値を超えたら拡大
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
        
        if (graphicRaycaster != null) graphicRaycaster.enabled = true;
        
        transform.localScale = Vector3.one; 

        if (IsTargetingCard())
        {
            // 親が変わっていない＝どこにもドロップされなかった場合
            // かつ、閾値より高い位置で離した場合に発動
            if ((transform.parent == transform.root || transform.parent == originalParent.root) &&
                Input.mousePosition.y > Screen.height * SPELL_CAST_THRESHOLD)
            {
                if (GameManager.instance.StartSpellCast(cardView))
                {
                    return; 
                }
                // If failed (No Mana), fall through to reset logic
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

    bool IsTargetingCard()
    {
        if (cardView == null || cardView.cardData == null) return false;
        
        // スペルは常にターゲットモード判定あり（ドロップ高さで判定）
        if (cardView.cardData.type == CardType.SPELL || cardView.cardData.type == CardType.BUILD) return true;

        return false;
    }
}