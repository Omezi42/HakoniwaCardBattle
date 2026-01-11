using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class CardListManager : MonoBehaviour
{
    public enum SortMode { Default, Cost, Name }

    [Header("メイン画面UI")]
    public Transform contentParent; 
    public GameObject cardPrefab;
    public Button sortButton;
    public TextMeshProUGUI sortButtonText;
    public Button filterButton;

    [Header("フィルターパネルUI")]
    public GameObject filterPanel;
    public TMP_InputField searchInput;
    public Button applyFilterButton;
    public Button closeFilterButton;
    public Button resetFilterButton;

    [Header("フィルターラベルボタン")]
    public Button jobLabelButton;
    public Button costLabelButton;
    public Button rarityLabelButton;
    public Button typeLabelButton;

    [Header("フィルタートグル群")]
    public List<Toggle> jobToggles;
    public List<Toggle> costToggles;
    public List<Toggle> rarityToggles;
    
    // [0]=Unit, [1]=Spell, [2]=Build
    public List<Toggle> typeToggles; 

    private List<CardData> allCards = new List<CardData>();
    private SortMode currentSortMode = SortMode.Default;

    void Start()
    {
        allCards = Resources.LoadAll<CardData>("CardsData").ToList();

        if (sortButton) sortButton.onClick.AddListener(OnClickSort);
        if (filterButton) filterButton.onClick.AddListener(OpenFilterPanel);
        if (applyFilterButton) applyFilterButton.onClick.AddListener(ApplyFilter);
        if (closeFilterButton) closeFilterButton.onClick.AddListener(CloseFilterPanel);
        if (resetFilterButton) resetFilterButton.onClick.AddListener(ResetFilter);

        if (jobLabelButton) jobLabelButton.onClick.AddListener(() => ToggleCategory(jobToggles));
        if (costLabelButton) costLabelButton.onClick.AddListener(() => ToggleCategory(costToggles));
        if (rarityLabelButton) rarityLabelButton.onClick.AddListener(() => ToggleCategory(rarityToggles));
        if (typeLabelButton) typeLabelButton.onClick.AddListener(() => ToggleCategory(typeToggles));

        if (filterPanel) filterPanel.SetActive(false);
        UpdateSortButtonText();
        RefreshList();

        // WebGL Input Fix
        if (searchInput != null) WebGLHelper.SetupInputField(searchInput);
    }

    void OnClickSort()
    {
        currentSortMode++;
        if (currentSortMode > SortMode.Name) currentSortMode = SortMode.Default;
        UpdateSortButtonText();
        RefreshList();
    }

    void UpdateSortButtonText()
    {
        if (sortButtonText)
        {
            switch (currentSortMode)
            {
                case SortMode.Default: sortButtonText.text = "デフォルト"; break;
                case SortMode.Cost:    sortButtonText.text = "コスト"; break;
                case SortMode.Name:    sortButtonText.text = "名前"; break;
            }
        }
    }

    void OpenFilterPanel() => filterPanel.SetActive(true);
    void CloseFilterPanel() => filterPanel.SetActive(false);
    void ApplyFilter() { RefreshList(); CloseFilterPanel(); }

    void ResetFilter()
    {
        if (searchInput) searchInput.text = "";
        SetAllToggles(jobToggles, true);
        SetAllToggles(costToggles, true);
        SetAllToggles(rarityToggles, true);
        SetAllToggles(typeToggles, true);
    }

    void ToggleCategory(List<Toggle> toggles)
    {
        if (toggles == null || toggles.Count == 0) return;
        bool allOn = toggles.All(t => t.isOn);
        SetAllToggles(toggles, !allOn);
    }

    void SetAllToggles(List<Toggle> toggles, bool state)
    {
        if (toggles == null) return;
        foreach (var t in toggles) if (t != null) t.isOn = state;
    }

    // --- リスト更新ロジック ---
    void RefreshList()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        var filtered = allCards.Where(card => CheckFilter(card)).ToList();

        switch (currentSortMode)
        {
            case SortMode.Cost:
                filtered = filtered
                    .OrderBy(c => c.cost)
                    .ThenBy(c => c.job == JobType.NEUTRAL ? 99 : (int)c.job)
                    .ThenBy(c => c.id)
                    .ToList();
                break;
            case SortMode.Name:
                filtered = filtered
                    .OrderBy(c => c.cardName)
                    .ToList();
                break;
            case SortMode.Default:
            default:
                // ★修正：デフォルトの並び順
                // 1. ジョブ順 (Knight=1 -> ... -> Neutral=0を最後に)
                // 2. タイプ順 (Unit=0 -> Spell=1 -> Build=2)
                // 3. コスト順
                filtered = filtered
                    .OrderBy(c => c.job == JobType.NEUTRAL ? 99 : (int)c.job) // ニュートラルを最後にする
                    .ThenBy(c => (int)c.type)                                 // 次にタイプ順
                    .ThenBy(c => c.cost)                                      // 次にコスト順
                    .ThenBy(c => c.id)
                    .ToList();
                break;
        }

        for (int i = 0; i < filtered.Count; i++)
        {
            CardData card = filtered[i];
            int index = i;

            GameObject obj = Instantiate(cardPrefab, contentParent);
            
            // 不要なコンポーネント削除
            var drag = obj.GetComponent<Draggable>();
            if (drag != null) Destroy(drag);
            var deckDrag = obj.GetComponent<DeckDraggable>();
            if (deckDrag != null) Destroy(deckDrag);

            var view = obj.GetComponent<CardView>();
            if(view != null) 
            {
                view.SetCard(card);
                view.enableHoverScale = false; 
                view.enableHoverDetail = false;
            }

            // クリックで詳細
            Button btn = obj.GetComponent<Button>();
            if (btn == null) btn = obj.AddComponent<Button>();
            btn.onClick.AddListener(() => 
            {
                if (CardDetailModal.instance != null)
                    CardDetailModal.instance.Open(filtered, index);
            });
        }
    }

    bool CheckFilter(CardData card)
    {
        if (searchInput != null && !string.IsNullOrEmpty(searchInput.text))
        {
            if (!card.cardName.Contains(searchInput.text)) return false;
        }

        if (!CheckToggleGroup(jobToggles, (int)card.job)) return false;

        int costIndex = card.cost > 9 ? 9 : card.cost;
        if (!CheckToggleGroup(costToggles, costIndex)) return false;

        if (!CheckToggleGroup(rarityToggles, (int)card.rarity)) return false;

        if (!CheckToggleGroup(typeToggles, (int)card.type)) return false;

        return true;
    }

    bool CheckToggleGroup(List<Toggle> toggles, int index)
    {
        bool anyOn = toggles.Any(t => t.isOn);
        if (!anyOn) return true; 

        if (index >= 0 && index < toggles.Count)
        {
            return toggles[index].isOn;
        }
        return false;
    }

    public void OnClickBack()
    {
        SceneManager.LoadScene("MenuScene");
    }
}