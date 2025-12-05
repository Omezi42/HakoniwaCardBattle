using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class CardListManager : MonoBehaviour
{
    [Header("UIパーツ")]
    public Transform contentParent; 
    public GameObject cardPrefab;

    [Header("フィルタ用UI")]
    public TMP_InputField searchInput;
    public TMP_Dropdown jobFilter;
    public TMP_Dropdown costFilter;
    
    // public Toggle spellOnlyToggle; // ← 削除
    public TMP_Dropdown typeFilter;   // ★追加：タイプ選択用プルダウン

    private List<CardData> allCards = new List<CardData>();

    void Start()
    {
        allCards = Resources.LoadAll<CardData>("CardsData").ToList();
        SetupFilters();
        RefreshList();
    }

    void SetupFilters()
    {
        // 1. ジョブフィルタ (既存)
        jobFilter.ClearOptions();
        List<string> jobs = new List<string> { "ALL" };
        jobs.AddRange(System.Enum.GetNames(typeof(JobType)));
        jobFilter.AddOptions(jobs);

        // 2. ★追加：タイプフィルタの設定
        // CardTypeの定義（UNIT, SPELL...）を自動で取得して選択肢にします
        typeFilter.ClearOptions();
        List<string> types = new List<string> { "ALL" };
        types.AddRange(System.Enum.GetNames(typeof(CardType))); 
        typeFilter.AddOptions(types);

        // イベント登録
        searchInput.onValueChanged.AddListener(delegate { RefreshList(); });
        jobFilter.onValueChanged.AddListener(delegate { RefreshList(); });
        costFilter.onValueChanged.AddListener(delegate { RefreshList(); });
        
        // spellOnlyToggle... ← 削除
        typeFilter.onValueChanged.AddListener(delegate { RefreshList(); }); // ★追加
    }

    void RefreshList()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        var filtered = allCards.Where(card => {
            // 1. 名前検索
            if (!string.IsNullOrEmpty(searchInput.text))
            {
                if (!card.cardName.Contains(searchInput.text)) return false;
            }

            // 2. ジョブフィルタ
            if (jobFilter.value > 0) 
            {
                JobType selectedJob = (JobType)(jobFilter.value - 1);
                if (card.job != selectedJob) return false;
            }

            // 3. コストフィルタ
            if (costFilter.value > 0)
            {
                if (card.cost != costFilter.value) return false;
            }

            // 4. ★変更：タイプフィルタ
            if (typeFilter.value > 0) // 0は"ALL"なのでスルー
            {
                // Dropdownの選択肢(1,2...)をEnumの値(0,1...)に変換
                CardType selectedType = (CardType)(typeFilter.value - 1);
                
                if (card.type != selectedType) return false;
            }

            return true;
        }).ToList();

        foreach (var card in filtered)
        {
            GameObject obj = Instantiate(cardPrefab, contentParent);
            var view = obj.GetComponent<CardView>();
            if(view != null) view.SetCard(card);
        }
    }

    public void OnClickBack()
    {
        SceneManager.LoadScene("MenuScene");
    }
}