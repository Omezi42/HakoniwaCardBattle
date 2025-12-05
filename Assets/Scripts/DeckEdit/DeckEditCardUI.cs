using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckEditCardUI : MonoBehaviour
{
    [Header("UIパーツ")]
    public Image cardIcon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public GameObject selectionHighlight; // デッキに入っている時の枠など

    private CardData myData;
    private System.Action<CardData> onClickCallback;

    // 初期化処理
    public void Setup(CardData data, bool isSelected, System.Action<CardData> onClick)
    {
        myData = data;
        onClickCallback = onClick;

        // UI反映
        if (data.cardIcon != null) cardIcon.sprite = data.cardIcon;
        nameText.text = data.cardName;
        costText.text = data.cost.ToString();

        // 選択状態（デッキに入っているか）の表示切り替え
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(isSelected);
        }
    }

    // ボタンにアタッチするクリックイベント
    public void OnClick()
    {
        if (myData != null)
        {
            onClickCallback?.Invoke(myData);
        }
    }
}