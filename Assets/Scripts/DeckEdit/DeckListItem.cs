using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckListItem : MonoBehaviour
{
    [Header("UIパーツ")]
    public TextMeshProUGUI nameText; // デッキ名
    public Button selectButton;      // クリック用
    public GameObject highlightObj;  // 選択中であることを示す下線や枠

    // セットアップ関数
    public void Setup(DeckData deck, System.Action<DeckData> onClick)
    {
        nameText.text = deck.deckName;
        
        // ジョブによって文字色を変えるとおしゃれかも
        /*
        switch(deck.deckJob) {
            case JobType.KNIGHT: nameText.color = new Color(1f, 0.8f, 0.8f); break;
            // ...
        }
        */

        // クリックイベント
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClick(deck));
    }

    public void SetSelected(bool isSelected)
    {
        if (highlightObj != null) highlightObj.SetActive(isSelected);
        // 選択中は文字色を変えるなどの演出も可
    }
}