using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class DeckViewManager : MonoBehaviour
{
    public static DeckViewManager instance;

    [Header("Popup UI")]
    public GameObject deckViewPopupRoot;
    public TextMeshProUGUI deckNameText;
    public Image jobIconImage;
    public TextMeshProUGUI deckCodeText;
    public Button closeButton;
    public Button copyCodeButton;

    [Header("Visuals")]
    public Transform cardGridContainer; // 3x10グリッド用
    public Transform buildGridContainer; // 3つ用
    public GameObject cardPrefab; // 変更: DeckFigure -> CardPrefab (CardView)
    public ManaCurveGraph manaCurveGraph; // 既存のグラフコンポーネントを使い回す

    [Header("Resources")]
    public Sprite[] jobIcons;

    private DeckData viewingDeck;

    void Awake()
    {
        instance = this;
        if (deckViewPopupRoot) deckViewPopupRoot.SetActive(false);
        
        if (closeButton) closeButton.onClick.AddListener(CloseDeckView);
        if (copyCodeButton) copyCodeButton.onClick.AddListener(PublishAndCopyCode);
    }

    public void OpenDeckView(DeckData deck)
    {
        if (deck == null) return;
        viewingDeck = deck;
        
        if (deckViewPopupRoot) deckViewPopupRoot.SetActive(true);
        
        // 基本情報表示
        if (deckNameText) deckNameText.text = deck.deckName;
        if (jobIconImage && jobIcons != null)
        {
            int index = (int)deck.deckJob;
            if (index >= 0 && index < jobIcons.Length)
            {
                jobIconImage.sprite = jobIcons[index];
                jobIconImage.gameObject.SetActive(true);
            }
        }

        // ここで「長いコード」は生成しておくが、表示はしない（あるいはボタンを押して発行）
        deckCodeText.text = "Click 'Publish' to get code";
        
        RefreshCardList(deck);
    }
    
    // UIボタンから呼ぶ: コードを発行して表示＆コピー
    public void PublishAndCopyCode()
    {
        if (viewingDeck == null) return;
        
        string longCode = DeckCodeUtility.Encode(viewingDeck);

        // Firebaseが生きていれば保存
        if (DeckCodeDatabaseManager.instance != null)
        {
            deckCodeText.text = "Publishing...";
            DeckCodeDatabaseManager.instance.RegisterDeck(longCode, (shortCode, error) => {
                if (string.IsNullOrEmpty(error))
                {
                    deckCodeText.text = shortCode;
                    GUIUtility.systemCopyBuffer = shortCode;
                    Debug.Log($"Published & Copied: {shortCode}");
                }
                else
                {
                    deckCodeText.text = "Error";
                    Debug.LogError(error);
                    // 失敗したらローカルコードを出すなどのフォールバックも可
                }
            });
        }
        else
        {
            // Firebaseが無い場合は長いコードをそのまま出す
            deckCodeText.text = longCode;
            GUIUtility.systemCopyBuffer = longCode;
            Debug.Log("Copied local code (Firebase not active)");
        }
    }

    void RefreshCardList(DeckData deck)
    {
        // メインデッキ
        if (cardGridContainer)
        {
            foreach (Transform child in cardGridContainer) Destroy(child.gameObject);
            
            // ソート: コスト -> ID
            var sortedIds = deck.cardIds
                .OrderBy(id => 
                {
                    var c = PlayerDataManager.instance.GetCardById(id);
                    return c != null ? c.cost : 99;
                })
                .ThenBy(id => id);

            List<CardData> cardObjList = new List<CardData>();

            foreach (var id in sortedIds)
            {
                var card = PlayerDataManager.instance.GetCardById(id);
                if (card)
                {
                    cardObjList.Add(card);
                    GameObject obj = Instantiate(cardPrefab, cardGridContainer);
                    
                    // CardViewコンポーネントを使用
                    var view = obj.GetComponent<CardView>();
                    if (view)
                    {
                        view.SetCard(card);
                        // 詳細画面なのでホバー効果も無効化したい場合はここで行う
                        view.enableHoverScale = false; 
                        view.enableHoverDetail = false;
                    }

                    // ★修正: ドラッグやクリック判定を削除して「見るだけ」にする
                    // Draggable系コンポーネントがあれば削除
                    var draggable = obj.GetComponent<Draggable>();
                    if (draggable) Destroy(draggable);
                    
                    var deckDraggable = obj.GetComponent<DeckDraggable>();
                    if (deckDraggable) Destroy(deckDraggable);

                    // Buttonコンポーネントがあれば削除（クリック反応を消す）
                    var btn = obj.GetComponent<Button>();
                    if (btn) Destroy(btn);
                    
                    // Raycast Targetも切っておくと安心（CardView内などのImageで制御が必要な場合もあるが、親のCanvasGroupで切る手もある）
                    // ここではシンプルにコンポーネント削除で対応
                }
            }
            
            // マナカーブ更新
            if (manaCurveGraph) manaCurveGraph.UpdateGraph(cardObjList);
        }

        // ビルドデッキ
        if (buildGridContainer)
        {
            foreach (Transform child in buildGridContainer) Destroy(child.gameObject);
            foreach (var id in deck.buildIds)
            {
                var card = PlayerDataManager.instance.GetCardById(id);
                if (card)
                {
                    GameObject obj = Instantiate(cardPrefab, buildGridContainer);
                    var view = obj.GetComponent<CardView>();
                    if (view) 
                    {
                        view.SetCard(card);
                        view.enableHoverScale = false; 
                        view.enableHoverDetail = false;
                    }
                    
                    // ★修正: ドラッグ/クリック無効化
                    if (obj.GetComponent<Draggable>()) Destroy(obj.GetComponent<Draggable>());
                    if (obj.GetComponent<DeckDraggable>()) Destroy(obj.GetComponent<DeckDraggable>());
                    if (obj.GetComponent<Button>()) Destroy(obj.GetComponent<Button>());
                }
            }
        }
    }

    public void CloseDeckView()
    {
        if (deckViewPopupRoot) deckViewPopupRoot.SetActive(false);
    }
}
