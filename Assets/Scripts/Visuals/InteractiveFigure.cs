using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class InteractiveFigure : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Rigidbody2D rb;
    private RectTransform myRect;
    private RectTransform parentRect;
    private Canvas canvas;
    private BoxCollider2D boxCollider;
    private Image myImage;

    private Vector2 dragOffset;
    private Vector3 lastMousePos;

    // ★追加：このサイズに収まるように拡大縮小する
    [Header("最大表示サイズ")]
    public Vector2 targetMaxSize = new Vector2(400f, 600f);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        boxCollider = GetComponent<BoxCollider2D>();
        myImage = GetComponent<Image>();
        
        if (transform.parent != null)
        {
            parentRect = transform.parent.GetComponent<RectTransform>();
        }
    }

    // ★修正：画像サイズとコライダーサイズの両方を調整するメソッド
    public void AdjustSizeAndCollider()
    {
        if (myImage == null || myImage.sprite == null || boxCollider == null) return;

        // 1. スプライトの元サイズを取得
        float spriteW = myImage.sprite.rect.width;
        float spriteH = myImage.sprite.rect.height;

        if (spriteW <= 0 || spriteH <= 0) return;

        // 2. 拡大率を計算
        // 「幅400に合わせる倍率」と「高さ600に合わせる倍率」のうち、小さい方（はみ出さない方）を選ぶ
        float scaleX = targetMaxSize.x / spriteW;
        float scaleY = targetMaxSize.y / spriteH;
        float finalScale = Mathf.Min(scaleX, scaleY);

        // 3. 新しいサイズを決定
        Vector2 newSize = new Vector2(spriteW * finalScale, spriteH * finalScale);

        // 4. RectTransform（画像の見た目の大きさ）を更新
        myRect.sizeDelta = newSize;

        // 5. コライダーも同じ大きさに合わせる
        boxCollider.size = newSize;
        boxCollider.offset = Vector2.zero;

        // ※Preserve Aspect（アスペクト比維持）はオフでも綺麗に表示されますが、念のためオンでもOK
    }

    // ... (OnBeginDrag, OnDrag, OnEndDrag, ResetPosition はそのまま) ...
    public void OnBeginDrag(PointerEventData eventData)
    {
        rb.simulated = false;
        rb.velocity = Vector2.zero;
        lastMousePos = Input.mousePosition;

        Vector2 localPointerPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, 
            eventData.position, 
            canvas.worldCamera, 
            out localPointerPos
        );
        
        dragOffset = (Vector2)myRect.localPosition - localPointerPos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentRect == null) return;

        Vector2 localPointerPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, 
            eventData.position, 
            canvas.worldCamera, 
            out localPointerPos
        );

        Vector2 targetPos = localPointerPos + dragOffset;

        // 制限計算（スケール考慮）
        float limitX = (parentRect.rect.width - myRect.rect.width * transform.localScale.x) * 0.5f;
        float limitY = (parentRect.rect.height - myRect.rect.height * transform.localScale.y) * 0.5f;

        if (limitX < 0) limitX = 0;
        if (limitY < 0) limitY = 0;

        targetPos.x = Mathf.Clamp(targetPos.x, -limitX, limitX);
        targetPos.y = Mathf.Clamp(targetPos.y, -limitY, limitY);

        myRect.localPosition = targetPos;

        float deltaX = Input.mousePosition.x - lastMousePos.x;
        transform.Rotate(Vector3.back, deltaX * 0.5f);
        lastMousePos = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        rb.simulated = true;
        Vector3 delta = Input.mousePosition - lastMousePos;
        rb.velocity = delta * 0.1f; 
        rb.angularVelocity = Random.Range(-300f, 300f);
    }

    public void ResetPosition()
    {
        rb.simulated = true;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0;
        myRect.anchoredPosition = Vector2.zero;
        transform.rotation = Quaternion.identity;
    }
}