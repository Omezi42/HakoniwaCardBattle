using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class RoomManager : NetworkBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI roomIdText;
    public Button startButton;
    public Button exitButton;
    public GameObject deckSelectPopup;
    public PagedDeckManager pagedDeckManager; // Re-use existing deck selector
    
    [Header("Player 1 (Host) UI")]
    public TextMeshProUGUI p1NameText;
    public TextMeshProUGUI p1StatusText;
    public Button p1SelectDeckButton;
    public Image p1LeaderImage;

    [Header("Player 2 (Client) UI")]
    public TextMeshProUGUI p2NameText;
    public TextMeshProUGUI p2StatusText; // "Waiting..." or "Ready!"
    public Button p2SelectDeckButton;
    public Image p2LeaderImage;
    public GameObject p2Panel; // Hide if no player

    // Networked Properties to Sync State
    [Networked] public NetworkBool P1Ready { get; set; }
    [Networked] public NetworkBool P2Ready { get; set; }
    [Networked] public NetworkString<_16> RoomIdDisplay { get; set; }
    
    // Simple way to sync names/decks - in a real app use a NetworkDictionary or NetworkObject per player
    [Networked] public NetworkString<_32> P1Name { get; set; }
    [Networked] public NetworkString<_32> P2Name { get; set; }
    
    private void Start()
    {
        // Setup UI Listeners
        if(startButton) startButton.onClick.AddListener(OnClickStartGame);
        if(exitButton) exitButton.onClick.AddListener(OnClickExitRoom);
        
        if(p1SelectDeckButton) p1SelectDeckButton.onClick.AddListener(() => OnClickSelectDeck(true));
        
        // P2 button is usually disabled for local player unless we strictly separate UI
        // For simplicity, local player uses the button corresponding to their slot? 
        // Actually easier to have one "My Deck" button, but the design asked for "Next to name".
        // Let's assume buttons are ONLY clickable if they belong to LocalPlayer.
        
        if(p2SelectDeckButton) p2SelectDeckButton.onClick.AddListener(() => OnClickSelectDeck(false));
    }

    public override void Spawned()
    {
        // Initialize State
        if (Object.HasStateAuthority)
        {
            RoomIdDisplay = Runner.SessionInfo.Name;
            P1Name = "Player 1"; // Default or fetch from Local Settings
            P1Ready = false;
            P2Ready = false;
        }
        
        UpdateUI();
    }

    public override void FixedUpdateNetwork()
    {
        // Update UI if data changed (Simple polling for now, or use ChangeDetector)
        UpdateUI();
    }

    void UpdateUI()
    {
        // Disable UI if not in RoomScene (e.g. game started)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "RoomScene")
        {
            if (roomIdText && roomIdText.transform.parent.gameObject.activeSelf) 
                roomIdText.transform.parent.gameObject.SetActive(false); // Assume parent is root or close to it
            
            // Or just clear texts if not root
            return; 
        }

        if(roomIdText) roomIdText.text = $"Room ID: {RoomIdDisplay}";
        
        // Player 1 UI
        if(p1NameText) p1NameText.text = P1Name.ToString();
        if(p1StatusText) p1StatusText.text = P1Ready ? "<color=green>READY</color>" : "Selecting...";
        
        // Player 2 UI
        int playerCount = Runner.ActivePlayers.Count();
        if (p2Panel) p2Panel.SetActive(playerCount > 1);
        
        if (playerCount > 1)
        {
            if(p2NameText) p2NameText.text = string.IsNullOrEmpty(P2Name.ToString()) ? "Player 2" : P2Name.ToString();
            if(p2StatusText) p2StatusText.text = P2Ready ? "<color=green>READY</color>" : "Selecting...";
        }
        else
        {
            if(p2StatusText) p2StatusText.text = "Waiting for opponent...";
        }

        // Host Control: Start Button
        if (startButton)
        {
            bool amIHost = Runner.IsSharedModeMasterClient;
            startButton.gameObject.SetActive(amIHost);
            // Interactive only if both ready
            startButton.interactable = ((P1Ready && P2Ready) || (playerCount == 1 && P1Ready)); // Allow solo start for debug? Or strictly P1 && P2.
            
            // Strict rule:
            if (playerCount < 2) startButton.interactable = false;
        }

        // Button Interactivity (Only enable MY button)
        bool amIP1 = Runner.LocalPlayer == Runner.ActivePlayers.OrderBy(p => p.PlayerId).FirstOrDefault();
        if(p1SelectDeckButton) p1SelectDeckButton.interactable = amIP1;
        if(p2SelectDeckButton) p2SelectDeckButton.interactable = !amIP1;
    }

    // --- Actions ---

    public void OnClickSelectDeck(bool isP1Button)
    {
        // Check if I am actually that player
        bool amIP1 = Runner.LocalPlayer == Runner.ActivePlayers.OrderBy(p => p.PlayerId).FirstOrDefault();
        if (isP1Button != amIP1) return; // Prevention
        
        if(deckSelectPopup) 
        {
            deckSelectPopup.SetActive(true);

            // Auto-find if missing (Common setup error)
            if (pagedDeckManager == null) 
            {
                pagedDeckManager = deckSelectPopup.GetComponentInChildren<PagedDeckManager>();
            }

            if(pagedDeckManager)
            {
                pagedDeckManager.Initialize();
                // We need to hook into the Deck Selected event.
                // Currently MenuManager handles this. We might need to reuse or adapt.
                // For now, let's assume PagedDeckManager has a "OnDeckSelected" callback we can assign?
                // Or we update PagedDeckManager to call a delegate.
                pagedDeckManager.OnDeckSelected = OnDeckSelected;
            }
            else
            {
                Debug.LogError("PagedDeckManager not found on DeckSelectPopup!");
            }
        }
    }

    // Callback from PagedDeckManager
    public void OnDeckSelected(DeckData deck)
    {
        if(deckSelectPopup) deckSelectPopup.SetActive(false);
        
        // Set Ready and Name
        RPC_SetReadyState(true, "Player"); // We can pass real name later
        
        // Persist Deck Selection (Save to PlayerData so GameManager can load it in next scene)
        PlayerDataManager.instance.playerData.currentDeckIndex = PlayerDataManager.instance.playerData.decks.IndexOf(deck);
        PlayerDataManager.instance.Save();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReadyState(bool ready, string name, RpcInfo info = default)
    {
        // HOST updates the Networked Properties
        if (Runner.ActivePlayers.OrderBy(p => p.PlayerId).FirstOrDefault() == info.Source)
        {
            // Info Source is P1
            P1Ready = ready;
            // P1Name = name; 
        }
        else
        {
            // Info Source is P2
            P2Ready = ready;
            // P2Name = name;
        }
    }

    public void OnClickStartGame()
    {
        if (!Object.HasStateAuthority) return;
        
        // Close Room access?
        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;
        
        // Load Game Scene
        Runner.LoadScene("SampleScene");
    }

    public void OnClickExitRoom()
    {
        // Despawn/Shutdown and return to Menu
        if(Runner != null) Runner.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
    }
}
