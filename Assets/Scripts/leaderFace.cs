using UnityEngine;
using UnityEngine.EventSystems;

public class LeaderFace : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("親のLeaderを登録")]
    public Leader leader;

    private void Start()
    {
        // 登録し忘れた場合の保険（親から探す）
        if (leader == null) leader = GetComponentInParent<Leader>();
    }

    // クリック時：ビルドメニューを開く
    public void OnPointerClick(PointerEventData eventData)
    {
        if (leader != null)
        {
            // ここで leader.isPlayerLeader が false（敵）なら、
            // GameManager.OpenMenu(false) が呼ばれ、敵用のメニューが開くはずです。
            GameManager.instance.OpenBuildMenu(leader.isPlayerLeader);
        }
    }

    // ホバー時：ツールチップを表示
    public void OnPointerEnter(PointerEventData eventData)
    {
        // ドラッグ中や矢印操作中は表示しない
        if (eventData.pointerDrag != null) return;
        if (GameManager.instance.IsArrowActive) return;

        if (leader != null)
        {
            // メニューが開いているなら表示しない
            if (GameManager.instance.buildUIManager != null && 
                GameManager.instance.buildUIManager.gameObject.activeSelf) // panelRoot ではなく gameObject.activeSelf をチェック
            {
                return;
            }

            GameManager.instance.ShowTooltip("ビルド");
        }
    }

    // ホバー終了時：ツールチップを隠す
    public void OnPointerExit(PointerEventData eventData)
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.HideTooltip();
        }
    }
}