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
    
    [Header("Random Match UI")]
    public TextMeshProUGUI randomMatchStatusText;

    [Header("Player 1 (Host) UI")]
    public TextMeshProUGUI p1NameText;
    public TextMeshProUGUI p1StatusText;
    public Button p1SelectDeckButton;
    public Image p1LeaderImage;
    public GameObject p1Panel; // New field

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
    
    // Auto-Ready if Random Match
    private bool autoReadyTriggered = false;

    private void Start()
    {
        // Setup UI Listeners
        if(exitButton) exitButton.onClick.AddListener(OnClickExitRoom);
        
        if(p1SelectDeckButton) p1SelectDeckButton.onClick.AddListener(() => OnClickSelectDeck(true));
        
        if(p2SelectDeckButton) p2SelectDeckButton.onClick.AddListener(() => OnClickSelectDeck(false));
        
        // Start Button defaults to Start Game, but we might override it in Spawned or Update
        if(startButton) startButton.onClick.AddListener(OnClickStartGame);
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
        
        // Check if Random Match to override Start Button behavior immediately
        if (Runner.SessionInfo.Name.StartsWith("Random_"))
        {
            if (startButton) 
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnClickExitRoom); // Cancel Match
                startButton.GetComponentInChildren<TextMeshProUGUI>().text = "キャンセル";
                startButton.GetComponent<Image>().color = new Color(1.0f, 0.0f, 0.0f); // Red (FF0000)
            }
        }

        UpdateUI();
    }

    private bool _isStarting = false;

    public override void FixedUpdateNetwork()
    {
        // Update UI if data changed (Simple polling for now, or use ChangeDetector)
        UpdateUI();

        // [Random Match] Auto-Start Logic
        if (Object.HasStateAuthority && Runner.SessionInfo.Name.StartsWith("Random_"))
        {
             // If P1 and P2 are present and Ready, Start Game
             // Prevent multiple calls
             if (!_isStarting && Runner.ActivePlayers.Count() >= 2 && P1Ready && P2Ready)
             {
                 _isStarting = true;
                 Debug.Log("[RoomManager] Random Match Ready. Starting Game...");
                 // Disable Open status to prevent others joining?
                 Runner.SessionInfo.IsOpen = false;
                 Runner.SessionInfo.IsVisible = false;
                 Runner.LoadScene("SampleScene");
             }
        }
    }
    
    private void Update()
    {
        if (!autoReadyTriggered && Runner != null && Runner.IsRunning && Runner.SessionInfo.Name.StartsWith("Random_"))
        {
             // Only if we are spawned
             if (Object != null && Object.IsValid)
             {
                 autoReadyTriggered = true;
                 RPC_SetReadyState(true, "Player");
             }
        }
    }

    void UpdateUI()
    {
        // Disable UI if not in RoomScene (e.g. game started)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "RoomScene")
        {
            if (roomIdText && roomIdText.transform.parent.gameObject.activeSelf) 
                roomIdText.transform.parent.gameObject.SetActive(false); 
             if (randomMatchStatusText) randomMatchStatusText.gameObject.SetActive(false);
            return; 
        }

        bool isRandomMatch = Runner.SessionInfo.Name.StartsWith("Random_");

        if (isRandomMatch)
        {
            // --- Random Match UI ---
            // Hide Room ID Panel
             if (roomIdText && roomIdText.transform.parent.gameObject.activeSelf) 
                roomIdText.transform.parent.gameObject.SetActive(false);

            // Hide P1/P2 Panels (Status shown in Center)
            if (p1Panel) p1Panel.SetActive(false);
            if (p2Panel) p2Panel.SetActive(false);

            // Hide specific elements if panel isn't set, as a fallback (or just relying on Panel is better)
            if (p1NameText) p1NameText.gameObject.SetActive(false);
            if (p1StatusText) p1StatusText.gameObject.SetActive(false);
            if (p1LeaderImage) p1LeaderImage.gameObject.SetActive(false);
            if (p1SelectDeckButton) p1SelectDeckButton.gameObject.SetActive(false);
            
            // Note: P1 Panel might be static UI. We can try to hide it by finding "Player1Info" or similar?
            // For now, let's hide the specific components we have refs to.
            if (p1NameText) p1NameText.gameObject.SetActive(false);
            if (p1StatusText) p1StatusText.gameObject.SetActive(false);
            if (p1LeaderImage) p1LeaderImage.gameObject.SetActive(false);
            if (p1SelectDeckButton) p1SelectDeckButton.gameObject.SetActive(false);
            
            // Hide Exit Button (Start button acts as Cancel)
            if (exitButton) exitButton.gameObject.SetActive(false);
            
            // Show Status Text
            if (randomMatchStatusText)
            {
                randomMatchStatusText.gameObject.SetActive(true);
                int playerCount = Runner.ActivePlayers.Count();
                
                if (playerCount < 2)
                {
                    randomMatchStatusText.text = "マッチング待機中...";
                }
                else if (playerCount >= 2)
                {
                    // Check Ready
                    if (P1Ready && P2Ready) randomMatchStatusText.text = "対戦を開始します...";
                    else randomMatchStatusText.text = "マッチングに成功しました";
                }
            }
            
            // Start Button (Cancel) Always Active
            if(startButton)
            {
                 startButton.gameObject.SetActive(true);
                 startButton.interactable = true;
            }
        }
        else
        {
            // --- Normal Room Match UI ---
             if (roomIdText && !roomIdText.transform.parent.gameObject.activeSelf) 
                roomIdText.transform.parent.gameObject.SetActive(true);
             if (randomMatchStatusText) randomMatchStatusText.gameObject.SetActive(false);
            
             if(exitButton) exitButton.gameObject.SetActive(true);

            if(roomIdText) roomIdText.text = $"Room ID: {RoomIdDisplay}";
            
            // Player 1 UI
            if (p1NameText) { p1NameText.gameObject.SetActive(true); p1NameText.text = P1Name.ToString(); }
            if (p1StatusText) { p1StatusText.gameObject.SetActive(true); p1StatusText.text = P1Ready ? "<color=green>READY</color>" : "Selecting..."; }
            if (p1LeaderImage) p1LeaderImage.gameObject.SetActive(true);
            if (p1SelectDeckButton) p1SelectDeckButton.gameObject.SetActive(true);
            
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
                startButton.interactable = ((P1Ready && P2Ready) || (playerCount == 1 && P1Ready)); 
                
                if (playerCount < 2) startButton.interactable = false;
            }
    
            // Button Interactivity (Only enable MY button)
            bool amIP1 = Runner.LocalPlayer == Runner.ActivePlayers.OrderBy(p => p.PlayerId).FirstOrDefault();
            if(p1SelectDeckButton) p1SelectDeckButton.interactable = amIP1;
            if(p2SelectDeckButton) p2SelectDeckButton.interactable = !amIP1;
        }
    }
    
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
                pagedDeckManager.OnDeckSelected = OnDeckSelected;
            }
            else
            {
                Debug.LogError("PagedDeckManager not found on DeckSelectPopup!");
            }
        }
    }

    public void OnDeckSelected(DeckData deck)
    {
        if(deckSelectPopup) deckSelectPopup.SetActive(false);
        
        // Set Ready and Name
        RPC_SetReadyState(true, "Player"); // We can pass real name later
        
        // Persist Deck Selection
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
        }
        else
        {
            // Info Source is P2
            P2Ready = ready;
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
