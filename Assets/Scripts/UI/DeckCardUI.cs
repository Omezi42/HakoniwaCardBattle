using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;

// IPointerClickHandler: 右クリック用, IPointerDown/Up: 長押し用
public class DeckCardUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Components")]
    public TextMeshProUGUI deckNameText;
    public Image jobIconImage;
    public Button selectButton;
    public Image selectionHighlight; 

    private DeckData myDeck;
    private UnityAction<DeckData> onSelectCallback;
    
    // 長押し判定用
    private float pressTime = 0f;
    private bool isPressing = false;
    private const float LONG_PRESS_DURATION = 0.5f; // 長押し秒数
    private bool longPressTriggered = false;

    public void Setup(DeckData deck, UnityAction<DeckData> callback)
    {
        myDeck = deck;
        onSelectCallback = callback;

        if (deckNameText != null) deckNameText.text = deck.deckName;
        
        // Buttonコンポーネントがある場合、イベントを奪い合う可能性があるため
        // ButtonのOnClickを使うか、このスクリプトで一括管理するか統一したほうが良い。
        // 今回は「左クリック単押し＝選択」「右クリックor長押し＝詳細」なので、
        // Buttonコンポーネントを使わず、IPointerClickHandlerで全部処理する方が安全だが、
        // 既存のButton機能を生かすため、AddListenerは維持しつつ、EventSystemのイベントも併用する。
        
        // ★修正: Buttonコンポーネントに頼らず、OnPointerClickで処理するためリスナー登録は削除
        // if (selectButton != null)
        // {
        //     selectButton.onClick.RemoveAllListeners();
        //     selectButton.onClick.AddListener(OnClicked);
        // }

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
    }

    void OnClicked()
    {
        // 長押しが成立していた場合はクリック（選択）処理を行わないようにする
        if (longPressTriggered) return;
        onSelectCallback?.Invoke(myDeck);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"OnPointerClick: Button={eventData.button}"); // デバッグログ

        // 左クリック: 長押し成立してなければ「選択」
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (!longPressTriggered)
            {
                OnClicked(); // 選択処理呼び出し
            }
        }
        // 右クリック: 詳細表示
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OpenDeckView();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Debug.Log("OnPointerDown"); // ログ抑制
        isPressing = true;
        pressTime = 0f;
        longPressTriggered = false;
        
        // 左クリック（タッチ）のみ長押し判定開始
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            StartCoroutine(LongPressCheck());
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Debug.Log("OnPointerUp"); // ログ抑制
        isPressing = false;
    }

    IEnumerator LongPressCheck()
    {
        while (isPressing && !longPressTriggered)
        {
            pressTime += Time.deltaTime;
            if (pressTime >= LONG_PRESS_DURATION)
            {
                Debug.Log("Long Press Triggered!"); // デバッグログ
                longPressTriggered = true;
                OpenDeckView();
            }
            yield return null;
        }
    }

    void OpenDeckView()
    {
        Debug.Log("Trying to Open Deck View...");
        if (DeckViewManager.instance != null && myDeck != null)
        {
            // 他の操作と競合しないよう、詳細が開かれたフラグなどが必要ならここで管理
            DeckViewManager.instance.OpenDeckView(myDeck);
            Debug.Log("OpenDeckView Called Success.");
        }
        else
        {
            Debug.LogWarning($"OpenDeckView Failed. Instance: {DeckViewManager.instance != null}, Deck: {myDeck != null}");
        }
    }
}
