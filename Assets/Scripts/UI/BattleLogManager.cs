using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class BattleLogManager : MonoBehaviour
{
    public static BattleLogManager instance;

    [Header("UIパーツ")]
    public Transform content;
    public GameObject logTextPrefab;
    public GameObject turnLogPrefab; // ターン表示用のプレハブ
    public ScrollRect scrollRect;
    public GameObject logPanel;
    
    [Header("ボタン")]
    public Button openButton;
    public Button closeButton;

    void Awake()
    {
        instance = this;
        if (openButton) openButton.onClick.AddListener(ToggleLogPanel);
        if (closeButton) closeButton.onClick.AddListener(CloseLogPanel);
        
        if (logPanel) logPanel.SetActive(false);
    }

    public void ToggleLogPanel()
    {
        if (logPanel != null)
        {
            bool isActive = !logPanel.activeSelf;
            logPanel.SetActive(isActive);
            if (isActive)
            {
                ScrollToBottom();
            }
        }
    }

    public void CloseLogPanel()
    {
        if (logPanel != null) logPanel.SetActive(false);
    }

    // 通常ログの追加
    public void AddLog(string text, bool isPlayerAction, CardData card = null)
    {
        if (content == null || logTextPrefab == null) return;

        GameObject obj = Instantiate(logTextPrefab, content);
        
        // 子要素も含めてテキストコンポーネントを探す
        TextMeshProUGUI txt = obj.GetComponentInChildren<TextMeshProUGUI>();
        
        if (txt)
        {
            txt.text = text;
            txt.color = isPlayerAction ? Color.cyan : new Color(1f, 0.6f, 0.6f);

            // ★修正：相手の行動なら背景反転＆右詰め
            if (!isPlayerAction)
            {
                // 背景（親オブジェクト）を左右反転
                obj.transform.localScale = new Vector3(-1, 1, 1);
                
                // テキスト（子オブジェクト）の反転を打ち消す（-1 * -1 = 1）
                txt.transform.localScale = new Vector3(-1, 1, 1);
                
                // テキストを右詰めにする
                txt.alignment = TextAlignmentOptions.MidlineRight; 
            }
            else
            {
                // 自分側は通常通り（念のため明示的に設定）
                obj.transform.localScale = Vector3.one;
                txt.transform.localScale = Vector3.one;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        // ホバー機能
        if (card != null)
        {
            // テキストオブジェクト自体、もしくはルートオブジェクトにイベントを追加
            var triggerObj = txt != null ? txt.gameObject : obj;
            var trigger = triggerObj.GetComponent<EventTrigger>();
            if (trigger == null) trigger = triggerObj.AddComponent<EventTrigger>();
            
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entryEnter.callback.AddListener((data) => {
                if (GameManager.instance != null) GameManager.instance.ShowUnitDetail(card);
            });
            trigger.triggers.Add(entryEnter);

            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((data) => {
                if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail();
            });
            trigger.triggers.Add(entryExit);
        }

        // パネルが開いているならスクロール更新
        if (logPanel != null && logPanel.activeSelf)
        {
            ScrollToBottom();
        }
    }

    // ターンラベルの追加
    public void AddTurnLabel(int turn)
    {
        if (content == null) return;

        // 専用プレハブを使用（なければ通常プレハブで代用）
        GameObject prefabToUse = turnLogPrefab != null ? turnLogPrefab : logTextPrefab;
        if (prefabToUse == null) return;

        GameObject obj = Instantiate(prefabToUse, content);
        
        // 子要素も含めて探す
        TextMeshProUGUI txt = obj.GetComponentInChildren<TextMeshProUGUI>();
        
        if (txt)
        {
            txt.text = $"--- {turn} ターン目 ---";
            
            // 通常プレハブで代用している場合のみ、色や配置を強制変更
            if (prefabToUse == logTextPrefab)
            {
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.yellow;
                // 念のためスケールもリセット
                obj.transform.localScale = Vector3.one;
                txt.transform.localScale = Vector3.one;
            }
        }

        if (logPanel != null && logPanel.activeSelf)
        {
            ScrollToBottom();
        }
    }

    // 最下部へスクロール
    void ScrollToBottom()
    {
        if (scrollRect != null)
        {
            // レイアウト再構築を待ってからスクロール位置をセット
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}