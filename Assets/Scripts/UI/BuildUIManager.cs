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

    // ★変更: BuildData -> CardData
    private List<CardData> currentBuildList; 
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
        if (detailDesc != null) detailDesc.text = ""; 
        if (detailCost != null) detailCost.text = "";
        if (detailDuration != null) detailDuration.text = "";

        if (detailIcon != null)
        {
            detailIcon.sprite = null;
            detailIcon.color = Color.clear; 
        }

        if (buildButton != null)
        {
            buildButton.interactable = false; 
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
        foreach (Transform child in listContainer) Destroy(child.gameObject);

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
            CardData data = currentBuildList[i]; // ★CardData
            
            GameObject btnObj = Instantiate(buildListButtonPrefab, listContainer);
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = data.cardName; // buildName -> cardName

            btnObj.GetComponent<Button>().onClick.AddListener(() => OnSelectBuild(index));
        }
    }

    void OnSelectBuild(int index)
    {
        selectedIndex = index;
        CardData data = currentBuildList[index]; // ★CardData

        if (detailIcon != null)
        {
            if (data.cardIcon != null) // icon -> cardIcon
            {
                detailIcon.sprite = data.cardIcon;
                detailIcon.color = Color.white;
            }
            else
            {
                detailIcon.color = Color.clear;
            }
        }

        // 変数名の変更に対応
        if (detailName != null) detailName.text = data.cardName; // buildName -> cardName
        if (detailDesc != null) detailDesc.text = data.description;
        if (detailCost != null) detailCost.text = "Cost: " + data.cost;
        if (detailDuration != null) detailDuration.text = "Life: " + data.duration + " turns";

        // ★修正：ボタンの制御（ロジックは以前と同じだが型がCardData）
        if (buildButton != null)
        {
            buildButton.onClick.RemoveAllListeners();

            if (isPlayerTarget)
            {
                bool canBuild = GameManager.instance.isPlayerTurn 
                                && GameManager.instance.playerBuildCooldown == 0
                                && !GameManager.instance.HasActiveBuild(true)
                                && GameManager.instance.currentMana >= data.cost;

                buildButton.gameObject.SetActive(true); 
                buildButton.interactable = canBuild;    

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
                buildButton.gameObject.SetActive(false);
                buildButton.interactable = false;
            }
        }   
    }

    void UpdateStatus()
    {
        string message = "";
        ActiveBuild current = GameManager.instance.GetActiveBuild(isPlayerTarget);
        int cooldown = isPlayerTarget ? GameManager.instance.playerBuildCooldown : GameManager.instance.enemyBuildCooldown;

        if (current != null)
        {
            string state = current.isUnderConstruction ? "建築中..." : "稼働中";
            // buildName -> cardName
            message = $"【現在のビルド】\n{current.data.cardName}\n状態: {state}\n残り寿命: {current.remainingTurns}";
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