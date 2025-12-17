using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text; // StringBuilder用

public class CardDetailModal : MonoBehaviour
{
    public static CardDetailModal instance;

    [Header("UIパーツ")]
    public GameObject rootObject;
    public Image figureImage; // InteractiveFigureがついている画像
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI statsText;

    [Header("ナビゲーション")]
    public Button closeButton;
    public Button nextButton;
    public Button prevButton;

    private InteractiveFigure interactiveFigure;
    private List<CardData> currentList; // 現在見ているリスト
    private int currentIndex;           // 現在の番号

    void Awake()
    {
        instance = this;
        if (figureImage != null)
            interactiveFigure = figureImage.GetComponent<InteractiveFigure>();
        
        // ボタン設定
        if (closeButton) closeButton.onClick.AddListener(Close);
        if (nextButton) nextButton.onClick.AddListener(OnClickNext);
        if (prevButton) prevButton.onClick.AddListener(OnClickPrev);

        Close(); // 最初は閉じる
    }

    // リストと番号を受け取って開く
    public void Open(List<CardData> list, int index)
    {
        currentList = list;
        currentIndex = index;
        
        rootObject.SetActive(true);
        ShowCard();
    }

    void ShowCard()
    {
        if (currentList == null || currentList.Count == 0) return;

        currentIndex = Mathf.Clamp(currentIndex, 0, currentList.Count - 1);
        CardData data = currentList[currentIndex];

        // データ反映
        if (data.cardIcon != null) 
        {
            if (figureImage != null)
            {
                figureImage.sprite = data.cardIcon;
                figureImage.preserveAspect = true;
                
                if (interactiveFigure != null)
                {
                    interactiveFigure.AdjustSizeAndCollider();
                }
            }
        }

        if (nameText) nameText.text = data.cardName;
        
        // ★修正：キーワード説明を追記する処理
        if (descText)
        {
            string fullDesc = data.description;
            string keywordNote = GetKeywordExplanations(data);
            
            if (!string.IsNullOrEmpty(keywordNote))
            {
                // 元の説明文の下に改行を入れて追加
                fullDesc += "\n\n" + keywordNote;
            }
            descText.text = fullDesc;
        }
        
        // ステータス表示
        if (statsText)
        {
            if (data.type == CardType.UNIT)
            {
                statsText.text = $"COST: {data.cost}   ATK: {data.attack} / HP: {data.health}";
            }
            else if (data.type == CardType.BUILD)
            {
                statsText.text = $"COST: {data.cost}   DUR: {data.duration}";
            }
            else
            {
                statsText.text = $"COST: {data.cost} (Spell)";
            }
        }

        if (interactiveFigure != null) interactiveFigure.ResetPosition();

        if (nextButton) nextButton.interactable = (currentIndex < currentList.Count - 1);
        if (prevButton) prevButton.interactable = (currentIndex > 0);
    }

    // ★追加：キーワードの解説文を生成するメソッド
    string GetKeywordExplanations(CardData data)
    {
        StringBuilder sb = new StringBuilder();
        HashSet<EffectType> added = new HashSet<EffectType>(); // 重複防止用

        foreach (var abi in data.abilities)
        {
            // パッシブ効果のみ対象（トリガー系も必要ならここに追加）
            if (abi.trigger == EffectTrigger.PASSIVE)
            {
                if (added.Contains(abi.effect)) continue;

                switch (abi.effect)
                {
                    case EffectType.TAUNT:
                        sb.AppendLine("<color=#FFD700>【守護】</color> 相手はこのユニット以外を攻撃できない。（潜伏中は無効）");
                        break;
                    case EffectType.STEALTH:
                        sb.AppendLine("<color=#FFD700>【潜伏】</color> 攻撃するまで相手の効果や攻撃の対象にならない。");
                        break;
                    case EffectType.QUICK:
                        sb.AppendLine("<color=#FFD700>【疾風】</color> 移動した後、同じターンに攻撃できる。");
                        break;
                    case EffectType.HASTE:
                        sb.AppendLine("<color=#FFD700>【速攻】</color> 召喚したターンに攻撃できる。");
                        break;
                    case EffectType.PIERCE:
                        sb.AppendLine("<color=#FFD700>【貫通】</color> 攻撃時、正面の敵を貫通して後ろの敵にもダメージを与える。");
                        break;
                    case EffectType.SPELL_DAMAGE_PLUS:
                        sb.AppendLine("<color=#FFD700>【魔導】</color> スペルカードのダメージが増加する。");
                        break;
                }
                added.Add(abi.effect);
            }
        }

        return sb.ToString();
    }
    
    public void OnClickNext()
    {
        if (currentIndex < currentList.Count - 1)
        {
            currentIndex++;
            ShowCard();
        }
    }

    public void OnClickPrev()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            ShowCard();
        }
    }

    public void Close()
    {
        rootObject.SetActive(false);
    }
}