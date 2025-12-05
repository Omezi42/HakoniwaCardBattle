using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    [Header("バトル準備ポップアップ")]
    public GameObject deckSelectPopup;
    public TMP_Dropdown deckDropdown; 
    public Button startBattleButton;

    void Start()
    {
        // --- 診断用ログ ---
        if (deckSelectPopup == null) Debug.LogError("【緊急確認】DeckSelectPopup が空です！Inspectorで割り当ててください！");
        else Debug.Log("DeckSelectPopup: OK");

        if (deckDropdown == null) Debug.LogError("【緊急確認】DeckDropdown が空です！Inspectorで割り当ててください！");
        else Debug.Log("DeckDropdown: OK");

        if (startBattleButton == null) Debug.LogError("【緊急確認】StartBattleButton が空です！Inspectorで割り当ててください！");
        else Debug.Log("StartBattleButton: OK");
        // ------------------

        if (deckSelectPopup != null) deckSelectPopup.SetActive(false);
    }

    public void OnClickBattle()
    {
        deckSelectPopup.SetActive(true);
        SetupDeckDropdown();
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

    void SetupDeckDropdown()
    {
        deckDropdown.ClearOptions();
        List<string> options = new List<string>();
        var decks = PlayerDataManager.instance.playerData.decks;

        // ★修正：デッキが0個の場合の処理
        if (decks.Count == 0)
        {
            // デッキがない場合は「おまかせデッキ」という選択肢を出す
            options.Add("おまかせデッキ（自動作成）");
            
            // ボタンは有効のままにする
            startBattleButton.interactable = true;
        }
        else
        {
            foreach (var deck in decks)
            {
                options.Add($"{deck.deckName} ({deck.deckJob})");
            }
            startBattleButton.interactable = true;
        }

        deckDropdown.AddOptions(options);
        
        // インデックスが範囲外なら0にする
        int currentIndex = PlayerDataManager.instance.playerData.currentDeckIndex;
        if (currentIndex >= options.Count) currentIndex = 0;
        
        deckDropdown.value = currentIndex;
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
        else
        {
            // 既存のデッキ選択処理
            PlayerDataManager.instance.playerData.currentDeckIndex = deckDropdown.value;
        }

        PlayerDataManager.instance.Save(); 
        SceneManager.LoadScene("SampleScene"); 
    }

    public void OnClosePopup()
    {
        deckSelectPopup.SetActive(false);
    }
}