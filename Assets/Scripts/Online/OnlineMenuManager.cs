using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;

public class OnlineMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject deckSelectPopup;
    public PagedDeckManager pagedDeckManager;
    
    [Header("Input")]
    public TMP_InputField roomIdInputField;
    
    [Header("Status")]
    public TextMeshProUGUI statusText;

    private bool isRandomMatch = false;

    // Called by Button: "Random Match"
    public void OnClickRandomMatch()
    {
        isRandomMatch = true;
        OpenDeckSelect();
    }

    // Called by Button: "Create Room"
    public void OnClickCreateRoom()
    {
        // For Private Rooms, we don't need Deck Select immediately?
        // Actually, design says "Room Scene" has deck select.
        // So we just start the session and go to Room Scene.
        StartRoomProcess(GameMode.Host, null);
    }

    // Called by Button: "Join Room"
    public void OnClickJoinRoom()
    {
        string roomId = roomIdInputField.text.Trim();
        if (string.IsNullOrEmpty(roomId))
        {
            SetStatus("ルームIDを入力してください。", Color.red);
            return;
        }

        StartRoomProcess(GameMode.Client, roomId);
    }

    void OpenDeckSelect()
    {
        if(deckSelectPopup) deckSelectPopup.SetActive(true);
        if(pagedDeckManager)
        {
            pagedDeckManager.Initialize();
            pagedDeckManager.OnDeckSelected = OnDeckSelected;
        }

        // Hide Offline Start Button to prevent confusion
        // Ensure we try both direct ref (if I add it to Inspector later) or Find
        MenuManager menu = FindObjectOfType<MenuManager>();
        if (menu)
        {
            menu.SetStartButtonActive(false);
        }
    }

    void OnDeckSelected(DeckData deck)
    {
        if(deckSelectPopup) deckSelectPopup.SetActive(false);
        
        // Save selected deck index
        PlayerDataManager.instance.playerData.currentDeckIndex = PlayerDataManager.instance.playerData.decks.IndexOf(deck);
        PlayerDataManager.instance.Save();

        if (isRandomMatch)
        {
            StartRandomMatchProcess();
        }
    }

    async void StartRandomMatchProcess()
    {
        SetStatus("ランダムマッチに接続中...", Color.white);
        
        // Ensure NetworkConnectionManager exists
        if (NetworkConnectionManager.instance == null)
        {
            SetStatus("ネットワーク接続エラー", Color.red);
            return;
        }

        // Connect to Random Lobby
        await NetworkConnectionManager.instance.StartRandomMatch();
    }

    async void StartRoomProcess(GameMode mode, string roomName)
    {
        SetStatus($"{(mode == GameMode.Host ? "ルームを作成中" : "ルームに参加中")}...", Color.white);

        if (NetworkConnectionManager.instance == null)
        {
            SetStatus("ネットワーク接続エラー", Color.red);
            return;
        }

        if (mode == GameMode.Host)
        {
            // Generate a random 4-digit Room ID
            string newRoomId = Random.Range(1000, 9999).ToString();
            // Create: joinOnly = false
            await NetworkConnectionManager.instance.StartSharedSession(newRoomId, "RoomScene", false);
        }
        else
        {
            // Join: joinOnly = true
            bool success = await NetworkConnectionManager.instance.StartSharedSession(roomName, "RoomScene", true);
            if (!success)
            {
                SetStatus("ルームが見つかりません。", Color.red);
            }
        }
    }

    void SetStatus(string msg, Color c)
    {
        if(statusText) 
        {
            statusText.text = msg;
            statusText.color = c;
        }
        Debug.Log($"[OnlineMenu] {msg}");
    }
}
