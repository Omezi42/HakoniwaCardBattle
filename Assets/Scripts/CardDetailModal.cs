using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
            figureImage.sprite = data.cardIcon;
            figureImage.preserveAspect = true;
            
            if (interactiveFigure != null)
            {
                interactiveFigure.AdjustSizeAndCollider();
            }
        }

        if (nameText) nameText.text = data.cardName;
        if (descText) descText.text = data.description;
        
        // ★修正：ビルドのステータス表示に対応
        if (statsText)
        {
            if (data.type == CardType.UNIT)
            {
                // COST / ATK / HP を並べて表示
                statsText.text = $"COST: {data.cost}   ATK: {data.attack} / HP: {data.health}";
            }
            else if (data.type == CardType.BUILD)
            {
                // ビルドの場合は持続ターン(Duration)を表示
                statsText.text = $"COST: {data.cost}   DUR: {data.duration}";
            }
            else
            {
                // スペル
                statsText.text = $"COST: {data.cost} (Spell)";
            }
        }

        if (interactiveFigure != null) interactiveFigure.ResetPosition();

        if (nextButton) nextButton.interactable = (currentIndex < currentList.Count - 1);
        if (prevButton) prevButton.interactable = (currentIndex > 0);
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