using UnityEngine;
using UnityEngine.EventSystems;

public class BuildSlotClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool isPlayerSlot = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        // ★修正：敵の建築スロットはクリックしても反応しない
        if (!isPlayerSlot) return;

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