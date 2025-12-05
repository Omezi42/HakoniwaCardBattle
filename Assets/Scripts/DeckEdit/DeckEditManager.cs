using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq; // ★追加：枚数カウントなどで使用

public class DeckEditManager : MonoBehaviour
{
    public static DeckEditManager instance;

    [Header("パネル切替")]
    public GameObject deckListPanel; // デッキ選択・作成画面
    public GameObject editPanel;     // カードドラッグ画面
    public GameObject newDeckPopup;  // ジョブ選択ポップアップ

    [Header("デッキリスト用")]
    public Transform deckButtonContainer;
    public GameObject deckButtonPrefab; // デッキを選択するボタン

    [Header("編集画面用")]
    public Transform inventoryContent;
    public Transform deckContent;
    public GameObject cardPrefab;
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI currentDeckNameText;

    // 現在編集中のデータ
    private DeckData currentDeck;
    private List<string> editingCardIds = new List<string>();

    void Awake() => instance = this;

    void Start()
    {
        ShowDeckListPanel();
    }

    // --- 1. デッキリスト画面 ---

    public void ShowDeckListPanel()
    {
        deckListPanel.SetActive(true);
        editPanel.SetActive(false);
        newDeckPopup.SetActive(false);

        foreach (Transform child in deckButtonContainer) Destroy(child.gameObject);
        
        var decks = PlayerDataManager.instance.playerData.decks;
        for (int i = 0; i < decks.Count; i++)
        {
            int index = i;
            GameObject btn = Instantiate(deckButtonPrefab, deckButtonContainer);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = $"{decks[i].deckName} ({decks[i].deckJob})";
            btn.GetComponent<Button>().onClick.AddListener(() => StartEditing(index));
        }
    }

    public void OnClickNewDeck()
    {
        newDeckPopup.SetActive(true);
    }

    public void OnSelectJobForNewDeck(int jobIndex)
    {
        DeckData newDeck = new DeckData();
        newDeck.deckName = $"Deck {PlayerDataManager.instance.playerData.decks.Count + 1}";
        newDeck.deckJob = (JobType)jobIndex;
        
        PlayerDataManager.instance.playerData.decks.Add(newDeck);
        StartEditing(PlayerDataManager.instance.playerData.decks.Count - 1);
    }

    // --- 2. 編集画面 ---

    public void StartEditing(int deckIndex)
    {
        PlayerDataManager.instance.playerData.currentDeckIndex = deckIndex;
        currentDeck = PlayerDataManager.instance.playerData.decks[deckIndex];
        editingCardIds = new List<string>(currentDeck.cardIds);

        deckListPanel.SetActive(false);
        newDeckPopup.SetActive(false);
        editPanel.SetActive(true);

        currentDeckNameText.text = $"Editing: {currentDeck.deckName} ({currentDeck.deckJob})";
        
        RefreshEditUI();
    }

    // ★追加：ドロップ時の処理（これがないとエラーになります）
    public void OnCardDrop(CardData card, ZoneType targetZone)
    {
        // A. デッキに入れようとした時
        if (targetZone == ZoneType.Deck)
        {
            // 1. デッキ枚数上限チェック (30枚)
            if (editingCardIds.Count >= 30)
            {
                Debug.Log("デッキが満杯です！");
                return;
            }

            // 2. 同名カードの枚数制限チェック
            int currentCount = editingCardIds.Count(id => id == card.id);
            if (currentCount >= card.maxInDeck)
            {
                Debug.Log($"{card.cardName} はこれ以上デッキに入れられません");
                return;
            }

            // 3. ジョブ縛りチェック
            if (card.job != JobType.NEUTRAL && card.job != currentDeck.deckJob)
            {
                Debug.Log("異なるジョブのカードは入れられません");
                return;
            }

            // 追加
            editingCardIds.Add(card.id);
        }
        // B. 所持リストに戻そうとした時（デッキから外す）
        else if (targetZone == ZoneType.Inventory)
        {
            if (editingCardIds.Contains(card.id))
            {
                editingCardIds.Remove(card.id);
            }
        }

        RefreshEditUI();
    }

    void RefreshEditUI()
    {
        // --- インベントリ表示 ---
        foreach (Transform child in inventoryContent) Destroy(child.gameObject);

        // 全カードから「共通(NEUTRAL)」または「デッキと同じジョブ」のみ抽出
        List<CardData> allCards = new List<CardData>(Resources.LoadAll<CardData>("CardsData"));
        
        foreach (var card in allCards)
        {
            if (card.job == JobType.NEUTRAL || card.job == currentDeck.deckJob)
            {
                // インベントリ側はゾーン指定 Inventory
                CreateCard(card, inventoryContent, ZoneType.Inventory);
            }
        }

        // --- デッキ内容表示 ---
        foreach (Transform child in deckContent) Destroy(child.gameObject);
        foreach (string id in editingCardIds)
        {
            CardData card = PlayerDataManager.instance.GetCardById(id);
            if (card != null) 
            {
                // デッキ側はゾーン指定 Deck
                CreateCard(card, deckContent, ZoneType.Deck);
            }
        }

        deckCountText.text = $"{editingCardIds.Count} / 30";
    }

    void CreateCard(CardData data, Transform parent, ZoneType zone)
    {
        GameObject obj = Instantiate(cardPrefab, parent);
        
        // 表示セットアップ
        CardView view = obj.GetComponent<CardView>();
        if (view != null) view.SetCard(data);

        // 古いドラッグ機能を消して新しいものをつける
        Draggable oldDrag = obj.GetComponent<Draggable>();
        if (oldDrag != null) DestroyImmediate(oldDrag);

        DeckDraggable newDrag = obj.AddComponent<DeckDraggable>();
        newDrag.Setup(data);
    }

    public void SaveAndExit()
    {
        currentDeck.cardIds = new List<string>(editingCardIds);
        PlayerDataManager.instance.Save();
        SceneManager.LoadScene("MenuScene");
    }
}