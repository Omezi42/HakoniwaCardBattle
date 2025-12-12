using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.EventSystems;

public class DeckEditManager : MonoBehaviour
{
    public static DeckEditManager instance;

    [Header("UI References")]
    [Tooltip("上段、中段、下段の順でアサインしてください")]
    public Transform[] deckShelfRows;

    [Header("新機能UI")]
    public TMP_InputField deckNameInput; // ★デッキ名変更用
    public ManaCurveGraph manaCurveGraph; // ★マナカーブグラフ
    
    [Header("パネル切替")]
    public GameObject deckListPanel;
    public GameObject editPanel;
    public GameObject newDeckPopup;

    [Header("デッキリスト画面用")]
    public Transform deckButtonContainer;
    public GameObject deckButtonPrefab;

    [Header("編集画面用")]
    public GameObject deckCardPrefab;      // 上部フィギュア
    public Transform cardListContent;      // 下部リストContent
    public GameObject listCardPrefab;      // 下部リストカード
    
    public TextMeshProUGUI deckCountText;
    
    // --- フィルター関連UI ---
    [Header("フィルターUI")]
    public Button filterButton;      
    public GameObject filterPanel;   
    public TMP_InputField searchInput;
    public Button applyFilterButton;
    public Button closeFilterButton;
    public Button resetFilterButton;

    public Button jobLabelButton;
    public Button costLabelButton;
    public Button rarityLabelButton;
    public Button typeLabelButton;

    public List<Toggle> jobToggles;
    public List<Toggle> costToggles;
    public List<Toggle> rarityToggles;
    public List<Toggle> typeToggles;
    // --------------------------------

    private DeckData currentDeck;
    private const int CARDS_PER_ROW = 10;
    private List<string> editingCardIds = new List<string>();

    void Awake() => instance = this;

    void Start()
    {
        // フィルターボタン登録（省略なし）
        if (filterButton) filterButton.onClick.AddListener(() => filterPanel.SetActive(true));
        if (applyFilterButton) applyFilterButton.onClick.AddListener(() => { RefreshEditUI(); filterPanel.SetActive(false); });
        if (closeFilterButton) closeFilterButton.onClick.AddListener(() => filterPanel.SetActive(false));
        if (resetFilterButton) resetFilterButton.onClick.AddListener(ResetFilter);

        if (jobLabelButton) jobLabelButton.onClick.AddListener(() => ToggleCategory(jobToggles));
        if (costLabelButton) costLabelButton.onClick.AddListener(() => ToggleCategory(costToggles));
        if (rarityLabelButton) rarityLabelButton.onClick.AddListener(() => ToggleCategory(rarityToggles));
        if (typeLabelButton) typeLabelButton.onClick.AddListener(() => ToggleCategory(typeToggles));

        if (filterPanel) filterPanel.SetActive(false);
        
        // ★追加：名前入力欄のリスナー登録
        if (deckNameInput != null)
        {
            deckNameInput.onEndEdit.AddListener(OnDeckNameChanged);
        }

        ShowDeckListPanel();
    }

