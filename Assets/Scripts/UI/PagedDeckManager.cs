using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class PagedDeckManager : MonoBehaviour
{
    [Header("Settings")]
    public GameObject deckCardPrefab;
    public Transform deckGridContainer;
    public Sprite[] jobIcons; // 0:Neutral, 1:Knight, 2:Mage... JobType順

    [Header("Navigation")]
    public Button prevPageButton;
    public Button nextPageButton;
    public TextMeshProUGUI pageIndicatorText;
    
    private List<DeckData> allDecks;
    private int currentPage = 0;
    private const int ITEMS_PER_PAGE = 10;
    
    // 外部に選択を通知するコールバック
    public System.Action<DeckData> OnDeckSelected;

    private DeckData currentSelectedDeck;

    void Start()
    {
        if (prevPageButton) prevPageButton.onClick.AddListener(PrevPage);
        if (nextPageButton) nextPageButton.onClick.AddListener(NextPage);
    }

    public void Initialize()
    {
        // プレイヤーデータからデッキリスト取得
        if (PlayerDataManager.instance != null)
        {
            allDecks = PlayerDataManager.instance.playerData.decks;
        }
        else
        {
            allDecks = new List<DeckData>();
        }

        currentPage = 0;
        
        // 最後に選んでいたデッキがあれば、そのページを表示したい
        if (PlayerDataManager.instance != null)
        {
            int lastIndex = PlayerDataManager.instance.playerData.currentDeckIndex;
            if (lastIndex >= 0 && lastIndex < allDecks.Count)
            {
                currentSelectedDeck = allDecks[lastIndex];
                currentPage = lastIndex / ITEMS_PER_PAGE;
            }
        }
        
        RefreshGrid();
    }

    public void RefreshGrid()
    {
        // Gridの中身を一度クリア
        foreach (Transform child in deckGridContainer)
        {
            Destroy(child.gameObject);
        }

        if (allDecks == null || allDecks.Count == 0)
        {
            if (pageIndicatorText) pageIndicatorText.text = "0 / 0";
            return;
        }
        
        // ページ範囲計算
        int totalPages = Mathf.CeilToInt((float)allDecks.Count / ITEMS_PER_PAGE);
        if (currentPage < 0) currentPage = 0;
        if (currentPage >= totalPages) currentPage = totalPages - 1;

        int startIndex = currentPage * ITEMS_PER_PAGE;
        int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, allDecks.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            DeckData deck = allDecks[i];
            GameObject obj = Instantiate(deckCardPrefab, deckGridContainer);
            DeckCardUI ui = obj.GetComponent<DeckCardUI>();
            if (ui != null)
            {
                ui.Setup(deck, OnDeckCardClicked);
                
                // アイコン設定
                int jobIndex = (int)deck.deckJob;
                if (jobIcons != null && jobIndex >= 0 && jobIndex < jobIcons.Length)
                {
                    ui.SetJobIcon(jobIcons[jobIndex]);
                }
                
                // 選択状態
                bool isSelected = (currentSelectedDeck == deck);
                ui.SetSelected(isSelected);
            }
        }

        // ページインジケータ更新
        if (pageIndicatorText)
        {
            pageIndicatorText.text = $"{currentPage + 1} / {totalPages}";
        }

        // ボタン制御
        if (prevPageButton) prevPageButton.interactable = (currentPage > 0);
        if (nextPageButton) nextPageButton.interactable = (currentPage < totalPages - 1);
    }

    void OnDeckCardClicked(DeckData deck)
    {
        currentSelectedDeck = deck;
        
        // データマネージャーにも反映
        if (PlayerDataManager.instance != null && allDecks.Contains(deck))
        {
            PlayerDataManager.instance.playerData.currentDeckIndex = allDecks.IndexOf(deck);
        }

        // 外部通知
        OnDeckSelected?.Invoke(deck);

        // 表示更新（選択枠の切り替えのため）
        // 効率化するなら全再生成せず、GetComponentで枠だけ切り替えるのもありだが、
        // 10個程度なら再生成でも一瞬なので簡易実装とする。
        RefreshGrid();
    }

    void NextPage()
    {
        currentPage++;
        RefreshGrid();
    }

    void PrevPage()
    {
        currentPage--;
        RefreshGrid();
    }

    // デッキが追加・削除された後に呼ぶ
    public void ReloadDeckList()
    {
        if (PlayerDataManager.instance != null)
        {
            allDecks = PlayerDataManager.instance.playerData.decks;
        }
        RefreshGrid();
    }
    
    public void GoToLastPage()
    {
        if (allDecks == null || allDecks.Count == 0) return;
        int totalPages = Mathf.CeilToInt((float)allDecks.Count / ITEMS_PER_PAGE);
        currentPage = totalPages - 1;
        RefreshGrid();
    }
}
