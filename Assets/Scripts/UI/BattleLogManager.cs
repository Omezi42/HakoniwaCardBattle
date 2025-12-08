using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // Coroutine用

public class BattleLogManager : MonoBehaviour
{
    public static BattleLogManager instance;

    [Header("UIパーツ")]
    public GameObject logPanelRoot;     // ログウィンドウ全体
    public Transform contentTransform;  // ScrollViewのContent
    public ScrollRect scrollRect;
    
    // ★追加：閉じるボタン
    public Button closeButton; 

    [Header("プレハブ")]
    public GameObject logItemPrefab;    
    public GameObject turnLabelPrefab;  

    [Header("設定")]
    public Color playerColor = new Color(0.8f, 0.9f, 1f); 
    public Color enemyColor = new Color(1f, 0.8f, 0.8f);  

    private void Awake()
    {
        instance = this;
    }

    // ★追加：ボタンの登録
    void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseLogPanel);
        }
    }

    public void AddLog(string message, bool isPlayerAction)
    {
        if (logItemPrefab == null) return;

        GameObject obj = Instantiate(logItemPrefab, contentTransform);
        
        TextMeshProUGUI text = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = message;

        Image bg = obj.GetComponentInChildren<Image>();
        if (bg != null) bg.color = isPlayerAction ? playerColor : enemyColor;

        StartCoroutine(ScrollToBottom());
    }

    public void AddTurnLabel(int turnCount)
    {
        if (turnLabelPrefab == null) return;
        GameObject obj = Instantiate(turnLabelPrefab, contentTransform);
        obj.GetComponentInChildren<TextMeshProUGUI>().text = $"--- Turn {turnCount} ---";
        
        StartCoroutine(ScrollToBottom());
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    public void ToggleLogPanel()
    {
        if(logPanelRoot != null)
            logPanelRoot.SetActive(!logPanelRoot.activeSelf);
    }

    // ★追加：閉じる処理
    public void CloseLogPanel()
    {
        if(logPanelRoot != null)
            logPanelRoot.SetActive(false);
    }
}