    // --- デッキリスト画面 ---
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
            var text = btn.GetComponentInChildren<TextMeshProUGUI>(); 
            if(text) text.text = $"{decks[i].deckName} ({decks[i].deckJob})"; 
            btn.GetComponent<Button>().onClick.AddListener(() => StartEditing(index)); 
        } 
    }
    
    public void OnClickNewDeck() { newDeckPopup.SetActive(true); }
    public void OnSelectJobForNewDeck(int jobIndex) 
    { 
        DeckData newDeck = new DeckData(); 
        newDeck.deckName = $"Deck {PlayerDataManager.instance.playerData.decks.Count + 1}"; 
        newDeck.deckJob = (JobType)jobIndex; 
        PlayerDataManager.instance.playerData.decks.Add(newDeck); 
        StartEditing(PlayerDataManager.instance.playerData.decks.Count - 1); 
    }

    // --- 編集開始 ---
    public void StartEditing(int deckIndex)
    {
        PlayerDataManager.instance.playerData.currentDeckIndex = deckIndex;
        currentDeck = PlayerDataManager.instance.playerData.decks[deckIndex];
        editingCardIds = new List<string>(currentDeck.cardIds);

        deckListPanel.SetActive(false);
        newDeckPopup.SetActive(false);
        editPanel.SetActive(true);

        // ★追加：名前入力欄に現在の名前を反映
        if(deckNameInput != null) deckNameInput.text = currentDeck.deckName;
        
        ResetFilter();
        RefreshEditUI();
    }

    // --- UI更新 ---
    public void RefreshEditUI()
    {
        // 1. 下部リスト（所持カード）更新
        foreach (Transform child in cardListContent) Destroy(child.gameObject);
        
        var allCards = Resources.LoadAll<CardData>("CardsData").ToList();
        var filteredCards = allCards
            .Where(c => (c.job == JobType.NEUTRAL || c.job == currentDeck.deckJob))
            .Where(c => CheckFilter(c))
            .OrderBy(c => c.job == JobType.NEUTRAL)
            .ThenBy(c => c.cost)
            .ThenBy(c => c.id)
            .ToList();

        foreach (var card in filteredCards)
        {
            GameObject obj = Instantiate(listCardPrefab, cardListContent);
            var draggable = obj.AddComponent<DeckDraggable>();
            draggable.Setup(card);
            var view = obj.GetComponent<CardView>();
            if (view != null) 
            {
                view.SetCard(card);
                view.enableHoverScale = false;
                view.enableHoverDetail = false;
            }
            
            var btn = obj.GetComponent<Button>();
            if(btn == null) btn = obj.AddComponent<Button>();
            
            var trigger = obj.AddComponent<EventTrigger>();
            EventTrigger.Entry entryClick = new EventTrigger.Entry();
            entryClick.eventID = EventTriggerType.PointerClick;
            entryClick.callback.AddListener((data) => 
            {
                PointerEventData pData = (PointerEventData)data;
                if (pData.button == PointerEventData.InputButton.Left)
                {
                     if (SimpleCardModal.instance != null) SimpleCardModal.instance.Open(card);
                }
                else if (pData.button == PointerEventData.InputButton.Right)
                {
                     AddCardToDeck(card);
                }
            });
            trigger.triggers.Add(entryClick);
        }

        // 2. 上部デッキ（3段棚）更新
        if (deckShelfRows != null)
        {
            foreach (Transform row in deckShelfRows)
                if (row != null) foreach (Transform child in row) Destroy(child.gameObject);
        }
        
        var sortedDeckCards = editingCardIds
            .Select(id => PlayerDataManager.instance.GetCardById(id))
            .Where(data => data != null)
            .OrderBy(data => data.cost)
            .ThenBy(data => data.id)
            .ToList();

        for (int i = 0; i < sortedDeckCards.Count; i++)
        {
            CardData cardData = sortedDeckCards[i];
            int rowIndex = i / CARDS_PER_ROW;
            if (deckShelfRows != null && rowIndex >= deckShelfRows.Length) rowIndex = deckShelfRows.Length - 1;
            Transform targetRow = (deckShelfRows != null && deckShelfRows.Length > rowIndex) ? deckShelfRows[rowIndex] : null;

            if (targetRow != null)
            {
                GameObject obj = Instantiate(deckCardPrefab, targetRow);
                var figure = obj.GetComponent<DeckFigure>();
                if (figure != null) figure.Setup(cardData, 1);
            }
        }
        
        if (deckCountText) deckCountText.text = $"{editingCardIds.Count} / 30";
        if (deckCountText) deckCountText.color = (editingCardIds.Count == 30) ? Color.green : Color.white;

        // ★追加：マナカーブグラフの更新
        if (manaCurveGraph != null)
        {
            manaCurveGraph.UpdateGraph(sortedDeckCards);
        }
    }

    // --- フィルター関連ロジック ---
    void ResetFilter() { if (searchInput) searchInput.text = ""; SetAllToggles(jobToggles, true); SetAllToggles(costToggles, true); SetAllToggles(rarityToggles, true); SetAllToggles(typeToggles, true); }
    void ToggleCategory(List<Toggle> toggles) { if (toggles == null || toggles.Count == 0) return; bool allOn = toggles.All(t => t.isOn); SetAllToggles(toggles, !allOn); }
    void SetAllToggles(List<Toggle> toggles, bool state) { if (toggles == null) return; foreach (var t in toggles) if (t != null) t.isOn = state; }
    bool CheckFilter(CardData card) { if (searchInput != null && !string.IsNullOrEmpty(searchInput.text)) if (!card.cardName.Contains(searchInput.text)) return false; if (!CheckToggleGroup(jobToggles, (int)card.job)) return false; int costIndex = card.cost > 9 ? 9 : card.cost; if (!CheckToggleGroup(costToggles, costIndex)) return false; if (!CheckToggleGroup(rarityToggles, (int)card.rarity)) return false; if (!CheckToggleGroup(typeToggles, (int)card.type)) return false; return true; }
    bool CheckToggleGroup(List<Toggle> toggles, int index) { if (toggles == null || toggles.Count == 0) return true; if (index >= 0 && index < toggles.Count) return toggles[index].isOn; return false; }

    // --- デッキ操作 ---
    public void RemoveCardFromDeck(CardData card)
    {
        if (editingCardIds.Contains(card.id))
        {
            editingCardIds.Remove(card.id);
            RefreshEditUI();
        }
    }
    
    public void AddCardToDeck(CardData card)
    {
         if (editingCardIds == null) return;
         if (editingCardIds.Count >= 30) return;
         int count = editingCardIds.Count(id => id == card.id);
         if (count >= card.maxInDeck) return;
         editingCardIds.Add(card.id);
         RefreshEditUI();
    }

    // --- ★新機能：ボタン・入力イベント ---

    // 1. デッキ名変更
    public void OnDeckNameChanged(string newName)
    {
        if (currentDeck != null)
        {
            currentDeck.deckName = newName;
            // 即セーブはせず、セーブボタンで確定させるならここは変数更新だけでOK
            // ですが、念のためデータには反映しておきます
        }
    }

    // 2. セーブボタン
    public void OnClickSave()
    {
        if (currentDeck != null)
        {
            currentDeck.cardIds = new List<string>(editingCardIds);
            
            // InputFieldの値も念のため反映
            if (deckNameInput != null) currentDeck.deckName = deckNameInput.text;

            PlayerDataManager.instance.Save();
            Debug.Log("デッキを保存しました！");
            
            // 保存完了エフェクトなどを出すと親切です
        }
    }

    // 3. 戻るボタン（一覧へ戻る）
    public void OnClickReturn()
    {
        // ★未保存の警告などを出す場合はここに処理を追加
        ShowDeckListPanel();
    }

    // ドラッグ＆ドロップ対応
    public void OnCardDrop(CardData card, ZoneType targetZone)
    {
        if (targetZone == ZoneType.Deck) AddCardToDeck(card);
        else if (targetZone == ZoneType.Inventory) RemoveCardFromDeck(card);
    }
}