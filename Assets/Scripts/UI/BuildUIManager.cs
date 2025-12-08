using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BuildUIManager : MonoBehaviour
{
    public CanvasGroup handAreaCanvasGroup;

    [Header("パネル全体")]
    public GameObject panelRoot; // パネルの親オブジェクト

    [Header("右上：ビルドリスト")]
    public Transform listContainer; // リストボタンを並べる親
    public GameObject buildListButtonPrefab; // リスト用ボタンのプレハブ

    [Header("右下/右半分：詳細エリア")]
    public GameObject detailArea;
    public Image detailIcon;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public TextMeshProUGUI detailCost;
    public TextMeshProUGUI detailDuration;
    public Button buildButton; // 「ビルド」ボタン
    public Button closeButton;

    [Header("左下：ステータスエリア")]
    public TextMeshProUGUI statusText; // 現在の建築状況やクールタイムを表示

    private List<BuildData> currentBuildList;
    private int selectedIndex = -1;
    private bool isPlayerTarget; // 自分を見ているか、敵を見ているか

    void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseMenu);
        }
    }
    public void OpenMenu(bool isPlayer)
    {
        isPlayerTarget = isPlayer;
        panelRoot.SetActive(true);
        
        // ★追加：敵を見ているときは「ビルドボタン」を隠す（または無効化）
        if (buildButton != null)
        {
            buildButton.gameObject.SetActive(isPlayer);
        }

        UpdateBuildList();
        UpdateStatus();

        detailArea.SetActive(true);
        ClearDetail();
        if (handAreaCanvasGroup != null) handAreaCanvasGroup.blocksRaycasts = false;
    }

    void ClearDetail()
    {
        if (detailName != null) detailName.text = "";
        if (detailDesc != null) detailDesc.text = ""; // "ビルドを選択してください" と入れても親切です
        if (detailCost != null) detailCost.text = "";
        if (detailDuration != null) detailDuration.text = "";

        if (detailIcon != null)
        {
            detailIcon.sprite = null;
            detailIcon.color = Color.clear; // 透明にして見えなくする
        }

        if (buildButton != null)
        {
            buildButton.interactable = false; // ボタンは押せないように
            buildButton.onClick.RemoveAllListeners();
        }
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
        
        // 手札を操作可能に戻す
        if (handAreaCanvasGroup != null) handAreaCanvasGroup.blocksRaycasts = true;
    }

    void UpdateBuildList()
    {
        // 既存のリストをクリア
        foreach (Transform child in listContainer) Destroy(child.gameObject);

        // ★修正：リストの取得ロジックを整理
        if (isPlayerTarget)
        {
            currentBuildList = GameManager.instance.playerLoadoutBuilds;
        }
        else
        {
            currentBuildList = GameManager.instance.enemyLoadoutBuilds; 
        }

        for (int i = 0; i < currentBuildList.Count; i++)
        {
            int index = i;
            BuildData data = currentBuildList[i];
            
            GameObject btnObj = Instantiate(buildListButtonPrefab, listContainer);
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = data.buildName;

            btnObj.GetComponent<Button>().onClick.AddListener(() => OnSelectBuild(index));
        }
    }

    void OnSelectBuild(int index)
    {
        selectedIndex = index;
        BuildData data = currentBuildList[index];

        // アイコン表示などの処理（そのまま）
        if (detailIcon != null)
        {
            if (data.icon != null)
            {
                detailIcon.sprite = data.icon;
                detailIcon.color = Color.white;
            }
            else
            {
                detailIcon.color = Color.clear;
            }
        }

        // テキスト更新（そのまま）
        if (detailName != null) detailName.text = data.buildName;
        if (detailDesc != null) detailDesc.text = data.description;
        if (detailCost != null) detailCost.text = "Cost: " + data.cost;
        if (detailDuration != null) detailDuration.text = "Life: " + data.duration + " turns";

        // ★修正：ボタンの制御を厳格化
        if (buildButton != null)
        {
            // まずボタンのリスナーを全削除
            buildButton.onClick.RemoveAllListeners();

            if (isPlayerTarget)
            {
                // 自分を見ている場合のみ、建築判定を行う
                bool canBuild = GameManager.instance.isPlayerTurn 
                                && GameManager.instance.playerBuildCooldown == 0
                                && !GameManager.instance.HasActiveBuild(true)
                                && GameManager.instance.currentMana >= data.cost;

                buildButton.gameObject.SetActive(true); // ボタンを表示
                buildButton.interactable = canBuild;    // 条件を満たせば押せる

                if (canBuild)
                {
                    buildButton.onClick.AddListener(() => 
                    {
                        GameManager.instance.BuildConstruction(index);
                        CloseMenu();
                    });
                }
            }
            else
            {
                // 敵を見ている場合は、ボタンを非表示かつ無効にする
                buildButton.gameObject.SetActive(false);
                buildButton.interactable = false;
            }
        }   
    }

    void UpdateStatus()
    {
        // 左下のステータス表示
        string message = "";

        // 現在建築中のものがあるか？
        ActiveBuild current = GameManager.instance.GetActiveBuild(isPlayerTarget);
        int cooldown = isPlayerTarget ? GameManager.instance.playerBuildCooldown : GameManager.instance.enemyBuildCooldown;

        if (current != null)
        {
            string state = current.isUnderConstruction ? "建築中..." : "稼働中";
            message = $"【現在のビルド】\n{current.data.buildName}\n状態: {state}\n残り寿命: {current.remainingTurns}";
        }
        else
        {
            if (cooldown > 0)
            {
                message = $"【準備中】\n次まで: {cooldown} ターン";
            }
            else
            {
                message = "【建築可能】\nビルドを選択してください";
            }
        }

        statusText.text = message;
    }
}