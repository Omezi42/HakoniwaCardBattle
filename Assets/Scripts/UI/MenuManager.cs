using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    [Header("バトル準備ポップアップ")]
    public GameObject deckSelectPopup;
    // public TMP_Dropdown deckDropdown; // 廃止
    public PagedDeckManager pagedDeckManager; // 追加
    public Button startBattleButton;

    [Header("モード選択ポップアップ")]
    public GameObject modeSelectPopup;
    public GameObject onlineMenuPopup; // New Online Menu
    [Header("難易度選択ポップアップ")]
    public GameObject difficultySelectPopup;

    void Start()
    {
        // --- 診断用ログ ---
        if (deckSelectPopup == null) Debug.LogError("【緊急確認】DeckSelectPopup が空です！Inspectorで割り当ててください！");
        // else Debug.Log("DeckSelectPopup: OK");

        if (pagedDeckManager == null) Debug.LogError("【緊急確認】PagedDeckManager が空です！Inspectorで割り当ててください！");
        // else Debug.Log("PagedDeckManager: OK");

        if (startBattleButton == null) Debug.LogError("【緊急確認】StartBattleButton が空です！Inspectorで割り当ててください！");
        // else Debug.Log("StartBattleButton: OK");
        // ------------------

        if (deckSelectPopup != null) deckSelectPopup.SetActive(false);
        if (modeSelectPopup != null) modeSelectPopup.SetActive(false);
        if (onlineMenuPopup != null) onlineMenuPopup.SetActive(false);
        if (difficultySelectPopup != null) difficultySelectPopup.SetActive(false);
        
        // PlayFabManagerの自動生成
        if (PlayFabManager.instance == null)
        {
            GameObject obj = new GameObject("PlayFabManager");
            obj.AddComponent<PlayFabManager>();
            Debug.Log("PlayFabManager Created Automatically.");
        }
        
        // PlayerDataManagerの自動生成 (Safety Net)
        if (PlayerDataManager.instance == null)
        {
            GameObject obj = new GameObject("PlayerDataManager");
            obj.AddComponent<PlayerDataManager>();
            Debug.Log("PlayerDataManager Created Automatically.");
        }
    }

    // 「バトル」ボタン -> モード選択
    public void OnClickBattle()
    {
        if (modeSelectPopup != null) modeSelectPopup.SetActive(true);
        else 
        {
             // Fallback: Default to CPU(Normal) -> DeckSelect if UI missing
             PlayerDataManager.instance.cpuDifficulty = 0;
             OnClickSelectDeck(); 
        }
    }

    // モード選択: CPU
    public void OnClickModeCPU()
    {
        if (modeSelectPopup != null) modeSelectPopup.SetActive(false);
        if (difficultySelectPopup != null) difficultySelectPopup.SetActive(true);
        else
        {
             // Fallback
             PlayerDataManager.instance.cpuDifficulty = 0;
             OnClickSelectDeck();
        }
    }
    
    // モード選択: オンライン
    public void OnClickModeOnline()
    {
        if (modeSelectPopup != null) modeSelectPopup.SetActive(false);
        
        if (onlineMenuPopup != null)
        {
            onlineMenuPopup.SetActive(true);
        }
        else
        {
             Debug.LogError("OnlineMenuPopup is not assigned in MenuManager!");
             // Fallback to legacy behavior for safety
             OnClickSelectDeck();
        }
    }

    // 難易度選択: Easy
    public void OnClickDifficultyEasy()
    {
        PlayerDataManager.instance.cpuDifficulty = 0;
        ProceedToDeckSelect();
    }
    // 難易度選択: Normal
    public void OnClickDifficultyNormal()
    {
        PlayerDataManager.instance.cpuDifficulty = 1;
        ProceedToDeckSelect();
    }
    // 難易度選択: Hard
    public void OnClickDifficultyHard()
    {
        PlayerDataManager.instance.cpuDifficulty = 2;
        ProceedToDeckSelect();
    }

    void ProceedToDeckSelect()
    {
        if (difficultySelectPopup != null) difficultySelectPopup.SetActive(false);
        OnClickSelectDeck();
    }

    // 以前のOnClickBattle相当
    public void OnClickSelectDeck()
    {
        deckSelectPopup.SetActive(true);
        SetupDeckManager();
        // Offline Mode: Ensure Start Button is visible
        if (startBattleButton) startBattleButton.gameObject.SetActive(true);
    }
    
    public void SetStartButtonActive(bool active)
    {
        if (startBattleButton) startBattleButton.gameObject.SetActive(active);
    }

    public void OnClickEditDeck()
    {
        SceneManager.LoadScene("DeckEditScene");
    }

    public void OnClickCardList()
    {
        SceneManager.LoadScene("CardListScene");
    }

    public void OnClickBackTitle()
    {
        SceneManager.LoadScene("TitleScene");
    }

    void SetupDeckManager()
    {
        if (pagedDeckManager != null)
        {
            pagedDeckManager.Initialize();
        }
    }

    public void OnClickStartBattle()
    {
        // ★修正：もしデッキが0個なら、ここで作成してからバトルへ
        if (PlayerDataManager.instance.playerData.decks.Count == 0)
        {
            PlayerDataManager.instance.CreateStarterDeck();
            // 作成されたデッキ（0番）を選択状態にする
            PlayerDataManager.instance.playerData.currentDeckIndex = 0;
        }
        
        // PagedDeckManager側でクリック時に currentDeckIndex は更新されている前提
        // そのため、ここでは保存してシーン遷移するだけでOK

        PlayerDataManager.instance.Save(); 
        SceneManager.LoadScene("SampleScene"); 
    }

    public void OnClosePopup()
    {
        if (deckSelectPopup != null) deckSelectPopup.SetActive(false);
        if (modeSelectPopup != null) modeSelectPopup.SetActive(false);
        if (onlineMenuPopup != null) onlineMenuPopup.SetActive(false);
        if (difficultySelectPopup != null) difficultySelectPopup.SetActive(false);
    }
}
