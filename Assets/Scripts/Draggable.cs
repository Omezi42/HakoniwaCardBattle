using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private CardView cardView;
    private UnityEngine.UI.GraphicRaycaster graphicRaycaster;

    // ★変更：画面の高さの5割（0.5）より上なら発動
    private const float SPELL_CAST_THRESHOLD = 0.5f; 

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        cardView = GetComponent<CardView>();
        graphicRaycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>(); // ★追加
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail();
        
        originalParent = transform.parent;
        transform.SetParent(transform.root);
        
        canvasGroup.blocksRaycasts = false;
        
        // ★追加：自身のRaycasterも切らないと、下のスロットにドロップできないことがある
        if (graphicRaycaster != null) graphicRaycaster.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // ★修正：Pivotが下(0)なので、中心(0.5)を持つために、高さの半分だけ下にずらす
        RectTransform rect = GetComponent<RectTransform>();
        float yOffset = rect.rect.height * transform.localScale.y * 0.5f; // 高さの半分（スケール考慮）
        
        // マウス位置から下にオフセットして配置
        transform.position = eventData.position - new Vector2(0, yOffset);

        // スペルカードの場合の拡大処理（既存）
        if (cardView != null && cardView.cardData.type == CardType.SPELL)
        {
            if (Input.mousePosition.y > Screen.height * SPELL_CAST_THRESHOLD)
            {
                // 拡大時は、オフセットも大きくなるので再計算してもいいですが、
                // 簡易的にそのままでも「中心より少し下」を持つ感じになり自然です
                transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }

        // ★追加：マナプレビュー
        if (cardView != null)
        {
            GameManager.instance.PreviewMana(cardView.cardData.cost);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        if (graphicRaycaster != null) graphicRaycaster.enabled = true;
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

        GameManager.instance.ResetManaPreview(); // 戻す
    }
}