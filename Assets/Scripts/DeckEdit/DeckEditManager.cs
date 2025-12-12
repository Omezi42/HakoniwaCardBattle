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

    // --- モード管理 ---
    public enum ScreenMode { DeckSelect, DeckEdit }
    private ScreenMode currentMode = ScreenMode.DeckSelect;

    [Header("共通UI References")]
    public Transform[] deckShelfRows; // 上部の棚
    public Transform buildShelf;      // 左のビルド棚
    
    [Header("デッキ情報（共通エリア）")]
    // ★変更：テキスト表示用変数は削除し、Input欄を兼用します
    public TMP_InputField deckNameInput; // 名前表示＆変更欄（共通）
    public Image deckJobIconImage;       // ジョブアイコン（共通）
    public Sprite[] jobIcons;            // 画像リスト

    // --- モード別パネル ---
    [Header("一覧モード用 (Deck Select)")]
    public GameObject deckSelectPanelRoot;  // 下部の本パネル
    public Transform deckNameListContainer; // 本の中のリスト親
    public GameObject deckNameItemPrefab;   // リストアイテム
    public Button createNewDeckButton;      // 羽ペン
    public Button editStartButton;          // 編集開始ボタン
    public Button backToMenuButton;         // ★追加：メニューへ戻るボタン

    [Header("編集モード用 (Deck Edit)")]
    public GameObject deckEditPanelRoot;    // 下部のカード一覧パネル
    public Transform cardListContent;
    public GameObject listCardPrefab;
    public GameObject deckCardPrefab;
    public Button saveButton;               // 保存ボタン

    [Header("その他")]
    public ManaCurveGraph manaCurveGraph;
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI buildCountText;
    public GameObject newDeckPopup;
    public Button closeNewDeckPopupButton;

    // --- フィルター関連 ---
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

    private DeckData previewingDeck;
    private List<string> editingCardIds = new List<string>();
    private List<string> editingBuildIds = new List<string>();

    void Awake() => instance = this;

    void Start()
    {
        // ボタン登録
        if (filterButton) filterButton.onClick.AddListener(() => filterPanel.SetActive(true));
        if (applyFilterButton) applyFilterButton.onClick.AddListener(() => { RefreshEditUI(); filterPanel.SetActive(false); });
        if (closeFilterButton) closeFilterButton.onClick.AddListener(() => filterPanel.SetActive(false));
        if (resetFilterButton) resetFilterButton.onClick.AddListener(ResetFilter);

        if (jobLabelButton) jobLabelButton.onClick.AddListener(() => ToggleCategory(jobToggles));
        if (costLabelButton) costLabelButton.onClick.AddListener(() => ToggleCategory(costToggles));
        if (rarityLabelButton) rarityLabelButton.onClick.AddListener(() => ToggleCategory(rarityToggles));
        if (typeLabelButton) typeLabelButton.onClick.AddListener(() => ToggleCategory(typeToggles));

        if (createNewDeckButton) createNewDeckButton.onClick.AddListener(OnClickNewDeck);
        if (saveButton) saveButton.onClick.AddListener(OnClickSave);
        if (editStartButton) editStartButton.onClick.AddListener(OnClickStartEdit);
        if (backToMenuButton) backToMenuButton.onClick.AddListener(OnClickBackToMenu);

        if (closeNewDeckPopupButton != null) closeNewDeckPopupButton.onClick.AddListener(OnClickCloseNewDeckPopup);

        if (deckNameInput != null)
        {
            deckNameInput.onEndEdit.AddListener(OnDeckNameChanged);
            deckNameInput.characterLimit = 10; 
        }

        if (filterPanel) filterPanel.SetActive(false);
        
        ShowDeckSelectScreen();
    }

    // ========================================================================
    //  1. デッキ一覧（プレビュー）モード
    // ========================================================================

    public void ShowDeckSelectScreen()
    {
        currentMode = ScreenMode.DeckSelect;
        
        if (deckSelectPanelRoot) deckSelectPanelRoot.SetActive(true);
        if (deckEditPanelRoot) deckEditPanelRoot.SetActive(false);
        if (newDeckPopup) newDeckPopup.SetActive(false);
        
        // ★修正：入力欄を表示するが、操作不可（ReadOnly）にする
        if (deckNameInput)
        {
            deckNameInput.gameObject.SetActive(true);
            deckNameInput.interactable = false; // 入力不可
        }

        RefreshDeckNameList();

        // 最後に選択していたデッキを表示
        int lastIndex = PlayerDataManager.instance.playerData.currentDeckIndex;
        var decks = PlayerDataManager.instance.playerData.decks;
        if (decks.Count > 0)
        {
            if (lastIndex >= decks.Count) lastIndex = 0;
            SelectDeckForPreview(decks[lastIndex]);
        }
        else
        {
            ClearShelf();
            if (deckNameInput) deckNameInput.text = "";
            UpdateJobIcon(null);
        }
    }

    void RefreshDeckNameList()
    {
        foreach (Transform child in deckNameListContainer) Destroy(child.gameObject);

        var decks = PlayerDataManager.instance.playerData.decks;
        foreach (var deck in decks)
        {
            GameObject obj = Instantiate(deckNameItemPrefab, deckNameListContainer);
            DeckListItem item = obj.GetComponent<DeckListItem>();
            if (item != null)
            {
                item.Setup(deck, SelectDeckForPreview);
                item.SetSelected(previewingDeck == deck);
            }
        }
    }

    // デッキ選択（プレビュー）
    public void SelectDeckForPreview(DeckData deck)
    {
        previewingDeck = deck;
        PlayerDataManager.instance.playerData.currentDeckIndex = 
            PlayerDataManager.instance.playerData.decks.IndexOf(deck);

        RefreshDeckNameList();

        // ★名前の表示更新
        if (deckNameInput != null)
        {
            deckNameInput.text = deck.deckName;
            deckNameInput.gameObject.SetActive(true); // 念のため
        }
        
        // ★アイコンの表示更新
        UpdateJobIcon(deck);

        // 棚の更新
        UpdateShelfVisuals(deck.cardIds, deck.buildIds, false);
    }

    public void OnClickStartEdit()
    {
        if (previewingDeck != null) StartEditing(previewingDeck);
    }

    public void OnClickBackToMenu()
    {
        SceneManager.LoadScene("MenuScene");
    }

    public void OnClickNewDeck()
    {
        if (newDeckPopup) newDeckPopup.SetActive(true);
    }

    public void OnSelectJobForNewDeck(int jobIndex)
    {
        DeckData newDeck = new DeckData();
        newDeck.deckName = $"Deck {PlayerDataManager.instance.playerData.decks.Count + 1}";
        newDeck.deckJob = (JobType)jobIndex;
        PlayerDataManager.instance.playerData.decks.Add(newDeck);
        StartEditing(newDeck);
    }

    // ========================================================================
    //  2. デッキ編集モード
    // ========================================================================

    public void StartEditing(DeckData deck)
    {
        currentMode = ScreenMode.DeckEdit;
        previewingDeck = deck; 

        editingCardIds = new List<string>(deck.cardIds);
        editingBuildIds = new List<string>(deck.buildIds);

        if (deckSelectPanelRoot) deckSelectPanelRoot.SetActive(false);
        if (deckEditPanelRoot) deckEditPanelRoot.SetActive(true);
        if (newDeckPopup) newDeckPopup.SetActive(false);

        // ★修正：入力欄を入力可能にする
        if (deckNameInput) 
        {
            deckNameInput.gameObject.SetActive(true);
            deckNameInput.interactable = true; // 入力可能
            deckNameInput.text = deck.deckName;
        }

        UpdateJobIcon(deck);
        ResetFilter();
        RefreshEditUI();
    }

    // ジョブアイコン更新
    void UpdateJobIcon(DeckData deck)
    {
        if (deckJobIconImage != null && jobIcons != null)
        {
            if (deck != null)
            {
                int jobIndex = (int)deck.deckJob;
                if (jobIndex >= 0 && jobIndex < jobIcons.Length && jobIcons[jobIndex] != null)
                {
                    deckJobIconImage.sprite = jobIcons[jobIndex];
                    deckJobIconImage.gameObject.SetActive(true);
                    deckJobIconImage.preserveAspect = true;
                    return;
                }
            }
            deckJobIconImage.gameObject.SetActive(false);
        }
    }

    public void RefreshEditUI()
    {
        RefreshCardInventory();
        UpdateShelfVisuals(editingCardIds, editingBuildIds, true);
    }

    void RefreshCardInventory()
    {
        foreach (Transform child in cardListContent) Destroy(child.gameObject);
        
        var allCards = Resources.LoadAll<CardData>("CardsData").ToList();
        var filteredCards = allCards
            .Where(c => (c.job == JobType.NEUTRAL || c.job == previewingDeck.deckJob))
            .Where(c => CheckFilter(c)) 
            .OrderBy(c => c.type == CardType.BUILD ? 1 : 0)
            .ThenBy(c => c.cost)
            .ThenBy(c => c.id)
            .ToList();

        foreach (var card in filteredCards)
        {
            GameObject obj = Instantiate(listCardPrefab, cardListContent);
            var draggable = obj.AddComponent<DeckDraggable>();
            draggable.Setup(card);

            var view = obj.GetComponent<CardView>();
            if (view) 
            {
                view.SetCard(card);
                view.enableHoverScale = false;
                view.enableHoverDetail = false;
            }
            
            var btn = obj.GetComponent<Button>();
            if (!btn) btn = obj.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => AddCardToDeck(card));
        }
    }

    // ========================================================================
    //  棚の描画処理
    // ========================================================================

    void UpdateShelfVisuals(List<string> cardIds, List<string> buildIds, bool isEditable)
    {
        if (deckShelfRows != null)
        {
            foreach (Transform row in deckShelfRows)
                if (row) foreach (Transform child in row) Destroy(child.gameObject);
        }

        var sortedCards = cardIds
            .Select(id => PlayerDataManager.instance.GetCardById(id))
            .Where(d => d != null)
            .OrderBy(d => d.cost).ThenBy(d => d.id).ToList();

        const int CARDS_PER_ROW = 10;
        for (int i = 0; i < sortedCards.Count; i++)
        {
            CardData data = sortedCards[i];
            int rowIndex = i / CARDS_PER_ROW;
            if (deckShelfRows != null && rowIndex < deckShelfRows.Length)
            {
                Transform targetRow = deckShelfRows[rowIndex];
                GameObject obj = Instantiate(deckCardPrefab, targetRow);
                var figure = obj.GetComponent<DeckFigure>();
                if (figure) figure.Setup(data, 1);

                if (!isEditable) Destroy(obj.GetComponent<DeckFigure>()); 
            }
        }

        if (buildShelf != null) foreach (Transform child in buildShelf) Destroy(child.gameObject);
        
        foreach (string id in buildIds)
        {
            CardData data = PlayerDataManager.instance.GetCardById(id);
            if (data != null)
            {
                GameObject obj = Instantiate(deckCardPrefab, buildShelf);
                var figure = obj.GetComponent<DeckFigure>();
                if (figure) figure.Setup(data, 1);
                
                if (!isEditable) Destroy(obj.GetComponent<DeckFigure>());
            }
        }

        if (deckCountText) deckCountText.text = $"{cardIds.Count} / 30";
        if (buildCountText) buildCountText.text = $"Build: {buildIds.Count} / 3";
        if (manaCurveGraph) manaCurveGraph.UpdateGraph(sortedCards);
    }

    void ClearShelf()
    {
        if (deckShelfRows != null) foreach(var row in deckShelfRows) if(row) foreach(Transform c in row) Destroy(c.gameObject);
        if (buildShelf != null) foreach(Transform c in buildShelf) Destroy(c.gameObject);
    }

    public void AddCardToDeck(CardData card)
    {
        if (currentMode != ScreenMode.DeckEdit) return;

        if (card.type == CardType.BUILD)
        {
            if (editingBuildIds.Count >= 3) return;
            if (editingBuildIds.Contains(card.id)) return;
            editingBuildIds.Add(card.id);
        }
        else
        {
            if (editingCardIds.Count >= 30) return;
            if (editingCardIds.Count(id => id == card.id) >= card.maxInDeck) return;
            editingCardIds.Add(card.id);
        }
        RefreshEditUI();
    }

    public void RemoveCardFromDeck(CardData card)
    {
        if (currentMode != ScreenMode.DeckEdit) return;

        if (card.type == CardType.BUILD) editingBuildIds.Remove(card.id);
        else editingCardIds.Remove(card.id);
        
        RefreshEditUI();
    }

    public void OnDeckNameChanged(string newName)
    {
    }

    public void OnClickCloseNewDeckPopup()
    {
        if (newDeckPopup != null) newDeckPopup.SetActive(false);
    }

    public void OnClickSave()
    {
        if (previewingDeck != null)
        {
            previewingDeck.cardIds = new List<string>(editingCardIds);
            previewingDeck.buildIds = new List<string>(editingBuildIds);
            if (deckNameInput) previewingDeck.deckName = deckNameInput.text;
            
            PlayerDataManager.instance.Save();
            Debug.Log("保存しました");
            ShowDeckSelectScreen();
        }
    }
    
    public void OnCardDrop(CardData card, ZoneType targetZone)
    {
        if (currentMode != ScreenMode.DeckEdit) return;

        if (targetZone == ZoneType.MainDeck && card.type != CardType.BUILD) AddCardToDeck(card);
        else if (targetZone == ZoneType.BuildDeck && card.type == CardType.BUILD) AddCardToDeck(card);
    }

    // フィルター関連
    void ResetFilter() { if (searchInput) searchInput.text = ""; SetAllToggles(jobToggles, true); SetAllToggles(costToggles, true); SetAllToggles(rarityToggles, true); SetAllToggles(typeToggles, true); }
    void ToggleCategory(List<Toggle> toggles) { if (toggles == null || toggles.Count == 0) return; bool allOn = toggles.All(t => t.isOn); SetAllToggles(toggles, !allOn); }
    void SetAllToggles(List<Toggle> toggles, bool state) { if (toggles == null) return; foreach (var t in toggles) if (t != null) t.isOn = state; }
    bool CheckFilter(CardData card) { if (searchInput != null && !string.IsNullOrEmpty(searchInput.text)) if (!card.cardName.Contains(searchInput.text)) return false; if (!CheckToggleGroup(jobToggles, (int)card.job)) return false; int costIndex = card.cost > 9 ? 9 : card.cost; if (!CheckToggleGroup(costToggles, costIndex)) return false; if (!CheckToggleGroup(rarityToggles, (int)card.rarity)) return false; if (!CheckToggleGroup(typeToggles, (int)card.type)) return false; return true; }
    bool CheckToggleGroup(List<Toggle> toggles, int index) { if (toggles == null || toggles.Count == 0) return true; if (index >= 0 && index < toggles.Count) return toggles[index].isOn; return false; }
}