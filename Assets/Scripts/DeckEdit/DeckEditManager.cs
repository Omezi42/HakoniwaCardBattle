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

    public enum ScreenMode { DeckSelect, DeckEdit }
    private ScreenMode currentMode = ScreenMode.DeckSelect;

    [Header("共通UI References")]
    public Transform[] deckShelfRows;
    public Transform buildShelf;      
    
    [Header("デッキ情報（共通エリア）")]
    public TMP_InputField deckNameInput;
    public Image deckJobIconImage;
    public Sprite[] jobIcons;

    [Header("一覧モード用 (Deck Select)")]
    public GameObject deckSelectPanelRoot;
    // public Transform deckNameListContainer; // 廃止
    // public GameObject deckNameItemPrefab; // 廃止
    public PagedDeckManager pagedDeckManager; // 追加
    public Button createNewDeckButton;
    public Button editStartButton;
    public Button backToMenuButton;

    [Header("編集モード用 (Deck Edit)")]
    public GameObject deckEditPanelRoot;
    public Transform cardListContent;
    public GameObject listCardPrefab;
    public GameObject deckCardPrefab;
    public Button saveButton;
    public Button returnButton; 
    public Button deleteDeckButton;

    [Header("その他")]
    public ManaCurveGraph manaCurveGraph;
    public TextMeshProUGUI deckCountText;
    public TextMeshProUGUI buildCountText;
    public GameObject newDeckPopup;
    public Button closeNewDeckPopupButton;

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
        // イベント二重登録防止
        SetupButton(filterButton, () => filterPanel.SetActive(true));
        SetupButton(applyFilterButton, () => { RefreshEditUI(); filterPanel.SetActive(false); });
        SetupButton(closeFilterButton, () => filterPanel.SetActive(false));
        SetupButton(resetFilterButton, ResetFilter);

        SetupButton(jobLabelButton, () => ToggleCategory(jobToggles));
        SetupButton(costLabelButton, () => ToggleCategory(costToggles));
        SetupButton(rarityLabelButton, () => ToggleCategory(rarityToggles));
        SetupButton(typeLabelButton, () => ToggleCategory(typeToggles));

        SetupButton(createNewDeckButton, OnClickNewDeck);
        SetupButton(saveButton, OnClickSave);
        SetupButton(returnButton, OnClickReturn);
        SetupButton(editStartButton, OnClickStartEdit);
        SetupButton(backToMenuButton, OnClickBackToMenu);
        SetupButton(closeNewDeckPopupButton, OnClickCloseNewDeckPopup);
        SetupButton(deleteDeckButton, OnClickDeleteDeck);

        if (deckNameInput != null)
        {
            deckNameInput.onEndEdit.RemoveAllListeners();
            deckNameInput.onEndEdit.AddListener(OnDeckNameChanged);
            deckNameInput.characterLimit = 10; 
        }

        if (filterPanel) filterPanel.SetActive(false);
        
        // PagedDeckManagerのコールバック登録
        if (pagedDeckManager != null)
        {
            pagedDeckManager.OnDeckSelected = (deck) => {
                SelectDeckForPreview(deck);
            };
        }
        
        ShowDeckSelectScreen();
    }

    void SetupButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
    }

    public void OnClickDeleteDeck()
    {
        if (previewingDeck == null) return;
        
        // 念のためリストに含まれているか確認
        if (!PlayerDataManager.instance.playerData.decks.Contains(previewingDeck)) return;

        // 削除実行
        PlayerDataManager.instance.playerData.decks.Remove(previewingDeck);
        
        // 参照を切る
        previewingDeck = null;

        // インデックス調整（範囲外に出ないように）
        int count = PlayerDataManager.instance.playerData.decks.Count;
        if (PlayerDataManager.instance.playerData.currentDeckIndex >= count)
        {
            PlayerDataManager.instance.playerData.currentDeckIndex = Mathf.Max(0, count - 1);
        }

        PlayerDataManager.instance.Save();
        Debug.Log("デッキを削除しました");

        // UIをリロード
        if(pagedDeckManager != null) pagedDeckManager.ReloadDeckList();

        ShowDeckSelectScreen();
    }

    public void ShowDeckSelectScreen()
    {
        currentMode = ScreenMode.DeckSelect;
        
        if (deckSelectPanelRoot) deckSelectPanelRoot.SetActive(true);
        if (deckEditPanelRoot) deckEditPanelRoot.SetActive(false);
        if (newDeckPopup) newDeckPopup.SetActive(false);
        
        if (deckNameInput)
        {
            deckNameInput.gameObject.SetActive(true);
            deckNameInput.interactable = false; 
        }

        // RefreshDeckNameList(); // 削除：PagedManagerに委譲
        if (pagedDeckManager != null) pagedDeckManager.Initialize();

        int lastIndex = PlayerDataManager.instance.playerData.currentDeckIndex;
        var decks = PlayerDataManager.instance.playerData.decks;
        if (decks.Count > 0)
        {
            if (lastIndex < 0 || lastIndex >= decks.Count) lastIndex = 0;
            // SelectDeckForPreview は PagedDeckManager.Initialize 内で呼び出されるわけではないので、
            // ここで明示的に初期表示を行う
            SelectDeckForPreview(decks[lastIndex]);
        }
        else
        {
            previewingDeck = null;
            ClearShelf();
            if (deckNameInput) deckNameInput.text = "";
            UpdateJobIcon(null);
        }
    }

    // RefreshDeckNameList() は削除

    public void SelectDeckForPreview(DeckData deck)
    {
        previewingDeck = deck;
        // インデックスを保存
        if (PlayerDataManager.instance.playerData.decks.Contains(deck))
        {
            PlayerDataManager.instance.playerData.currentDeckIndex = 
                PlayerDataManager.instance.playerData.decks.IndexOf(deck);
        }

        if (deckNameInput != null)
        {
            deckNameInput.text = deck.deckName;
            deckNameInput.gameObject.SetActive(true);
        }
        
        UpdateJobIcon(deck);
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

    public void OnClickReturn()
    {
        ShowDeckSelectScreen();
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
        PlayerDataManager.instance.playerData.currentDeckIndex = PlayerDataManager.instance.playerData.decks.Count - 1;
        
        // リロードして最終ページへ
        if (pagedDeckManager != null)
        {
            pagedDeckManager.ReloadDeckList();
            pagedDeckManager.GoToLastPage();
        }

        StartEditing(newDeck);
    }

    public void StartEditing(DeckData deck)
    {
        currentMode = ScreenMode.DeckEdit;
        previewingDeck = deck; 

        editingCardIds = new List<string>(deck.cardIds);
        editingBuildIds = new List<string>(deck.buildIds);

        if (deckSelectPanelRoot) deckSelectPanelRoot.SetActive(false);
        if (deckEditPanelRoot) deckEditPanelRoot.SetActive(true);
        if (newDeckPopup) newDeckPopup.SetActive(false);

        if (deckNameInput) 
        {
            deckNameInput.gameObject.SetActive(true);
            deckNameInput.interactable = true; 
            deckNameInput.text = deck.deckName;
        }

        UpdateJobIcon(deck);
        ResetFilter();
        RefreshEditUI();
    }

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
        allCards = allCards.Distinct().ToList();

        var filteredCards = allCards
            .Where(c => (c.job == JobType.NEUTRAL || c.job == previewingDeck.deckJob))
            .Where(c => CheckFilter(c)) 
            .OrderBy(c => c.job == JobType.NEUTRAL ? 99 : (int)c.job) 
            .ThenBy(c => (int)c.type)                                 
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
            if (btn) Destroy(btn);
        }
    }

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
        // ★修正：確実にリスト内のオブジェクトと一致するか確認
        if (previewingDeck != null && PlayerDataManager.instance.playerData.decks.Contains(previewingDeck))
        {
            previewingDeck.cardIds = new List<string>(editingCardIds);
            previewingDeck.buildIds = new List<string>(editingBuildIds);
            if (deckNameInput) previewingDeck.deckName = deckNameInput.text;
            
            PlayerDataManager.instance.Save();
            Debug.Log("保存しました");
            if(pagedDeckManager != null) pagedDeckManager.ReloadDeckList(); // 名前が変わったかもしれないのでリロード
            ShowDeckSelectScreen();
        }
        else
        {
            Debug.LogError("保存対象のデッキが見つかりません");
        }
    }
    
    public void OnCardDrop(CardData card, ZoneType targetZone)
    {
        if (currentMode != ScreenMode.DeckEdit) return;

        if (targetZone == ZoneType.MainDeck && card.type != CardType.BUILD) AddCardToDeck(card);
        else if (targetZone == ZoneType.BuildDeck && card.type == CardType.BUILD) AddCardToDeck(card);
    }

    void ResetFilter() { if (searchInput) searchInput.text = ""; SetAllToggles(jobToggles, true); SetAllToggles(costToggles, true); SetAllToggles(rarityToggles, true); SetAllToggles(typeToggles, true); }
    void ToggleCategory(List<Toggle> toggles) { if (toggles == null || toggles.Count == 0) return; bool allOn = toggles.All(t => t.isOn); SetAllToggles(toggles, !allOn); }
    void SetAllToggles(List<Toggle> toggles, bool state) { if (toggles == null) return; foreach (var t in toggles) if (t != null) t.isOn = state; }
    bool CheckFilter(CardData card) { if (searchInput != null && !string.IsNullOrEmpty(searchInput.text)) if (!card.cardName.Contains(searchInput.text)) return false; if (!CheckToggleGroup(jobToggles, (int)card.job)) return false; int costIndex = card.cost > 9 ? 9 : card.cost; if (!CheckToggleGroup(costToggles, costIndex)) return false; if (!CheckToggleGroup(rarityToggles, (int)card.rarity)) return false; if (!CheckToggleGroup(typeToggles, (int)card.type)) return false; return true; }
    bool CheckToggleGroup(List<Toggle> toggles, int index) { if (toggles == null || toggles.Count == 0) return true; if (index >= 0 && index < toggles.Count) return toggles[index].isOn; return false; }
}