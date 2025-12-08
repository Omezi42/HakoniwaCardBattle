using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LeaderDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Leader leader;

    void Start()
    {
        // 親のLeaderコンポーネントを探す
        leader = GetComponentInParent<Leader>();
        
        // 透明な当たり判定用にするため、ImageのAlphaHitTestを設定
        var image = GetComponent<Image>();
        if (image != null) image.alphaHitTestMinimumThreshold = 0.001f;
    }

    // ドロップされた時（攻撃された時）
    public void OnDrop(PointerEventData eventData)
    {
        if (leader != null)
        {
            // 親のリーダーに処理を任せる
            leader.OnDrop(eventData);
        }
    }

    // マウスが乗った時（ビルドの吹き出しなどを出すならここで連携）
    public void OnPointerEnter(PointerEventData eventData)
    {
        // ドラッグ中や矢印操作中は表示しない
        if (eventData.pointerDrag != null) return;
        if (GameManager.instance != null && GameManager.instance.IsArrowActive) return;

        // ビルドの吹き出し（必要なら）
        // if (leader != null) GameManager.instance.ShowTooltip("ビルド");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GameManager.instance != null) GameManager.instance.HideTooltip();
    }
}