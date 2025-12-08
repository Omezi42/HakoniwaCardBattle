using UnityEngine;
using UnityEngine.EventSystems;

public class BuildSlotClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool isPlayerSlot = true; // 自分用ならON、敵用ならOFF

    public void OnPointerClick(PointerEventData eventData)
    {
        // GameManager経由でビルドメニューを開く
        if (GameManager.instance != null)
        {
            GameManager.instance.OpenBuildMenu(isPlayerSlot);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GameManager.instance != null && !GameManager.instance.IsArrowActive)
        {
            GameManager.instance.ShowTooltip("ビルド");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.HideTooltip();
        }
    }
}