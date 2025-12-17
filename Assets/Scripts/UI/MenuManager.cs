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

    void Start()
    {
        // --- 診断用ログ ---
        if (deckSelectPopup == null) Debug.LogError("【緊急確認】DeckSelectPopup が空です！Inspectorで割り当ててください！");
        else Debug.Log("DeckSelectPopup: OK");

        if (pagedDeckManager == null) Debug.LogError("【緊急確認】PagedDeckManager が空です！Inspectorで割り当ててください！");
        else Debug.Log("PagedDeckManager: OK");

        if (startBattleButton == null) Debug.LogError("【緊急確認】StartBattleButton が空です！Inspectorで割り当ててください！");
        else Debug.Log("StartBattleButton: OK");
        // ------------------

        if (deckSelectPopup != null) deckSelectPopup.SetActive(false);
    }

    public void OnClickBattle()
    {
        deckSelectPopup.SetActive(true);
        SetupDeckManager();
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
        deckSelectPopup.SetActive(false);
    }
}
