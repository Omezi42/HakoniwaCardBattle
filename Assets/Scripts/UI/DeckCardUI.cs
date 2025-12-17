using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class DeckCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI deckNameText;
    public Image jobIconImage;
    public Button selectButton;
    public Image selectionHighlight; // 選択中であることを示す枠など（任意）

    private DeckData myDeck;
    private UnityAction<DeckData> onSelectCallback;

    public void Setup(DeckData deck, UnityAction<DeckData> callback)
    {
        myDeck = deck;
        onSelectCallback = callback;

        if (deckNameText != null) deckNameText.text = deck.deckName;
        
        // ジョブアイコン設定はManager側からSpriteをもらうか、ここで解決するか。
        // シンプルに今回はアイコン画像単体セットではなく、ManagerからSpriteを受け取る形でも良いが、
        // いったん「セットアップ時にアイコンSpriteを渡す」形が綺麗かも。
        // ただし、呼び出し元でSprite管理させるため、ここではSpriteの設定メソッドは分け、
        // Setupではデータのみ受け取る形にする。
        
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClicked);
        }

        SetSelected(false);
    }

    public void SetJobIcon(Sprite icon)
    {
        if (jobIconImage != null)
        {
            jobIconImage.sprite = icon;
            jobIconImage.gameObject.SetActive(icon != null);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.gameObject.SetActive(isSelected);
        }
        
        // 選択中はボタンを押せなくする等の制御が必要ならここで行う
        // if (selectButton != null) selectButton.interactable = !isSelected;
    }

    void OnClicked()
    {
        onSelectCallback?.Invoke(myDeck);
    }
}
