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
            SetStatus("Please enter a Room ID.", Color.red);
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
        SetStatus("Connecting to Random Match...", Color.white);
        
        // Ensure NetworkConnectionManager exists
        if (NetworkConnectionManager.instance == null)
        {
            SetStatus("Network Manager missing!", Color.red);
            return;
        }

        // Connect to Random Lobby
        await NetworkConnectionManager.instance.StartRandomMatch();
    }

    async void StartRoomProcess(GameMode mode, string roomName)
    {
        SetStatus($"{(mode == GameMode.Host ? "Creating" : "Joining")} Room...", Color.white);

        if (NetworkConnectionManager.instance == null)
        {
            SetStatus("Network Manager missing!", Color.red);
            return;
        }

        if (mode == GameMode.Host)
        {
            // Generate a random 4-digit Room ID
            string newRoomId = Random.Range(1000, 9999).ToString();
            await NetworkConnectionManager.instance.StartSharedSession(newRoomId, "RoomScene");
        }
        else
        {
            // Join existing
            await NetworkConnectionManager.instance.StartSharedSession(roomName, "RoomScene");
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
