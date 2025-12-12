using UnityEngine;
using UnityEngine.UI;

public class SimpleCardModal : MonoBehaviour
{
    public static SimpleCardModal instance;

    [Header("UIパーツ")]
    public GameObject rootObject;   
    public CardView displayCard;    // 中央に表示するデカいカード
    public Button backgroundButton; // 背景（閉じる用）

    void Awake()
    {
        instance = this;
        if (backgroundButton != null)
        {
            backgroundButton.onClick.AddListener(Close);
        }
        Close(); 
    }

    public void Open(CardData data)
    {
        if (data == null) return;

        rootObject.SetActive(true);
        
        if (displayCard != null)
        {
            displayCard.SetCard(data);
            displayCard.enableHoverScale = false;
            displayCard.enableHoverDetail = false;
        }
    }

    public void Close()
    {
        if (rootObject != null) rootObject.SetActive(false);
    }
}