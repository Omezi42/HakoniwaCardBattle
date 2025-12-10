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

    [Header("フィルターラベルボタン（一括操作用）")] // ★追加
    public Button jobLabelButton;
    public Button costLabelButton;
    public Button rarityLabelButton;
    public Button typeLabelButton;

    [Header("フィルタートグル群")]
    public List<Toggle> jobToggles;
    public List<Toggle> costToggles;
    public List<Toggle> rarityToggles;
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

        // ★追加：ラベルボタンの登録
        if (jobLabelButton) jobLabelButton.onClick.AddListener(() => ToggleCategory(jobToggles));
        if (costLabelButton) costLabelButton.onClick.AddListener(() => ToggleCategory(costToggles));
        if (rarityLabelButton) rarityLabelButton.onClick.AddListener(() => ToggleCategory(rarityToggles));
        if (typeLabelButton) typeLabelButton.onClick.AddListener(() => ToggleCategory(typeToggles));

        filterPanel.SetActive(false);
        UpdateSortButtonText();
        RefreshList();
    }

    // --- 並び替え機能 ---
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

    // --- フィルターパネル制御 ---
    void OpenFilterPanel()
    {
        filterPanel.SetActive(true);
    }

    void CloseFilterPanel()
    {
        filterPanel.SetActive(false);
    }

    void ApplyFilter()
    {
        RefreshList();
        CloseFilterPanel();
    }

    // ★修正：リセット時は「全てON」にする
    void ResetFilter()
    {
        if (searchInput) searchInput.text = "";

        SetAllToggles(jobToggles, true);
        SetAllToggles(costToggles, true);
        SetAllToggles(rarityToggles, true);
        SetAllToggles(typeToggles, true);
    }

    // ★追加：カテゴリごとの一括切り替え（全選択/全解除）
    void ToggleCategory(List<Toggle> toggles)
    {
        if (toggles == null || toggles.Count == 0) return;

        // 現在「全てON」になっているかチェック
        bool allOn = true;
        foreach (var t in toggles)
        {
            if (!t.isOn)
            {
                allOn = false;
                break;
            }
        }

        // 全てONなら「全てOFF」に、それ以外（一部OFFや全部OFF）なら「全てON」にする
        bool newState = !allOn;
        SetAllToggles(toggles, newState);
    }

    // トグルリストの状態を一括設定するヘルパー関数
    void SetAllToggles(List<Toggle> toggles, bool state)
    {
        if (toggles == null) return;
        foreach (var t in toggles)
        {
            if (t != null) t.isOn = state;
        }
    }

    // --- リスト更新ロジック ---
    void RefreshList()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        var filtered = allCards.Where(card => CheckFilter(card)).ToList();

        switch (currentSortMode)
        {
            case SortMode.Cost:
                filtered = filtered.OrderBy(c => c.cost).ThenBy(c => c.id).ToList();
                break;
            case SortMode.Name:
                filtered = filtered.OrderBy(c => c.cardName).ToList();
                break;
            case SortMode.Default:
            default:
                filtered = filtered.OrderBy(c => c.id).ToList();
                break;
        }

        for (int i = 0; i < filtered.Count; i++)
        {
            CardData card = filtered[i];
            int index = i;

            GameObject obj = Instantiate(cardPrefab, contentParent);
            
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

            Button btn = obj.GetComponent<Button>();
            if (btn == null) btn = obj.AddComponent<Button>();
            btn.onClick.AddListener(() => 
            {
                CardDetailModal.instance.Open(filtered, index);
            });
        }
    }

    bool CheckFilter(CardData card)
    {
        if (!string.IsNullOrEmpty(searchInput.text))
        {
            if (!card.cardName.Contains(searchInput.text)) return false;
        }

        // ★ロジック変更なし（全てOFFなら全表示というロジックは維持しても良いが、
        // リセットで全ONにするなら、素直にONのものだけ通すロジックでOK）

        // Job
        if (!CheckToggleGroup(jobToggles, (int)card.job)) return false;

        // Cost
        int costIndex = card.cost;
        if (costIndex > 9) costIndex = 9;
        if (!CheckToggleGroup(costToggles, costIndex)) return false;

        // Rarity
        if (!CheckToggleGroup(rarityToggles, (int)card.rarity)) return false;

        // Type
        if (!CheckToggleGroup(typeToggles, (int)card.type)) return false;

        return true;
    }

    // 指定したインデックスのトグルがONか、または「全OFF（＝絞り込みなし）」かを判定
    bool CheckToggleGroup(List<Toggle> toggles, int index)
    {
        // 全てOFFなら「条件なし」とみなして通過させる（お好みで）
        // 今回は「リセット＝全ON」にしたので、厳密にチェックしても良いですが、
        // ユーザビリティのために「全OFF＝全表示」の挙動を残しておくと便利です。
        bool anyOn = false;
        foreach (var t in toggles) if (t.isOn) { anyOn = true; break; }
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