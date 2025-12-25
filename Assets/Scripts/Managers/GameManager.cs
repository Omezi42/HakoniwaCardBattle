using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq; 
using Fusion; // [NEW] 

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UIパーツ")]
    public Transform handArea;
    // public Transform playerManaText; // Removed
    public Transform endTurnButton;
    public Transform playerDeckIsland;
    public GameObject playerDeckVisual; 
    public GameObject playerGraveVisual;

    [Header("情報表示")]
    public TextMeshProUGUI playerDeckCountText;
    public TextMeshProUGUI enemyDeckCountText;
    public TextMeshProUGUI enemyHandCountText;  // ★追加
    public TextMeshProUGUI playerGraveCountText; // ★追加
    public TextMeshProUGUI enemyGraveCountText;  // ★追加

    [Header("Result UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public GameObject retryButton; // For Offline
    public GameObject returnToRoomButton; // For Online

    [Header("カード拡大表示")]
    public GameObject enlargedCardPanel; 
    public CardView enlargedCardView;    

    [Header("ゲームデータ")]
    public int maxMana = 0;
    public int currentMana = 0;
    public bool isPlayerTurn = true;
    private int fatigueCount = 0; 

    public int playerGraveyardCount = 0;
    public int enemyGraveyardCount = 0;

    [Header("デッキ・手札")]
    [Header("デッキ・手札")]
    public List<CardData> mainDeck = new List<CardData>();
    public List<CardData> hand = new List<CardData>(); // Fixed missing 'hand'


    [Header("敵のステータス")]
    public Transform enemyBoard;
    public Transform playerLeader;
    public GameObject enemyLeaderObj; // Fixed missing 'enemyLeaderObj'
    public Transform enemyManaText;
    public Transform enemyDeckIsland; 
    public GameObject enemyDeckVisual;
    public GameObject enemyGraveVisual;
    public int enemyMaxMana = 0;
    public int enemyCurrentMana = 0;
    private int enemyFatigueCount = 0;
    
    [Header("敵AIデッキ・ビルド設定")]
    public List<CardData> enemyDeckDefinition; 
    public List<CardData> enemyBuildDefinition; 

    private List<CardData> enemyMainDeck = new List<CardData>();
    private int enemyDeckCount = 0; 
    
    // 手札リスト（敵の手札戻し用）
    public List<CardData> enemyHandData = new List<CardData>();

    [Header("Prefab")]
    public CardView cardPrefab;
    public GameObject unitPrefabForEnemy;
    public GameObject playerUnitPrefab;
    public Sprite manaCoinSprite; // Added for customized Mana Coin

    [Header("効果音(SE)")]
    public AudioClip seSummon;
    public AudioClip seAttack;
    public AudioClip seDamage;
    public AudioClip seWin;
    public AudioClip seLose;
    public AudioClip seDraw;
    private AudioSource audioSource;

    [Header("ビルドシステム")]
    public List<CardData> playerLoadoutBuilds;
    public List<CardData> enemyLoadoutBuilds;
    public List<ActiveBuild> activeBuilds = new List<ActiveBuild>();
    public BuildUIManager buildUIManager;
    public int playerBuildCooldown = 0;
    public int enemyBuildCooldown = 0;
    // Build Cooldowns (Dynamic)
    public int maxPlayerBuildCooldown = 5; 
    public int maxEnemyBuildCooldown = 4;
    // Removed const int COOLDOWN_PLAYER / ENEMY to allow dynamic setting


    [Header("演出")]
    public GameObject floatingTextPrefab;
    public Transform effectCanvasLayer;
    public TurnCutIn turnCutIn;
    public TargetArrow targetArrowPrefab;
    private TargetArrow currentArrow;
    public SimpleTooltip simpleTooltip;
    
    public Transform screenCenter; 

    [Header("マナUI")]
    public List<Image> playerManaCrystals; 
    public List<Image> enemyManaCrystals;
    public Sprite manaOnSprite;
    public Sprite manaOffSprite;

    [Header("リーダー画像")]
    public Sprite[] leaderIcons; 

    [Header("スペル詠唱")]
    public Transform spellCastCenter;
    public CardView currentCastingCard;
    public bool isTargetingMode = false;
    
    private Transform pendingSummonSlot; 
    private Vector3 arrowStartPos; 

    [Header("敵UI")]
    public Transform enemyHandArea;
    public GameObject cardBackPrefab;

    [Header("ビルド表示用")]
    public Transform playerBuildArea;
    public Transform enemyBuildArea;
    public GameObject buildIconPrefab;

    [Header("マリガン")]
    public MulliganManager mulliganManager;
    private List<CardData> tempHand = new List<CardData>();

    [Header("追加Prefab")]
    public GameObject deckCardPrefab; // ★追加

    [Header("ターゲット情報")]
    public int lastTargetX = -1; // ★追加

    private int turnCount = 0;
    private bool isGameEnded = false;
    
    // Check if Mulligan is still ongoing (Turn 0)
    public bool isMulliganActive => turnCount == 0 && !isGameEnded;

    #region Singleton & Lifecycle
    private void Awake() 
    { 
        if (instance == null) instance = this; 
    }

    void Start()
    {
        isGameEnded = false;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (playerUnitPrefab == null && unitPrefabForEnemy != null) playerUnitPrefab = unitPrefabForEnemy;

        maxMana = 0; currentMana = 0; enemyMaxMana = 0; enemyCurrentMana = 0;
        playerGraveyardCount = 0; 
        enemyGraveyardCount = 0;  
        
        if (enlargedCardPanel != null) enlargedCardPanel.SetActive(false);
        if (enlargedCardView != null) 
        {
            enlargedCardView.enableHoverScale = false;  
            enlargedCardView.enableHoverDetail = false; 
        }

        if (cardBackPrefab == null) Debug.LogError("[GameManager] cardBackPrefab is NOT assigned in Inspector!");
        else Debug.Log("[GameManager] cardBackPrefab is assigned.");

        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;

        UpdateManaUI();
        UpdateEnemyManaUI();
        SetupBoard(GameObject.Find("PlayerBoard").transform, false);
        SetupBoard(enemyBoard, true);
        SetupDeck();
        
        // オフライン時のみ敵デッキ構築を行う
        if (!isOnline)
        {
            SetupEnemyDeck();
        }
        
        UpdateBuildUI();
        SetupLeaderIcon();
        UpdateDeckGraveyardVisuals();

        if (targetArrowPrefab != null)
        {
            Transform canvas = handArea.parent; 
            currentArrow = Instantiate(targetArrowPrefab, canvas);
            currentArrow.gameObject.SetActive(false);
        }

        // Offline Randomization
        bool isFirst = true;
        if (!isOnline) 
        {
            isFirst = (Random.Range(0, 2) == 0);
        }

        StartCoroutine(WaitForNetworkAndStart(isOnline, isFirst));
        if (isOnline) StartCoroutine(ForceSyncOpponentStats()); // ★Force Polling Sync
    }

    System.Collections.IEnumerator ForceSyncOpponentStats()
    {
        while(true)
        {
            if (NetworkConnectionManager.instance != null && 
                NetworkConnectionManager.instance.Runner != null && 
                NetworkConnectionManager.instance.Runner.IsRunning)
            {
                 SyncNetworkInfo();
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    System.Collections.IEnumerator WaitForNetworkAndStart(bool isOnline, bool isFirst)
    {
        // Wait a frame to ensure NetworkRunner is ready if online
        // Online Mode: Wait until GameStateController is spawned
        GameStateController gameState = null;
        if (isOnline)
        {
            while (gameState == null)
            {
                gameState = FindObjectOfType<GameStateController>();
                if (gameState == null) yield return null;
            }
            Debug.Log("[GameManager] GameStateController found. Checking game status...");
        }

        if (isOnline)
        {
            // Submit Job ID
            // ... (rest of function)
            if (PlayerDataManager.instance != null && PlayerDataManager.instance.playerData.decks.Count > PlayerDataManager.instance.playerData.currentDeckIndex)
            {
               var currentDeck = PlayerDataManager.instance.playerData.decks[PlayerDataManager.instance.playerData.currentDeckIndex];
               int myJobId = (int)currentDeck.deckJob; 
               
               // Direct set if State Authority (Shared Mode Owner)
               var myPC = NetworkPlayerController.Get(NetworkConnectionManager.instance.Runner.LocalPlayer);
               if (myPC != null && myPC.Object.HasStateAuthority)
               {
                   myPC.JobId = myJobId;
                   Debug.Log($"[GameManager] Set JobID {myJobId} directly on NetworkPlayerController.");
               }
               else
               {
                   Debug.Log($"[GameManager] Sending JobID {myJobId} to GameStateController via RPC.");
                   gameState.RPC_SubmitJobId(myJobId);
               }
            }

            // Check if game already started
            if (gameState.IsGameStarted)
            {
                Debug.Log($"[GameManager] Game already started! FirstPlayer: {gameState.FirstPlayer}. Starting Mulligan manually.");
                bool amIFirst = (NetworkConnectionManager.instance.Runner.LocalPlayer == gameState.FirstPlayer);
                StartMulliganSequence(amIFirst ? 3 : 4, amIFirst);
            }
            else
            {
                Debug.Log("[GameManager] Online Mode: Waiting for GameStateController to start Mulligan...");
            }
        }
        else
        {
            // Offline Mode (Should usually handle above, but purely for logic splitting)
            StartMulliganSequence(isFirst ? 3 : 4, isFirst);
        }
    }



    public void SetEnemyLeaderIcon(int jobId)
    {
        Debug.Log($"[GameManager] SetEnemyLeaderIcon called with JobId: {jobId}");

        int index = jobId;
        if (leaderIcons != null && index >= 0 && index < leaderIcons.Length)
        {
             Leader leaderScript = null;
             
             // Try 1: Find "EnemyInfo" (Standard)
             GameObject enemyInfoObj = GameObject.Find("EnemyInfo");
             if (enemyInfoObj != null) 
             {
                 leaderScript = enemyInfoObj.GetComponent<Leader>();
                 if (leaderScript == null) leaderScript = enemyInfoObj.GetComponentInChildren<Leader>();
                 
                 // ★CRITICAL FIX: Cache it
                 this.enemyLeaderObj = enemyInfoObj;
             }

             // Try 2: If failed, try "EnemyLeader"
             if (leaderScript == null)
             {
                 GameObject enemyLeaderObj = GameObject.Find("EnemyLeader");
                 if (enemyLeaderObj != null) 
                 {
                     leaderScript = enemyLeaderObj.GetComponent<Leader>();
                     if (this.enemyLeaderObj == null) this.enemyLeaderObj = enemyLeaderObj;
                 }
             }

             // Apply
             if (leaderScript != null)
             {
                 Debug.Log($"[GameManager] Found Enemy Leader Script on {leaderScript.gameObject.name}. Setting Icon to index {index}.");
                 leaderScript.SetIcon(leaderIcons[index]);
             }
             else
             {
                 Debug.LogWarning("[GameManager] Enemy Leader Script NOT found! Searched 'EnemyInfo' and 'EnemyLeader'.");
             }
        }
        else
        {
             Debug.LogWarning($"[GameManager] Invalid JobId {jobId} or LeaderIcons array issue. Index: {index}, Length: {(leaderIcons==null?0:leaderIcons.Length)}");
        }
    }
    #endregion

    #region Mulligan
    private bool amIFirstPlayer = true; // Default true (offline)
    private bool _hasStartedMulligan = false; // [FIX] Prevent double trigger

    public void StartMulliganSequence(int drawCount = 3, bool isFirst = true)
    {
        if (_hasStartedMulligan) 
        {
            Debug.Log("[GameManager] StartMulliganSequence already started. Ignoring.");
            return;
        }
        _hasStartedMulligan = true;
        
        Debug.Log($"[GameManager] StartMulliganSequence Called. Draw: {drawCount}, First: {isFirst}");

        // マリガン中はターン終了ボタンを無効化
        if (endTurnButton != null) endTurnButton.GetComponent<Button>().interactable = false;

        amIFirstPlayer = isFirst;
        tempHand.Clear();
        for (int i = 0; i < drawCount; i++) 
        { 
            if (mainDeck.Count > 0) 
            { 
                tempHand.Add(mainDeck[0]); 
                mainDeck.RemoveAt(0); 
            } 
        }

        Debug.Log($"[GameManager] MulliganManager Reference: {mulliganManager}");
        if (mulliganManager != null) 
        {
            Debug.Log("[GameManager] Showing Mulligan UI.");
            mulliganManager.ShowMulligan(tempHand);
        }
        else 
        {
            Debug.LogWarning("[GameManager] MulliganManager is NULL! Starting game immediately.");
            StartGameAfterMulligan();
        }

        // Show Coin Toss Result AFTER Mulligan UI to ensure it is on top
        ShowCoinTossResult(isFirst);
    }

    void ShowCoinTossResult(bool isFirst)
    {
        Debug.Log($"[GameManager] ShowCoinTossResult called. isFirst: {isFirst}, turnCutIn: {turnCutIn}");
        if (turnCutIn != null)
        {
            turnCutIn.gameObject.SetActive(true);
            turnCutIn.transform.SetAsLastSibling(); // Ensure Coin Toss is on top of Mulligan
            turnCutIn.Show(isFirst ? "あなたは先攻です" : "あなたは後攻です", isFirst ? Color.cyan : Color.magenta);
            Invoke("HideCutIn", 2.0f);
        }
    }
    void HideCutIn() { if (turnCutIn != null) turnCutIn.gameObject.SetActive(false); }

    public void EndMulligan(List<bool> replaceFlags)
    {
        if (mulliganManager != null) mulliganManager.gameObject.SetActive(false);
        for (int i = 0; i < replaceFlags.Count; i++) { if (replaceFlags[i] && tempHand[i] != null) { mainDeck.Add(tempHand[i]); tempHand[i] = null; } }
        ShuffleDeck();
        for (int i = 0; i < tempHand.Count; i++) { if (tempHand[i] == null && mainDeck.Count > 0) { tempHand[i] = mainDeck[0]; mainDeck.RemoveAt(0); } }
        StartGameAfterMulligan();
    }
    #endregion

    #region Card Manipulation
    public void StartGameAfterMulligan()
    {
        Debug.Log("[GameManager] StartGameAfterMulligan Called. Instantiating Hand Cards...");
        foreach (var data in tempHand)
        {
            if (data == null) continue;
            CardView newCard = Instantiate(cardPrefab, handArea);
            newCard.SetCard(data);
            newCard.transform.localScale = Vector3.one;
            newCard.transform.localRotation = Quaternion.identity;
            newCard.ShowBack(false);
        }
        
        // Second Player Logic: Add Mana Coin
        if (!amIFirstPlayer)
        {
            AddManaCoinToHand();
            // Build Cooldown for Second Player is 4
            maxPlayerBuildCooldown = 4; 
            playerBuildCooldown = 0; // Initialize READY
        }
        else
        {
            // First Player: Cooldown 5
            maxPlayerBuildCooldown = 5;
            playerBuildCooldown = 0; // Initialize READY
        }

        UpdateHandState();
        UpdateDeckGraveyardVisuals();
        UpdateBuildUI(); // visual update for cooldown if needed? (Cooldown is internal usually, but good to refresh)

        // Mulligan終了後
        tempHand.Clear();

        // オフライン・オンライン共通: ボタンの有効化
        if (endTurnButton != null) endTurnButton.GetComponent<Button>().interactable = true;

        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        if (isOnline)
        {
            // オンライン: Mulligan完了を通知
             var gameState = FindObjectOfType<GameStateController>();
             if (gameState != null) gameState.RPC_FinishMulligan(gameState.Runner.LocalPlayer);
        }
        else
        {
            // オフライン: 即座にゲーム開始
            
            // ★ AI Difficulty Normal & Hard: Start with +1 Mana
            if (PlayerDataManager.instance != null && PlayerDataManager.instance.cpuDifficulty >= 1)
            {
                 enemyMaxMana = 1;
                 enemyCurrentMana = 1;
                 UpdateEnemyManaUI();
            }

            // Enemy Turn Order Logic (Offline)
            // If I am First (!amIFirstPlayer is false) -> Enemy is Second (4 cards + Coin)
            // If I am Second (!amIFirstPlayer is true) -> Enemy is First (3 cards)
            
            if (amIFirstPlayer)
            {
                // Player is First, Enemy is Second (4 cards + Coin)
                InitializeEnemyHand(4);
                
                // Add Mana Coin to Enemy
                CardData coin = ScriptableObject.CreateInstance<CardData>();
                coin.id = "ManaCoin";
                coin.cardName = "マナコイン";
                coin.cost = 0;
                coin.type = CardType.SPELL;
                coin.abilities = new List<CardAbility>();
                enemyHandData.Add(coin);
            }
            else
            {
                // Player is Second, Enemy is First (3 cards)
                InitializeEnemyHand(3);
                // Start Enemy Turn immediately
                StartEnemyTurn();
                // Return here to avoid StartPlayerTurn below
                return; 
            }

            bool isRunnerActive = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
            if (!isRunnerActive)
            {
                StartPlayerTurn();
            }
        else
        {
             // Online: GameStateController handles turn switching.
             // We just wait for OnTurnChanged event.
             Debug.Log("[GameManager] Online Mode: Waiting for GameStateController to trigger turn.");
        }
        }
    }

    void AddManaCoinToHand()
    {
        // Mana Coin CardData creation
        CardData coin = ScriptableObject.CreateInstance<CardData>();
        coin.id = "ManaCoin";
        coin.cardName = "マナコイン";
        coin.description = "このターンのみ、マナを+1する。";
        coin.cost = 0;
        coin.type = CardType.SPELL;
        
        CardAbility ability = new CardAbility();
        ability.effect = EffectType.GAIN_MANA;
        ability.value = 1;
        ability.trigger = EffectTrigger.SPELL_USE;
        ability.target = EffectTarget.SELF; 
        
        coin.abilities = new List<CardAbility> { ability };
        
        // Assign Sprite if available
        if (manaCoinSprite != null) coin.cardIcon = manaCoinSprite;
        
        // Add to Hand
        CardView newCard = Instantiate(cardPrefab, handArea);
        newCard.SetCard(coin);
        newCard.ShowBack(false);
    }

    // ★修正：手札に戻す処理（確実にカードを生成する）
    // ★修正：手札に戻す処理（アニメーション付き）
    public void ReturnCardToHand(CardData card, bool isPlayer, Vector3? startWorldPos = null)
    {
        if (card == null) return;
        StartCoroutine(ReturnToHandSequence(card, isPlayer, startWorldPos));
    }

    System.Collections.IEnumerator ReturnToHandSequence(CardData card, bool isPlayer, Vector3? startWorldPos)
    {
        // 開始位置：指定がなければ画面中央付近とかにする
        Vector3 startPos = startWorldPos ?? (screenCenter ? screenCenter.position : Vector3.zero);
        
        // 移動先ターゲット
        Transform targetArea = isPlayer ? handArea : enemyHandArea;
        Vector3 endPos = targetArea ? targetArea.position : Vector3.zero;
        if(handArea && isPlayer) 
        {
             // 手札の中心あたりを目指す（簡易的）
             endPos = handArea.position; 
        }

        if (isPlayer)
        {
            // 手札上限チェック
            if (handArea.childCount >= 10)
            {
                Debug.Log("手札がいっぱいです");
                 // 燃やす演出などを入れる
                 // 簡易的に燃やすログだけ
                 if(BattleLogManager.instance) BattleLogManager.instance.AddLog($"{card.cardName} は手札がいっぱいで燃えた", true, card);
                yield break;
            }
        }
        else
        {
            // 敵の場合も手札データに追加
            enemyHandData.Add(card);
            enemyDeckCount++; 
        }

        // 演出用のカード生成
        Transform tempParent = effectCanvasLayer != null ? effectCanvasLayer : (handArea ? handArea.root : null);
        GameObject flyObj = Instantiate(isPlayer ? cardPrefab.gameObject : cardBackPrefab, tempParent);
        
        flyObj.transform.position = startPos;
        flyObj.transform.localScale = Vector3.one; // 盤面上のサイズ感に合わせるなら1倍
        
        // プレイヤーなら表面を見せる
        if (isPlayer)
        {
            var v = flyObj.GetComponent<CardView>();
            if(v) { v.SetCard(card); v.ShowBack(false); }
        }

        // Raycastブロック等はオフ
        var cg = flyObj.GetComponent<CanvasGroup>();
        if (!cg) cg = flyObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // 手札へ飛ぶアニメーション
        float flyTime = 0.4f;
        float elapsed = 0f;
        Vector3 finalScale = isPlayer ? Vector3.one : Vector3.one * 0.5f; // 手札内サイズ

        while (elapsed < flyTime)
        {
            float t = elapsed / flyTime;
            // イージング：最初ゆっくり、最後速くとか、あるいは逆とか
             t = t * t * (3f - 2f * t); // smoothstep

            flyObj.transform.position = Vector3.Lerp(startPos, endPos, t);
            flyObj.transform.localScale = Vector3.Lerp(Vector3.one, finalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 到着
        flyObj.transform.position = endPos;
        Destroy(flyObj);

        // 正式に手札に追加（敵なら裏面オブジェクトを増やす）
        if (isPlayer)
        {
            GameObject finalCard = Instantiate(cardPrefab.gameObject, handArea);
            finalCard.transform.localScale = Vector3.one;
            finalCard.transform.localRotation = Quaternion.identity;
            
            var view = finalCard.GetComponent<CardView>();
            if(view) { view.SetCard(card); view.ShowBack(false); }

            // 一瞬入力をブロック
            var fcg = finalCard.GetComponent<CanvasGroup>();
            if(fcg) fcg.blocksRaycasts = true;
            
            UpdateHandState();
        }
        else
        {
            // 敵の手札ビジュアル追加
             if (enemyHandArea != null && cardBackPrefab != null)
            {
                GameObject obj = Instantiate(cardBackPrefab, enemyHandArea);
                obj.transform.localScale = Vector3.one;
            }
        }
        
        UpdateDeckGraveyardVisuals();
    }
    #endregion

    #region Enemy Setup
    void SetupEnemyDeck()
    {
        enemyMainDeck.Clear();
        if (enemyDeckDefinition != null && enemyDeckDefinition.Count > 0)
        {
            enemyMainDeck.AddRange(enemyDeckDefinition);
        }
        else
        {
            CardData[] allCards = Resources.LoadAll<CardData>("CardsData");
            for(int i = 0; i < 30; i++)
            {
                if(allCards.Length > 0) enemyMainDeck.Add(allCards[Random.Range(0, allCards.Length)]);
            }
        }

        for (int i = 0; i < enemyMainDeck.Count; i++)
        {
            CardData temp = enemyMainDeck[i];
            int r = Random.Range(i, enemyMainDeck.Count);
            enemyMainDeck[i] = enemyMainDeck[r];
            enemyMainDeck[r] = temp;
        }
        enemyDeckCount = enemyMainDeck.Count;

        if (enemyBuildDefinition != null && enemyBuildDefinition.Count > 0)
        {
            if (enemyLoadoutBuilds == null) enemyLoadoutBuilds = new List<CardData>();
            enemyLoadoutBuilds.Clear();
            enemyLoadoutBuilds.AddRange(enemyBuildDefinition);
        }
    }

    void InitializeEnemyHand(int count)
    {
        for(int i = 0; i < count; i++)
        {
            if (enemyMainDeck.Count <= 0) break;
            CardData card = enemyMainDeck[0];
            enemyMainDeck.RemoveAt(0);
            enemyHandData.Add(card);
            enemyDeckCount--;
            if (enemyHandArea != null && cardBackPrefab != null)
            {
                GameObject obj = Instantiate(cardBackPrefab, enemyHandArea);
                obj.transform.localScale = Vector3.one;
            }
        }
        UpdateDeckGraveyardVisuals();
    }
    #endregion

    #region Turn Management
    public void StartPlayerTurn() 
    { 
        StartCoroutine(PlayerTurnSequence()); 
    }

    System.Collections.IEnumerator PlayerTurnSequence() 
    { 
        isPlayerTurn = true; 
        yield return new WaitForSeconds(0.5f); 

        if (turnCutIn != null) 
        { 
            turnCutIn.gameObject.SetActive(true); 
            turnCutIn.Show("あなたのターン", Color.cyan); 
        } 
        
        // ★Sync Fix: Online Mode Enemy Turn End Processing
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;

        yield return new WaitForSeconds(2.0f); 

        turnCount++; 
        if (BattleLogManager.instance != null) BattleLogManager.instance.AddTurnLabel(turnCount); 

        if (playerBuildCooldown > 0) playerBuildCooldown--; 

        if (maxMana < 10) maxMana++; 
        currentMana = maxMana; 
        UpdateManaUI(); 

        StartCoroutine(DrawSequence(1, true)); 
        ResetBoardState(true); 

        if (buildUIManager != null && buildUIManager.gameObject.activeSelf) buildUIManager.OpenMenu(true); 
        UpdateBuildUI(); 
        if (Input.GetKeyDown(KeyCode.Space)) OnClickEndTurn();
    }

    // ★Sync Network State (Host writes, Guest reads)
    // ★Two-way Sync Logic
    public void SyncNetworkInfo()
    {
        var gameState = FindObjectOfType<GameStateController>();
        if (gameState == null || gameState.Object == null || !gameState.Object.IsValid) return;

        bool isHost = gameState.Object.HasStateAuthority;
        var myPC = NetworkPlayerController.Get(gameState.Runner.LocalPlayer);
        
        // --- 1. Push Local Stats to Network ---
        int myJobId = 0;
        if (PlayerDataManager.instance != null && PlayerDataManager.instance.playerData != null)
        {
             var d = PlayerDataManager.instance.playerData;
             if (d.decks != null && d.currentDeckIndex >= 0 && d.currentDeckIndex < d.decks.Count)
             {
                 myJobId = (int)d.decks[d.currentDeckIndex].deckJob;
             }
        }

        if (myPC != null)
        {
            if (myPC.Object.HasStateAuthority)
            {
                // Direct Update
                if (playerLeader != null) myPC.CurrentHp = playerLeader.GetComponent<Leader>().currentHp;
                myPC.HandCount = hand.Count;
                myPC.DeckCount = mainDeck.Count;
                myPC.GraveCount = playerGraveyardCount;
                myPC.CurrentMana = currentMana;
                myPC.MaxMana = maxMana; // ★追加: MaxManaも同期
                myPC.JobId = myJobId;
            }
            else
            {
                // Guest in Host Mode (Input Authority only)
                SendStatsToHost(gameState, myJobId, myPC.Object.Id);
            }
        }

        // --- 2. Read Enemy Stats from Network ---
        // ★修正：有効なNetworkObjectのみを対象にし、現在のRunnerに属するもの、かつ最新のものを取得する
        // ★修正：InputAuthority を用いて自分以外のプレイヤーのコントローラーを確実に特定する
        var enemyPC = NetworkPlayerController.Instances.FirstOrDefault(pc => 
            pc != null &&
            pc.Object != null && 
            pc.Object.IsValid && 
            pc.Object.IsValid && 
            pc.Object.InputAuthority != gameState.Runner.LocalPlayer);
        
        // ★Fallback: If Instances cache fails
        if (enemyPC == null)
        {
             var allPCs = FindObjectsOfType<NetworkPlayerController>();
             foreach(var pc in allPCs)
             {
                 if (pc != null && pc.Object != null && pc.Object.IsValid && pc.Object.InputAuthority != gameState.Runner.LocalPlayer)
                 {
                     enemyPC = pc;
                     // Debug.Log("[Sync] Found Enemy PC via FindObjectsOfType fallback");
                     break;
                 }
             }
        }
        
        if (enemyPC != null)
        {
             // Update Dummy HP on screen
             Leader enemyL = enemyLeaderObj != null ? enemyLeaderObj.GetComponent<Leader>() : null;
             
             if (enemyL == null && enemyLeaderObj == null)
             {
                  enemyLeaderObj = GameObject.Find("EnemyInfo");
                  if (enemyLeaderObj) enemyL = enemyLeaderObj.GetComponent<Leader>();
             }

             if (enemyL != null)
             {
                 if (enemyL.currentHp != enemyPC.CurrentHp)
                 {
                      enemyL.currentHp = enemyPC.CurrentHp;
                      enemyL.UpdateHPBar();
                 }
             }
             
             // Update Leader Icon
             if (enemyPC.JobId != 0) 
             {
                 SetEnemyLeaderIcon(enemyPC.JobId);
             }
             else
             {
                 // デバッグ用: JobIdが0の場合はアイコン更新がスキップされる
                 // Debug.Log($"[Sync] Enemy JobId is 0. Icon update skipped. PC: {enemyPC.Object.Id}");
             }

             // Update Dummy Counts
             enemyDeckCount = enemyPC.DeckCount;
             enemyGraveyardCount = enemyPC.GraveCount;
             
             // [NEW] Update Enemy Mana for Logic
             enemyCurrentMana = enemyPC.CurrentMana;
             enemyMaxMana = enemyPC.MaxMana; // ★修正: MaxManaも同期
             UpdateEnemyManaUI(); // Reflect visual
             
             // Update Dummy Hand
             UpdateEnemyHandVisuals(enemyPC.HandCount);
             
             // ★FIX: UI Text update is CRITICAL
             if (enemyHandCountText != null) 
             {
                 enemyHandCountText.text = enemyPC.HandCount.ToString();
                 enemyHandCountText.gameObject.SetActive(true); // 確実に見えるように
             }
             if (enemyDeckCountText != null) 
             {
                 enemyDeckCountText.text = enemyPC.DeckCount.ToString();
                 enemyDeckCountText.gameObject.SetActive(true);
             }

             UpdateDeckGraveyardVisuals(); 
             
             // デバッグ用: 同期データをログ出力
             Debug.Log($"[Sync] EnemyPC Stats: HP={enemyPC.CurrentHp}, Hand={enemyPC.HandCount}, Deck={enemyPC.DeckCount}, Grave={enemyPC.GraveCount}, Mana={enemyPC.CurrentMana}/{enemyPC.MaxMana}, Job={enemyPC.JobId}");
        }
        else
        {
             // Debug.Log("[SyncNetworkInfo] No Enemy PC found.");
        }
        // Debug.Log($"[SyncNetworkInfo] Player Hand: {hand.Count}, Deck: {mainDeck.Count}, Grave: {playerGraveyardCount}, Mana: {currentMana}, MaxMana: {maxMana}, Job: {myJobId}");
    }
    
    // SetEnemyLeaderIcon removed (Duplicate)
    
    private int _lastSentHp = -1;
    private int _lastSentHand = -1;
    private int _lastSentDeck = -1;
    private int _lastSentGrave = -1;
    private int _lastSentMana = -1;
    private int _lastSentMaxMana = -1; // ★追加
    private int _lastSentJobId = -1; // [NEW]

    void SendStatsToHost(GameStateController gameState, int jobId, NetworkId myId)
    {
         int myHp = playerLeader ? playerLeader.GetComponent<Leader>().currentHp : 0;
         int myHand = hand.Count;
         int myDeck = mainDeck.Count;
         int myGrave = playerGraveyardCount;
         int myMana = currentMana;
         int myMaxMana = maxMana; // ★追加
         
         if (myHp != _lastSentHp || myHand != _lastSentHand || myDeck != _lastSentDeck || myGrave != _lastSentGrave || myMana != _lastSentMana || myMaxMana != _lastSentMaxMana || jobId != _lastSentJobId)
         {
             _lastSentHp = myHp;
             _lastSentHand = myHand;
             _lastSentDeck = myDeck;
             _lastSentGrave = myGrave;
             _lastSentMana = myMana;
             _lastSentMaxMana = myMaxMana; // ★追加
             _lastSentJobId = jobId;
             
             _lastSentJobId = jobId;
             
             gameState.RPC_UpdatePlayerStats(myId, myHand, myDeck, myGrave, myHp, myMana, myMaxMana, jobId); // ★修正: NetworkId経由で確実化
         }
    }

    void UpdateEnemyHandVisuals(int targetCount)
    {
        if (enemyHandArea != null)
        {
            int current = enemyHandArea.childCount;
            Debug.Log($"[Sync] Enemy Hand UI Count: {current} -> Target: {targetCount}");
            
            if (current < targetCount)
            {
                for(int i=current; i<targetCount; i++)
                {
                    GameObject prefab = cardBackPrefab;
                    if (prefab == null && cardPrefab != null) prefab = cardPrefab.gameObject;

                    if (prefab != null) 
                    {
                        GameObject cardObj = Instantiate(prefab, enemyHandArea);
                        cardObj.transform.localScale = Vector3.one;
                        cardObj.transform.localPosition = Vector3.zero;
                        cardObj.SetActive(true);
                        cardObj.layer = enemyHandArea.gameObject.layer; 
                        
                        // ★CPU戦の挙動を再現: CardViewがある場合は裏面表示を強制
                        var cv = cardObj.GetComponent<CardView>();
                        if (cv) cv.ShowBack(true);
                        
                        // RectTransform のリセット (UI配置のズレ防止)
                        RectTransform rt = cardObj.GetComponent<RectTransform>();
                        if (rt)
                        {
                            rt.anchoredPosition = Vector2.zero;
                            rt.localScale = Vector3.one;
                        }
                        // Debug.Log($"[Sync] Added enemy hand card. Current count: {enemyHandArea.childCount}");
                    }
                }
            }
            else if (current > targetCount)
            {
                for(int i=current; i>targetCount; i--)
                {
                    if (enemyHandArea.childCount > 0) 
                    {
                        Transform lastCard = enemyHandArea.GetChild(enemyHandArea.childCount - 1);
                        // 即座に名前を変えるなどして重複取得を避ける（簡易的な対策）
                        lastCard.name = "__DESTROYING__"; 
                        lastCard.SetParent(null);
                        Destroy(lastCard.gameObject);
                        // Debug.Log($"[Sync] Removed enemy hand card. Current count: {enemyHandArea.childCount}");
                    }
                }
            }
        }
    }
    public void OnClickEndTurn() 
    { 
        if (!isPlayerTurn && !NetworkConnectionManager.instance) return;

        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        
        if (isOnline)
        {
            if (isPlayerTurn)
            {
                isPlayerTurn = false;
                var gameState = FindObjectOfType<GameStateController>();
                if (gameState != null) gameState.RPC_RequestEndTurn();
            }
        }
        else
        {
            if (!isPlayerTurn) return; 
            isPlayerTurn = false; 
            StartCoroutine(EndTurnSequence()); 
        }
    }

    // Host executes this for online turn end
    public void ProcessOnlineTurnEnd(bool isPlayerOneTurn)
    {
         AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, isPlayerOneTurn);
         DecreaseBuildDuration(isPlayerOneTurn);
         AbilityManager.instance.ProcessTurnEndEffects(isPlayerOneTurn);
    }

    // ★追加: PlayerRefを受け取るオーバーロード (GameStateControllerから呼ばれる)
    public void ProcessTurnEndEffects(PlayerRef activePlayer)
    {
        // P1かどうか判定 (ここではホストから見てP1かどうか)
        // 本来はPlayerIdの大小やFirstPlayer情報を見るべきだが、簡易的にActivePlayerとローカル/リモート判定
        // GameStateControllerで P1/P2 管理が行われている前提
        
        bool isP1 = (activePlayer == NetworkConnectionManager.instance.Runner.LocalPlayer);
        // ※注意: これはホスト視点。もしホストがP2なら逆になる。
        // GameStateController.FirstPlayerと比較するのが正しい
        
        var gameState = FindObjectOfType<GameStateController>();
        if (gameState != null)
        {
            // FirstPlayerなら P1, そうでなければ P2
            isP1 = (activePlayer == gameState.FirstPlayer);
        }
        
        ProcessOnlineTurnEnd(isP1);
    }

    System.Collections.IEnumerator EndTurnSequence() 
    { 
        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, true); 
        DecreaseBuildDuration(true); 
        AbilityManager.instance.ProcessTurnEndEffects(true); 

        isPlayerTurn = false; 
        yield return new WaitForSeconds(1.0f); 

        // オンライン時はAIターンを開始せず、ネットワーク同期を待つ
        // GameStateControllerを通じてターン終了を通知
        
        // 変数定義の欠落を修正
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        
        var gameState = FindObjectOfType<GameStateController>();
        if (isOnline && gameState != null)
        {
            gameState.EndTurn();
        }
        else if (!isOnline)
        {
            StartEnemyTurn(); 
        }
    }

    public void OnOnlineEnemyTurnStart()
    {
        // オンライン対戦での敵ターン開始処理
        if (turnCutIn != null) turnCutIn.Show("相手のターン", Color.red);
        isPlayerTurn = false;
        
        // 敵のボード状態（ビルドの建築完了など）を更新
        ResetBoardState(false);
        
        // ★FIX: Increment Enemy Mana on Host to match Guest's state
        if (enemyMaxMana < 10) enemyMaxMana++;
        enemyCurrentMana = enemyMaxMana;
        UpdateEnemyManaUI();
        
        // UI更新などが必要ならここで行う
        UpdateBuildUI();
    }


    public void StartEnemyTurn() 
    { 
        // Online Check
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        
        if (isOnline)
        {
            OnOnlineEnemyTurnStart();
        }
        else
        {
            StartCoroutine(EnemyTurnSequence()); 
        }
    }

    System.Collections.IEnumerator EnemyTurnSequence() 
    { 
        if (isGameEnded) yield break;

        if (turnCutIn != null) turnCutIn.Show("相手のターン", Color.red); 
        yield return new WaitForSeconds(2.0f);  

        if (enemyBuildCooldown > 0) enemyBuildCooldown--; 

        if (enemyMaxMana < 10) enemyMaxMana++; 
        enemyCurrentMana = enemyMaxMana; 
        UpdateEnemyManaUI(); 

        ResetBoardState(false); 
        UpdateBuildUI(); 

        EnemyDrawCard(1); 
        yield return new WaitForSeconds(1.0f); 

        yield return StartCoroutine(EnemyAttackPhase()); 
        yield return StartCoroutine(EnemyMainPhase()); 
        yield return StartCoroutine(EnemyAttackPhase()); 

        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, false); 
        DecreaseBuildDuration(false); 
        AbilityManager.instance.ProcessTurnEndEffects(false); 

        Invoke("StartPlayerTurn", 1.0f); 
    }

    void ResetBoardState(bool isPlayer) 
    { 
        Transform board = isPlayer ? GameObject.Find("PlayerBoard").transform : enemyBoard; 
        if (board != null) 
        { 
            foreach (UnitMover unit in board.GetComponentsInChildren<UnitMover>()) 
            { 
                unit.canAttack = true; 
                unit.canMove = true; 
                var img = unit.GetComponent<UnityEngine.UI.Image>(); 
                if (img != null) img.color = Color.white; 
            } 
        } 

        if (activeBuilds != null) 
        { 
            foreach (var build in activeBuilds) 
            { 
                if (build.isPlayerOwner == isPlayer) 
                { 
                    if (build.isUnderConstruction) build.isUnderConstruction = false; 
                    build.hasActed = false; 
                } 
            } 
        } 
        UpdateBuildUI();
    }
    #endregion

    #region Enemy AI
    System.Collections.IEnumerator EnemyAttackPhase() 
    { 
        if (enemyBoard == null) yield break; 
        if (isGameEnded) yield break;

        var attackers = enemyBoard.GetComponentsInChildren<UnitMover>()
            .Where(u => u.canAttack)
            .OrderByDescending(u => u.attackPower)
            .ToList(); 

        Transform playerBoard = GameObject.Find("PlayerBoard").transform; 

        foreach (var attacker in attackers) 
        { 
            if (attacker == null || !attacker.canAttack) continue; 

            UnitMover bestTarget = null; 
            bool attackLeader = true; 
            List<UnitMover> tauntUnits = new List<UnitMover>(); 
            List<UnitMover> allPlayerUnits = new List<UnitMover>(); 

            if (playerBoard != null) 
            { 
                foreach(var u in playerBoard.GetComponentsInChildren<UnitMover>()) 
                { 
                    allPlayerUnits.Add(u); 
                    if (u.IsTauntActive) 
                    {
                        tauntUnits.Add(u); 
                        Debug.Log($"[EnemyAI] Found Taunt Unit: {u.sourceData.cardName} at Slot({(u.transform.parent?u.transform.parent.GetComponent<SlotInfo>().x:-1)},{(u.transform.parent?u.transform.parent.GetComponent<SlotInfo>().y:-1)})");
                    }
                } 
            } 
            Debug.Log($"[EnemyAI] Attacker: {attacker.sourceData.cardName}. TauntUnits Count: {tauntUnits.Count}. AllUnits: {allPlayerUnits.Count}"); 

            if (tauntUnits.Count > 0) 
            { 
                attackLeader = false; 
                bestTarget = tauntUnits.OrderBy(t => t.health).FirstOrDefault(); 
            } 
            else 
            { 
                var freeKill = allPlayerUnits.Where(t => !t.hasStealth && CanAttackUnit(attacker, t))
                    .Where(t => attacker.attackPower >= t.health && attacker.health > t.attackPower)
                    .OrderByDescending(t => t.attackPower)
                    .FirstOrDefault(); 

                if (freeKill != null) 
                { 
                    attackLeader = false; 
                    bestTarget = freeKill; 
                } 
                else if (!CanAttackLeader(attacker)) 
                { 
                    attackLeader = false; 
                    bestTarget = allPlayerUnits.Where(t => !t.hasStealth && CanAttackUnit(attacker, t))
                        .OrderBy(t => t.health)
                        .FirstOrDefault(); 
                } 
            } 

            if (attackLeader) 
            { 
                if (playerLeader != null) 
                { 
                    Leader leader = playerLeader.GetComponent<Leader>(); 
                    attacker.Attack(leader); 
                    yield return new WaitForSeconds(0.5f); 
                } 
            } 
            else if (bestTarget != null) 
            { 
                attacker.AttackUnit(bestTarget); 
                yield return new WaitForSeconds(0.5f); 
            } 
        } 
    }

    System.Collections.IEnumerator EnemyMainPhase() 
    { 
        if (isGameEnded) yield break;
        bool acted = true; 
        int loopSafety = 0; 
        while (acted && loopSafety < 10) 
        { 
            acted = false; 
            loopSafety++; 

            var playableCards = enemyHandData.Where(c => c.cost <= enemyCurrentMana)
                .OrderByDescending(c => c.cost)
                .ToList(); 

            if (playableCards.Count > 0) 
            { 
                foreach(var cardToPlay in playableCards) 
                { 
                    bool played = false; 
                    Debug.Log($"[EnemyAI] Attempting to play: {cardToPlay.cardName} (Cost: {cardToPlay.cost}, Type: {cardToPlay.type})");
                    if (cardToPlay.type == CardType.UNIT) played = TrySummonEnemyUnit(cardToPlay); 
                    else if (cardToPlay.type == CardType.SPELL) played = TryCastEnemySpell(cardToPlay); 
                    else if (cardToPlay.type == CardType.BUILD) played = TryBuildEnemy(cardToPlay); 

                    if (played) 
                    { 
                        ShowUnitDetail(cardToPlay); 
                        UseEnemyCard(cardToPlay); 
                        acted = true; 
                        yield return new WaitForSeconds(1.5f); 
                        OnClickCloseDetail(); 
                        break; 
                    } 
                } 
            } 

            if (!acted && enemyBuildCooldown <= 0) 
            { 
                bool built = TryBuildFromLoadout(); 
                if (built) 
                { 
                    acted = true; 
                    yield return new WaitForSeconds(1.5f); 
                } 
            } 
        } 
    }

    bool TryBuildFromLoadout() 
    { 
        if (enemyLoadoutBuilds == null || enemyLoadoutBuilds.Count == 0) return false; 
        if (HasActiveBuild(false)) return false; 

        var buildable = enemyLoadoutBuilds.Where(b => b.cost <= enemyCurrentMana)
            .OrderByDescending(b => b.cost)
            .FirstOrDefault(); 

        if (buildable != null) 
        { 
            enemyCurrentMana -= buildable.cost; 
            UpdateEnemyManaUI(); 

            if (activeBuilds == null) activeBuilds = new List<ActiveBuild>(); 
            activeBuilds.Add(new ActiveBuild(buildable, false)); 

            enemyBuildCooldown = maxEnemyBuildCooldown; 
            enemyBuildCooldown = maxEnemyBuildCooldown; 
            BroadcastLog($"敵は {buildable.cardName} を建設した", false, buildable); 
            PlaySE(seSummon); 
            UpdateBuildUI(); 
            ShowBuildDetail(buildable, buildable.duration); 
            Invoke("OnClickCloseDetail", 1.5f); 
            return true; 
        } 
        return false; 
    }

    bool TrySummonEnemyUnit(CardData card) 
    { 
        Transform emptySlot = FindBestEmptySlot(card); 
        if (emptySlot == null) return false; 

        GameObject newUnit = SpawnUnit(unitPrefabForEnemy, emptySlot, null); // Assuming 'sender' is null or needs to be defined elsewhere
        if (newUnit == null) return false; // Changed 'return;' to 'return false;' for consistency with bool return type
        
        UnitMover mover = newUnit.GetComponent<UnitMover>(); 
        mover.Initialize(card, false); 

        object manualTarget = null; 
        if (HasSelectTargetAbility(card)) manualTarget = DecideBestTarget(card, mover); 

        AbilityManager.instance.ProcessAbilities(card, EffectTrigger.ON_SUMMON, mover, manualTarget); 
        mover.PlaySummonAnimation(); 

        if (mover.hasHaste) mover.canAttack = true; 

        if (mover.hasHaste) mover.canAttack = true; 

        BroadcastLog($"敵は {card.cardName} を召喚した", false, card); 
        PlaySE(seSummon); 
        return true; 
    }

    bool TryCastEnemySpell(CardData card) 
    { 
        object target = null; 
        if (CheckIfSpellNeedsTarget(card)) 
        { 
            target = DecideBestTarget(card, null); 
            if (target == null) return false; 
        } 

        AbilityManager.instance.ProcessAbilities(card, EffectTrigger.SPELL_USE, null, target); 
        AbilityManager.instance.TriggerSpellReaction(false); 
        PlayDiscardAnimation(card, false); 

        PlayDiscardAnimation(card, false); 

        BroadcastLog($"敵は {card.cardName} を唱えた", false, card); 
        PlaySE(seSummon); 
        return true; 
    }

    bool TryBuildEnemy(CardData card) 
    { 
        if (activeBuilds == null) activeBuilds = new List<ActiveBuild>(); 
        activeBuilds.Add(new ActiveBuild(card, false)); 

        BroadcastLog($"敵は {card.cardName} を建設した", false, card); 
        PlaySE(seSummon); 
        UpdateBuildUI(); 
        return true; 
    }

    Transform FindBestEmptySlot(CardData card) 
    { 
        if (enemyBoard == null) return null; 

        bool hasTaunt = card.abilities.Any(a => a.trigger == EffectTrigger.PASSIVE && a.effect == EffectType.TAUNT); 
        if (hasTaunt) 
        { 
            foreach (Transform slot in enemyBoard) 
            { 
                if (GetSlotY(slot) == 0 && slot.childCount == 0) return slot; 
            } 
        } 

        foreach (Transform slot in enemyBoard) 
        { 
            if (slot.childCount == 0) return slot; 
        } 
        return null; 
    }

    object DecideBestTarget(CardData card, UnitMover source) 
    { 
        var ability = card.abilities.FirstOrDefault(); 
        if (ability == null) return null; 

        if (ability.effect == EffectType.DAMAGE || ability.effect == EffectType.DESTROY || ability.effect == EffectType.RETURN_TO_HAND) 
        { 
            var playerBoard = GameObject.Find("PlayerBoard").transform; 
            // Filter out Stealth units
            var targets = playerBoard.GetComponentsInChildren<UnitMover>()
                .Where(u => !u.hasStealth) // Stealth filter
                .OrderByDescending(u => u.attackPower).ToList(); 
            if (targets.Count > 0) return targets[0]; 

            if (ability.target == EffectTarget.SELECT_ANY_ENEMY || ability.target == EffectTarget.SELECT_ENEMY_LEADER) return playerLeader.GetComponent<Leader>(); 
        } 
        else if (ability.effect == EffectType.BUFF_ATTACK || ability.effect == EffectType.BUFF_HEALTH || ability.effect == EffectType.HEAL) 
        { 
            var targets = enemyBoard.GetComponentsInChildren<UnitMover>().ToList(); 
            if (targets.Count > 0) return targets[0]; 
        } 
        return null; 
    }
    #endregion

    #region Card Operations (Draw & Discard)
    public void EnemyDrawCard(int count = 1, bool silent = false) 
    { 
        StartCoroutine(DrawSequence(count, false, silent)); 
    }

    public void DealCards(int count) 
    { 
        StartCoroutine(DrawSequence(count, true)); 
    }

    System.Collections.IEnumerator DrawSequence(int count, bool isPlayer, bool silent = false) 
    { 
        Vector3 centerPos = Vector3.zero; 
        if (screenCenter != null) centerPos = screenCenter.position; 

        float heightOffset = 150f * 3.0f * 0.5f; 
        Vector3 adjustCenterPos = centerPos; 
        if(isPlayer) adjustCenterPos -= new Vector3(0, heightOffset, 0); 
        else adjustCenterPos += new Vector3(0, 100f, 0); 

        Transform targetArea = isPlayer ? handArea : enemyHandArea; 
        Vector3 startPos = isPlayer ? (playerDeckIsland ? playerDeckIsland.position : Vector3.zero) : (enemyDeckIsland ? enemyDeckIsland.position : Vector3.zero); 

        for (int i = 0; i < count; i++) 
        { 
            if (isPlayer) 
            { 
                if (mainDeck.Count <= 0) 
                { 
                    fatigueCount++; 
                    SpawnDamageText(playerLeader.position, fatigueCount); 
                    playerLeader.GetComponent<Leader>().TakeDamage(fatigueCount); 
                    if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"疲労ダメージ: {fatigueCount}", true); 
                    yield return new WaitForSeconds(0.5f); 
                    continue; 
                } 
                if (handArea.childCount >= 10) 
                { 
                    CardData burned = mainDeck[0]; 
                    mainDeck.RemoveAt(0); 
                    if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"{burned.cardName} が燃えた！", true, burned); 
                    UpdateDeckGraveyardVisuals(); 
                    continue; 
                } 
            } 
            else 
            { 
                if (enemyMainDeck.Count <= 0) 
                { 
                    enemyFatigueCount++; 
                    yield return new WaitForSeconds(0.5f); 
                    continue; 
                } 
            } 

            CardData cardData = null; 
            if (isPlayer) 
            { 
                cardData = mainDeck[0]; 
                mainDeck.RemoveAt(0); 
            } 
            else 
            { 
                cardData = enemyMainDeck[0]; 
                enemyMainDeck.RemoveAt(0); 
                enemyDeckCount = enemyMainDeck.Count; 
                enemyHandData.Add(cardData); 
            } 
            PlaySE(seDraw); 

            // ★FIX: Check if effectCanvasLayer is actually visible. If not, fallback to handArea or top-level canvas.
            Transform tempParent = (effectCanvasLayer != null && effectCanvasLayer.gameObject.activeInHierarchy) ? effectCanvasLayer : null;
            if (tempParent == null)
            {
                if (handArea != null) tempParent = handArea.parent; // Likely a visible UI container
                else tempParent = transform.root; // Failsafe
            }
            
            // ★FIX: Failover logic for CardBack
            GameObject prefabToUse = isPlayer ? cardPrefab.gameObject : (cardBackPrefab != null ? cardBackPrefab : cardPrefab.gameObject);
            
            GameObject cardObj = Instantiate(prefabToUse, tempParent); 
            
            // If we used CardView as back
            if (!isPlayer && prefabToUse == cardPrefab.gameObject)
            {
                var cv = cardObj.GetComponent<CardView>();
                if (cv) cv.ShowBack(true);
            } 
            cardObj.transform.position = startPos; 
            cardObj.transform.localScale = Vector3.one * 0.5f; 

            var cg = cardObj.GetComponent<CanvasGroup>(); 
            if (cg == null) cg = cardObj.AddComponent<CanvasGroup>(); 
            cg.blocksRaycasts = false; 

            if (isPlayer) 
            { 
                var view = cardObj.GetComponent<CardView>(); 
                view.SetCard(cardData); 
                view.ShowBack(true); 
            } 

            float moveTime = 0.25f; 
            float elapsed = 0f; 
            Vector3 initialScale = Vector3.one * 0.5f; 
            Vector3 centerScale = isPlayer ? Vector3.one * 3.0f : Vector3.one * 1.5f; 

            while (elapsed < moveTime) 
            { 
                float t = elapsed / moveTime; 
                t = t * t * (3f - 2f * t); 
                cardObj.transform.position = Vector3.Lerp(startPos, adjustCenterPos, t); 
                cardObj.transform.localScale = Vector3.Lerp(initialScale, centerScale, t); 
                elapsed += Time.deltaTime; 
                yield return null; 
            } 
            cardObj.transform.position = adjustCenterPos; 
            cardObj.transform.localScale = centerScale; 

            if (isPlayer) 
            { 
                var view = cardObj.GetComponent<CardView>(); 
                yield return new WaitForSeconds(0.1f); 
                float flipTime = 0.15f; 
                elapsed = 0f; 
                bool flipped = false; 
                while (elapsed < flipTime) 
                { 
                    float t = elapsed / flipTime; 
                    float angle = Mathf.Lerp(0, 90, t); 
                    cardObj.transform.rotation = Quaternion.Euler(0, angle, 0); 
                    if (t >= 0.5f && !flipped) 
                    { 
                        view.ShowBack(false); 
                        flipped = true; 
                    } 
                    elapsed += Time.deltaTime; 
                    yield return null; 
                } 
                elapsed = 0f; 
                while (elapsed < flipTime) 
                { 
                    float t = elapsed / flipTime; 
                    float angle = Mathf.Lerp(90, 0, t); 
                    cardObj.transform.rotation = Quaternion.Euler(0, angle, 0); 
                    elapsed += Time.deltaTime; 
                    yield return null; 
                } 
                cardObj.transform.rotation = Quaternion.identity; 
                yield return new WaitForSeconds(0.15f); 
            } 
            else yield return new WaitForSeconds(0.1f); 

            float flyTime = 0.2f; 
            elapsed = 0f; 
            Vector3 startFly = cardObj.transform.position; 
            Vector3 targetFly = targetArea.position; 

            while (elapsed < flyTime) 
            { 
                float t = elapsed / flyTime; 
                t = t * t; 
                cardObj.transform.position = Vector3.Lerp(startFly, targetFly, t); 
                cardObj.transform.localScale = Vector3.Lerp(centerScale, Vector3.one, t); 
                elapsed += Time.deltaTime; 
                yield return null; 
            } 
            cardObj.transform.SetParent(targetArea); 
            cardObj.transform.localScale = Vector3.one; 
            cardObj.transform.localRotation = Quaternion.identity; 
            if (cg != null) cg.blocksRaycasts = true; 

            if (isPlayer && !silent) 
            {
                BroadcastLog("カードを引いた", true);
                
                // ★Notify Others
                var gs = FindObjectOfType<GameStateController>();
                if(gs != null && gs.Object != null && gs.Object.IsValid)
                {
                     gs.RPC_SyncDraw(gs.Runner.LocalPlayer, 1);
                }
            }
            else if (!isPlayer && !silent) BroadcastLog("相手がカードを引いた", false); 

            UpdateHandState(); 
            UpdateDeckGraveyardVisuals(); 
            yield return new WaitForSeconds(0.05f); 
        } 
    }

    public void BroadcastLog(string text, bool isPlayerAction, CardData card = null)
    {
        var gameState = FindObjectOfType<GameStateController>();
        if (gameState != null && gameState.Object.IsValid)
        {
            if (gameState.Object.HasStateAuthority)
            {
                // Logic runs on Host.
                // If it is Player Action -> Actor is Host (LocalPlayer)
                // If it is NOT Player Action (Enemy Action) -> Actor is Guest (Opponent)
                
                Fusion.PlayerRef actor = gameState.Runner.LocalPlayer; // Default: Me (Host)
                
                if (!isPlayerAction)
                {
                    // Find the OTHER player (Guest)
                    foreach(var p in gameState.Runner.ActivePlayers) 
                    { 
                        if (p != gameState.Runner.LocalPlayer) { actor = p; break; } 
                    }
                }
                
                string cardId = card != null ? card.id : "";
                gameState.RPC_BroadcastLog(text, actor, cardId);
            }
            // Guest: Do nothing to avoid double logs (Host will RPC)
        }
        else
        {
             if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog(text, isPlayerAction, card);
        }
    }

    public void UpdateDeckGraveyardVisuals() 
    { 
        if (playerDeckVisual) playerDeckVisual.SetActive(mainDeck.Count > 0); 
        if (enemyDeckVisual) enemyDeckVisual.SetActive(enemyDeckCount > 0); 
        if (playerGraveVisual) playerGraveVisual.SetActive(playerGraveyardCount > 0); 
        if (enemyGraveVisual) enemyGraveVisual.SetActive(enemyGraveyardCount > 0); 

        if (playerDeckCountText) playerDeckCountText.text = mainDeck.Count.ToString(); 
        if (enemyDeckCountText) enemyDeckCountText.text = enemyDeckCount.ToString(); 
        if (playerGraveCountText) playerGraveCountText.text = playerGraveyardCount.ToString(); // ★追加
        if (enemyGraveCountText) enemyGraveCountText.text = enemyGraveyardCount.ToString(); // ★追加
    }

    public void PlayDiscardAnimation(CardData card, bool isPlayer) 
    { 
        if (cardPrefab == null) return; 
        if (deckCardPrefab == null) 
        {
             // Debug.LogWarning("[GameManager] deckCardPrefab is not assigned! Discard animation will fall back to CardBack.");
             return; // Or continue with cardPrefab? Let's just return to be safe or assign default.
        }

        Vector3 endPos = isPlayer ? (playerDeckIsland ? playerDeckIsland.position : Vector3.zero) : (enemyDeckIsland ? enemyDeckIsland.position : Vector3.zero); 
        if (isPlayer && playerGraveVisual != null) endPos = playerGraveVisual.transform.position; 
        if (!isPlayer && enemyGraveVisual != null) endPos = enemyGraveVisual.transform.position; 

        Vector3 startPos = endPos + new Vector3(0, 100f, 0); 

        // ★FIX: For Discard animation, use DeckCardPrefab sprite instead of full card prefab
        GameObject obj = new GameObject("DiscardVisual");
        Transform tempParent = (effectCanvasLayer != null && effectCanvasLayer.gameObject.activeInHierarchy) ? effectCanvasLayer : null;
        if (tempParent == null)
        {
            if (handArea != null) tempParent = handArea.parent;
            else tempParent = transform.root;
        }
        obj.transform.SetParent(tempParent);

        var img = obj.AddComponent<UnityEngine.UI.Image>();
        img.preserveAspect = true; // ★アスペクト比維持
        
        if (deckCardPrefab != null && deckCardPrefab.GetComponent<UnityEngine.UI.Image>() != null)
        {
            img.sprite = deckCardPrefab.GetComponent<UnityEngine.UI.Image>().sprite;
        }
        else if (cardBackPrefab != null && cardBackPrefab.GetComponent<UnityEngine.UI.Image>() != null)
        {
            img.sprite = cardBackPrefab.GetComponent<UnityEngine.UI.Image>().sprite;
        }
        else if (cardPrefab != null && cardPrefab.GetComponent<UnityEngine.UI.Image>() != null)
        {
            img.sprite = cardPrefab.GetComponent<UnityEngine.UI.Image>().sprite;
        }

        obj.transform.position = startPos; 
        obj.transform.localScale = Vector3.one; 

        Canvas c = obj.GetComponent<Canvas>(); 
        if (c == null) c = obj.AddComponent<Canvas>(); 
        c.overrideSorting = true; 
        c.sortingOrder = 100; 

        CanvasGroup cg = obj.GetComponent<CanvasGroup>(); 
        if (cg == null) cg = obj.AddComponent<CanvasGroup>(); 
        cg.alpha = 0f; 
        cg.blocksRaycasts = false; 

        StartCoroutine(DiscardRoutine(obj, startPos, endPos, isPlayer)); 
    }

    System.Collections.IEnumerator DiscardRoutine(GameObject obj, Vector3 startPos, Vector3 endPos, bool isPlayer) 
    { 
        float duration = 0.5f; 
        float time = 0; 
        CanvasGroup cg = obj.GetComponent<CanvasGroup>(); 
        while(time < duration) 
        { 
            float t = time / duration; 
            obj.transform.position = Vector3.Lerp(startPos, endPos, t); 
            if(cg != null) cg.alpha = Mathf.Lerp(0f, 1f, t * 2f); 
            time += Time.deltaTime; 
            yield return null; 
        } 
        obj.transform.position = endPos; 
        if(cg != null) cg.alpha = 1f; 
        Destroy(obj, 0.1f); 

        if (isPlayer) playerGraveyardCount++; 
        else enemyGraveyardCount++; 

        UpdateDeckGraveyardVisuals(); 
    }
    #endregion

    #region Spell & Interaction
    void Update() 
    { 
        SyncNetworkInfo();
        
        // ★Sync Fix: Send My Stats to Host continuously (if changed)
        if (NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning)
        {
             var gameState = FindObjectOfType<GameStateController>();
             if (gameState != null)
             {
                  int myJobId = 0;
                  if (PlayerDataManager.instance != null && PlayerDataManager.instance.playerData.decks.Count > PlayerDataManager.instance.playerData.currentDeckIndex)
                  {
                       myJobId = (int)PlayerDataManager.instance.playerData.decks[PlayerDataManager.instance.playerData.currentDeckIndex].deckJob;
                  }
                  var myPC = NetworkPlayerController.Get(NetworkConnectionManager.instance.Runner.LocalPlayer);
                  if (myPC != null && myPC.Object != null)
                  {
                      SendStatsToHost(gameState, myJobId, myPC.Object.Id);
                  }
             }
        }
        if (isTargetingMode && currentCastingCard != null) 
        { 
            if (Input.GetMouseButtonDown(1)) 
            { 
                CancelSpellCast(); 
                return; 
            } 

            UpdateArrow(arrowStartPos, Input.mousePosition); 
            
            // ★列情報をリアルタイムに追跡
            int col = GetColumnUnderMouse();
            if (col != -1) lastTargetX = col;
            GameObject targetObj = GetObjectUnderMouse(); 
            bool isValid = false; 

            if (targetObj != null) 
            { 
                UnitMover unit = targetObj.GetComponentInParent<UnitMover>(); 
                Leader leader = targetObj.GetComponentInParent<Leader>(); 

                foreach (var ability in currentCastingCard.cardData.abilities) 
                { 
                    if (CheckTargetValidity(ability.target, unit, leader)) 
                    { 
                        isValid = true; 
                        break; 
                    } 
                } 

                if (isValid) 
                { 
                    int damage = GetAbilityDamage(currentCastingCard.cardData); 
                    int currentHP = unit != null ? unit.health : (leader != null ? leader.currentHp : 0); 
                    int predictedHP = Mathf.Max(0, currentHP - damage); 
                    ShowTooltip($"HP: {currentHP} -> <color=red>{predictedHP}</color>"); 
                } 
                else HideTooltip(); 
            } 
            else HideTooltip(); 

            SetArrowColor(isValid ? Color.red : Color.white); 

            if (Input.GetMouseButtonDown(0)) 
            { 
                if (isValid && targetObj != null) TryCastSpellToTarget(targetObj); 
                else CancelSpellCast(); 
            } 
        } 
        // ★FIX: FloatingTextが消えない問題の修正
        // FloatingTextは自身でDestroyされるため、ここでは何もしない
    }
    
    int GetAbilityDamage(CardData card)
    {
        foreach(var ab in card.abilities)
        {
            if (ab.effect == EffectType.DAMAGE)
            {
                int val = ab.value;
                if (card.type == CardType.SPELL)
                {
                    Transform board = isPlayerTurn ? GameObject.Find("PlayerBoard").transform : enemyBoard;
                    if (board) foreach(var u in board.GetComponentsInChildren<UnitMover>()) val += u.spellDamageBonus;
                    if (activeBuilds != null) foreach(var b in activeBuilds) if (b.isPlayerOwner == isPlayerTurn) foreach(var a in b.data.abilities) if (a.effect == EffectType.SPELL_DAMAGE_PLUS) val += a.value;
                }
                return val;
            }
        }
        return 0;
    }

    void ExecuteSpell(object manualTarget)
    {
        // 1. Online Sync
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        if (isOnline)
        {
            var gameState = FindObjectOfType<GameStateController>();
            if (gameState != null)
            {
                 // ターゲット不要なスペルの場合も、Hostに通知して効果を同期させる
                 // 列情報を取得して送る
                 int col = lastTargetX;
                 if (col == -1) col = GetColumnUnderMouse();
                 
                 gameState.RPC_RequestPlaySpell(currentCastingCard.cardData.id, default, false, col);
                 
                 // Local Cleanup (Visual)
                 hand.Remove(currentCastingCard.cardData);
                 Destroy(currentCastingCard.gameObject);
                 CleanupSpellMode();
                 return; 
            }
        }

        // オフラインまたは非同期実行
        TryUseMana(currentCastingCard.cardData.cost);
        AbilityManager.instance.ProcessAbilities(currentCastingCard.cardData, EffectTrigger.SPELL_USE, null, manualTarget);
        AbilityManager.instance.TriggerSpellReaction(isPlayerTurn);
        PlayDiscardAnimation(currentCastingCard.cardData, true);
        BroadcastLog($" {currentCastingCard.cardData.cardName} を唱えた", true, currentCastingCard.cardData);
        Destroy(currentCastingCard.gameObject);
        CleanupSpellMode();
        PlaySE(seSummon);
    }

    public void StartUnitTargeting(CardView card, Transform slot)
    {
        if (currentMana < card.cardData.cost)
        {
            // Debug.Log("No Mana");
            return;
        }
        currentCastingCard = card;
        pendingSummonSlot = slot;
        arrowStartPos = slot.position; 
        isTargetingMode = true;
        ShowArrow(arrowStartPos);
        SetArrowColor(Color.white);
        ShowUnitDetail(card.cardData);
        card.transform.position = slot.position;
        card.GetComponent<CanvasGroup>().alpha = 0.5f; 
    }

    public bool StartSpellCast(CardView card)
    {
        if (currentMana < card.cardData.cost) 
        { 
             // Warning feedback
             return false; 
        }
        currentCastingCard = card; 
        card.transform.SetParent(spellCastCenter); 
        card.transform.localPosition = Vector3.zero;
        arrowStartPos = spellCastCenter.position;
        if (CheckIfSpellNeedsTarget(card.cardData)) 
        { 
            isTargetingMode = true; 
            ShowArrow(arrowStartPos); 
            SetArrowColor(Color.white); 
            ShowUnitDetail(card.cardData); 
        }
        else 
        { 
            if (card.cardData.type == CardType.SPELL) ExecuteSpell(null); 
            else if (card.cardData.type == CardType.BUILD) ExecuteBuild();
            else CancelSpellCast(); 
        }
        return true;
    }

    void ExecuteBuild()
    {
        if (!TryUseMana(currentCastingCard.cardData.cost)) 
        {
            CancelSpellCast();
            return;
        }

        // 1. Construct Locally
        ConstructBuildByEffect(currentCastingCard.cardData.id, true);

        // 2. Online Sync
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        if (isOnline)
        {
            var gameState = FindObjectOfType<GameStateController>();
            if (gameState != null)
            {
                // Send RPC to others (Sender handled locally)
                // Note: The RPC takes 'isPlayerOwner'.
                gameState.RPC_ConstructBuild(currentCastingCard.cardData.id, true);
            }
        }

        // 3. Visual & Cleanup
        PlayDiscardAnimation(currentCastingCard.cardData, true);
        BroadcastLog($" {currentCastingCard.cardData.cardName} を建築した", true, currentCastingCard.cardData);
        
        Destroy(currentCastingCard.gameObject);
        CleanupSpellMode();
        PlaySE(seSummon);
    }

    void TryCastSpellToTarget(GameObject targetObj)
    {
        UnitMover unit = targetObj.GetComponentInParent<UnitMover>(); 
        Leader leader = targetObj.GetComponentInParent<Leader>(); 
        object target = (unit != null) ? (object)unit : leader;
        
        if (target != null) 
        { 
            if (currentCastingCard.cardData.type == CardType.UNIT) 
            {
                SummonPlayerUnit(target); 
            }
            else 
            {
                // ★Network対応: スペル発動
                bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
                if (isOnline)
                {
                    if (!TryUseMana(currentCastingCard.cardData.cost)) return;
                    
                    var gameState = FindObjectOfType<GameStateController>();
                    if (gameState != null)
                    {
                         // Resolve Target ID
                         NetworkId targetUnitsId = default;
                         bool isTargetLeader = false;
                         
                         if (unit != null && unit.Object != null) targetUnitsId = unit.Object.Id;
                         else if (leader != null)
                         {
                             // Leader has no NetworkId.
                             // We use isTargetLeader=true.
                             // Host logic will deduce target based on Card Data (e.g. Damage -> Enemy Leader).
                             isTargetLeader = true;
                         }
                         
                         // Send RPC with current column
                         int col = GetColumnUnderMouse();
                         gameState.RPC_RequestPlaySpell(currentCastingCard.cardData.id, targetUnitsId, isTargetLeader, col);
                         
                         // Local Cleanup
                         hand.Remove(currentCastingCard.cardData);
                         // Destroy Visual
                         Destroy(currentCastingCard.gameObject);
                         CleanupSpellMode();
                         return;
                    }
                }
                
                // オフライン
                ExecuteSpell(target); 
            }
        }
    }

    void SummonPlayerUnit(object manualTarget)
    {
        CardData card = currentCastingCard.cardData;
        if (!TryUseMana(card.cost)) return;
        Transform targetSlot = pendingSummonSlot;
        if (targetSlot == null || targetSlot.childCount > 0)
        {
            Transform playerBoard = GameObject.Find("PlayerBoard").transform;
            if (playerBoard != null) { foreach(Transform slot in playerBoard) { if (slot.childCount == 0) { targetSlot = slot; break; } } }
        }
        if (targetSlot == null || targetSlot.childCount > 0) { Debug.Log("空きスロットがありません"); CancelSpellCast(); return; }
        GameObject newUnit = SpawnUnit(playerUnitPrefab != null ? playerUnitPrefab : unitPrefabForEnemy, targetSlot);
        UnitView view = newUnit.GetComponent<UnitView>(); if (view != null) view.SetUnit(card);
        UnitMover mover = newUnit.GetComponent<UnitMover>(); 
        if (mover != null) 
        {
            // Host側で生成した場合、所有権(InputAuthority)が自分(Host)と一致するか確認
            // ローカル召喚は常に自分(Player)のユニット
            mover.Initialize(card, true);
            
            // ★追加：スロット座標を設定（Mirroring用）
            SlotInfo info = targetSlot.GetComponent<SlotInfo>();
            if (info != null)
            {
                // Only set Networked Vars if Online/Spawned
                if (mover.Object != null && mover.Object.IsValid)
                {
                    mover.NetworkedSlotX = info.x;
                    mover.NetworkedSlotY = info.y;
                }
            } 
        }
        AbilityManager.instance.ProcessAbilities(card, EffectTrigger.ON_SUMMON, mover, manualTarget);
        mover.PlaySummonAnimation();
        BroadcastLog($" {card.cardName} を召喚した", true, card);
        PlaySE(seSummon);

        // ★修正：手札データから確実に削除
        if (hand.Contains(card)) hand.Remove(card);
        
        // ★修正：ビジュアル破棄
        if (currentCastingCard != null) Destroy(currentCastingCard.gameObject);
        
        CleanupSpellMode();
    }

    public void CancelSpellCast() 
    { 
        if (currentCastingCard != null) 
        { 
            currentCastingCard.transform.SetParent(handArea); 
            currentCastingCard.transform.localScale = Vector3.one; 
            currentCastingCard.transform.localPosition = Vector3.zero; 
            var g = currentCastingCard.GetComponent<CanvasGroup>(); 
            if(g) 
            { 
                g.blocksRaycasts=true; 
                g.alpha=1f; 
            } 
            var d=currentCastingCard.GetComponent<Draggable>(); 
            if(d) d.originalParent=handArea; 
        } 
        CleanupSpellMode(); 
    }

    void CleanupSpellMode() 
    { 
        isTargetingMode = false; 
        currentCastingCard = null; 
        pendingSummonSlot = null; 
        HideArrow(); 
        HideTooltip(); 
        OnClickCloseDetail(); 
    }

    int GetSlotY(Transform slot) 
    { 
        var info = slot.GetComponent<SlotInfo>(); 
        return info ? info.y : -1; 
    }



    public bool HasSelectTargetAbility(CardData card) 
    { 
        return card.abilities.Any(a => a.target.ToString().StartsWith("SELECT")); 
    }

    bool CheckIfSpellNeedsTarget(CardData data) 
    { 
        return data.abilities.Any(ab => ab.target.ToString().StartsWith("SELECT")); 
    }

    void UseEnemyCard(CardData card) 
    { 
        enemyCurrentMana -= card.cost; 
        UpdateEnemyManaUI(); 
        enemyHandData.Remove(card); 
        if (enemyHandArea.childCount > 0) Destroy(enemyHandArea.GetChild(0).gameObject); 
        UpdateDeckGraveyardVisuals(); 
    }
    #endregion

    #region Building System
    public void OpenBuildMenu() 
    { 
        if (buildUIManager != null) 
        { 
            if (buildUIManager.gameObject.activeSelf) buildUIManager.CloseMenu(); 
            else buildUIManager.OpenMenu(true); 
        } 
    }

    public void OpenBuildMenu(bool isPlayer) 
    { 
        if (buildUIManager != null) 
        { 
            buildUIManager.OpenMenu(isPlayer); 
        } 
    }

    public void BuildConstruction(int buildIndex) 
    { 
        if (HasActiveBuild(true)) 
        { 
            Debug.Log("既に建築物があります"); 
            return; 
        } 
        if (playerBuildCooldown > 0) 
        { 
            Debug.Log("クールタイム中です"); 
            return; 
        } 
        if (buildIndex >= playerLoadoutBuilds.Count) return; 

        CardData data = playerLoadoutBuilds[buildIndex]; 
        if (TryUseMana(data.cost)) 
        { 
            if (activeBuilds == null) activeBuilds = new List<ActiveBuild>(); 
            activeBuilds.Add(new ActiveBuild(data, true)); 
            playerBuildCooldown = maxPlayerBuildCooldown; 
            Debug.Log(data.cardName + " の建築を開始します..."); 
            PlaySE(seSummon); 
            UpdateBuildUI(); 

            // ★追加: RPCを送信
            var gameState = FindObjectOfType<GameStateController>();
            if (gameState != null && gameState.Object != null)
            {
                gameState.RPC_ConstructBuild(data.id, true);
            }
        } 
    }

    // ★追加: RPC受信用の建築処理
    public void ConstructBuildByEffect(string cardId, bool isPlayer)
    {
        // PlayerDataManagerからカード取得
        CardData data = PlayerDataManager.instance.GetCardById(cardId);
        if (data == null)
        {
             // Fallback
             data = Resources.Load<CardData>("Cards/" + cardId);
        }
        if (data == null) return;

        if (activeBuilds == null) activeBuilds = new List<ActiveBuild>();
        activeBuilds.Add(new ActiveBuild(data, isPlayer));
        UpdateBuildUI();
    }

    public bool HasActiveBuild(bool isPlayer) 
    { 
        if (activeBuilds == null) return false; 
        foreach (var build in activeBuilds) 
        { 
            if (build.isPlayerOwner == isPlayer) return true; 
        } 
        return false; 
    }

    public ActiveBuild GetActiveBuild(bool isPlayer) 
    { 
        if (activeBuilds == null) return null; 
        foreach (var build in activeBuilds) 
        { 
            if (build.isPlayerOwner == isPlayer) return build; 
        } 
        return null; 
    }

    public void PreviewMana(int cost) 
    { 
        if (playerManaCrystals == null) return; 
        int startIndex = currentMana - cost; 
        for (int i = 0; i < playerManaCrystals.Count; i++) 
        { 
            if (i >= startIndex && i < currentMana) playerManaCrystals[i].color = Color.yellow; 
            else if (i < currentMana) playerManaCrystals[i].color = Color.white; 
        } 
    }

    public void ResetManaPreview() 
    { 
        UpdateManaUI(); 
    }

    public void ShowBuildDetail(CardData data, int currentLife = -1) 
    { 
        if (enlargedCardPanel == null || enlargedCardView == null || data == null) return; 

        enlargedCardPanel.SetActive(true); 
        if (enlargedCardView.nameText != null) enlargedCardView.nameText.text = data.cardName; 
        if (enlargedCardView.descText != null) enlargedCardView.descText.text = data.description; 
        if (enlargedCardView.costText != null) enlargedCardView.costText.text = data.cost.ToString(); 

        if (enlargedCardView.iconImage != null) 
        { 
            if (data.cardIcon != null) 
            { 
                enlargedCardView.iconImage.sprite = data.cardIcon; 
                enlargedCardView.iconImage.color = Color.white; 
            } 
        } 

        int life = (currentLife != -1) ? currentLife : data.duration; 
        if (enlargedCardView.healthText != null) enlargedCardView.healthText.text = life.ToString(); 
        if (enlargedCardView.attackText != null) enlargedCardView.attackText.text = ""; 

        if (enlargedCardView.attackOrbImage) enlargedCardView.attackOrbImage.gameObject.SetActive(false); 
        if (enlargedCardView.healthOrbImage) enlargedCardView.healthOrbImage.gameObject.SetActive(true); 
        if (enlargedCardView.buildTypeIcon) enlargedCardView.buildTypeIcon.gameObject.SetActive(true); 
    }
    #endregion

    #region UI & Feedback
    public void GameEnd(bool isPlayerWin) 
    { 
        if (resultPanel != null && resultPanel.activeSelf) return; 
        if (isGameEnded) return; // Prevent multiple calls
        isGameEnded = true;

        if (resultPanel != null) resultPanel.SetActive(true); 

        GameObject bgm = GameObject.Find("BGM Player"); 
        if(bgm != null) bgm.GetComponent<AudioSource>().Stop(); 

        if (resultText != null) 
        { 
            if (isPlayerWin) 
            { 
                resultText.text = "勝利！"; 
                resultText.color = Color.yellow; 
                PlaySE(seWin); 
            } 
            else 
            { 
                resultText.text = "敗北..."; 
                resultText.color = Color.red; 
                PlaySE(seLose); 
            } 
        } 

        // Button Toggle
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        bool isRandomMatch = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.IsRandomMatch;

        // ★修正: リザルトボタンの重複表示を防ぐため、一度すべて非表示にしてから必要なものを表示
        if (retryButton) retryButton.SetActive(false);
        if (returnToRoomButton) returnToRoomButton.SetActive(false);

        if (isOnline)
        {
            // オンライン時は両方のボタンを表示できるが、配置が重ならないようにするか、
            // ユーザーの意図に合わせて適切にラベルを設定
            if (retryButton) 
            {
                retryButton.SetActive(true);
                var txt = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = isRandomMatch ? "もう一度" : "再戦";
            }

            if (returnToRoomButton) 
            {
                returnToRoomButton.SetActive(true);
                var txt = returnToRoomButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = isRandomMatch ? "メニューへ" : "ルームに戻る";
            }
        }
        else
        {
            // オフライン時はリトライのみ表示
            if (retryButton) retryButton.SetActive(true);
        }

        isPlayerTurn = false; 
    }

    public void OnClickRetry() 
    { 
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
        
        if (isOnline)
        {
            // Online Retry
            NetworkConnectionManager.instance.RestartRandomMatch();
        }
        else
        {
            // Offline Retry
            SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
        }
    }

    // [NEW] Online: Back to Menu
    public void OnClickReturnToRoom()
    {
        // User requested "Return to Menu"
        bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;

        if (isOnline)
        {
             NetworkConnectionManager.instance.DisconnectAndGoToMenu();
        }
        else
        {
             SceneManager.LoadScene("MenuScene");
        }
    }

    public void RestartGame()
    {
        // ★FIX: If offline, reload SampleScene. If Online, go back to Menu or Lobby? 
        // For simplicity and user request, ensuring we can play again.
        if (FindObjectOfType<GameStateController>() == null)
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene"); // Reload scene for online too? Or Menu?
    }

    public void OnClickMenu() 
    { 
        SceneManager.LoadScene("MenuScene"); 
    }

    public void OnClickCloseDetail() 
    { 
        if (enlargedCardPanel != null) enlargedCardPanel.SetActive(false); 
    }

    public void ShowUnitDetail(CardData data) 
    { 
        if (enlargedCardPanel == null || enlargedCardView == null || data == null) return; 
        enlargedCardPanel.SetActive(true); 
        enlargedCardView.SetCard(data); 
    }

    public void ShowArrow(Vector3 s) 
    { 
        if (currentArrow) currentArrow.Show(s); 
    }

    public void UpdateArrow(Vector3 s, Vector3 c) 
    { 
        if (currentArrow) currentArrow.UpdatePosition(s, c); 
    }

    public void HideArrow() 
    { 
        if (currentArrow) currentArrow.Hide(); 
    }

    public void SetArrowColor(Color c) 
    { 
        if (currentArrow) currentArrow.SetColor(c); 
    }

    public void SetArrowLabel(string t, bool v) 
    { 
        if(currentArrow) currentArrow.SetLabel(t,v); 
    }

    public bool IsArrowActive 
    { 
        get { return currentArrow != null && currentArrow.gameObject.activeSelf; } 
    }

    public void ShowTooltip(string t) 
    { 
        if (simpleTooltip) simpleTooltip.Show(t); 
    }

    public void HideTooltip() 
    { 
        if (simpleTooltip) simpleTooltip.Hide(); 
    }

    public void SpawnDamageText(Vector3 p, int d) 
    { 
        if(floatingTextPrefab)
        { 
            // Ensures text is spawned on an active canvas layer
            Transform t = (effectCanvasLayer != null && effectCanvasLayer.gameObject.activeInHierarchy) ? effectCanvasLayer : null;
            if (t == null)
            {
                if (handArea != null) t = handArea.parent;
                else t = GameObject.Find("Canvas")?.transform;
            }

            if(t)
            { 
                var o = Instantiate(floatingTextPrefab, t); 
                o.transform.position = p + new Vector3(0, 50, 0); 
                FloatingText ft = o.GetComponent<FloatingText>();
                if (ft != null) ft.Setup(d);
                else Debug.LogError($"[GameManager] FloatingText component missing on prefab: {floatingTextPrefab.name}");
            }
        }
    }

    public void PlaySE(AudioClip c)
    { 
        if(c&&audioSource) audioSource.PlayOneShot(c); 
    }
    #endregion

    #region Core Helpers & Setup
    public bool TryUseMana(int cost) 
    { 
        if (!isPlayerTurn) return false; 
        if (currentMana >= cost) 
        { 
            currentMana -= cost; 
            UpdateManaUI(); 
            return true; 
        } 
        return false; 
    }

    public void UpdateManaUI() 
    { 
        // if (playerManaText != null) playerManaText.GetComponent<TextMeshProUGUI>().text = $"Mana: {currentMana}/{maxMana}"; 
        
        // Ensure crystal list is sufficient
        int displayCount = Mathf.Max(maxMana, currentMana);
        if (playerManaCrystals != null)
        {
             // Add missing crystals if needed
             while (playerManaCrystals.Count < displayCount)
             {
                 // Clone the first one or create new? Assuming first exists effectively
                 GameObject newObj = Instantiate(playerManaCrystals[0].gameObject, playerManaCrystals[0].transform.parent);
                 playerManaCrystals.Add(newObj.GetComponent<Image>());
             }

             for (int i = 0; i < playerManaCrystals.Count; i++) 
             { 
                 if (playerManaCrystals[i] == null) continue; 
                 playerManaCrystals[i].color = Color.white; 
                 var crystalUI = playerManaCrystals[i].GetComponent<ManaCrystalUI>(); 
                 if (crystalUI == null) crystalUI = playerManaCrystals[i].gameObject.AddComponent<ManaCrystalUI>(); 
                 
                 // Visibility: Show up to displayCount. 
                 bool isVisible = i < displayCount;
                 playerManaCrystals[i].gameObject.SetActive(isVisible);
                 
                 if (isVisible)
                 {
                     bool isActive = (i < currentMana); 
                     // Logic: Normal Mana (0 to maxMana-1) gets "On" or "Off".
                     //        Extra Mana (maxMana to currentMana-1) gets "Coin".
                     
                     Sprite onSprite = manaOnSprite;
                     
                     // If this slot is beyond normal maxMana, it's a temporary Coin slot
                     if (i >= maxMana)
                     {
                         if (manaCoinSprite != null) onSprite = manaCoinSprite;
                     }

                     crystalUI.SetState(isActive, onSprite, manaOffSprite);
                 }
             } 
        } 
        UpdateHandState(); 
    }

    // ★ NEW: Centralized Mana Gain with Visual Feedback (Crystal Animation)
    public void GainMana(int amount, bool isPlayer)
    {
        if (isPlayer)
        {
            int oldMana = currentMana;
            currentMana = Mathf.Min(currentMana + amount, maxMana + 10); 
            
            UpdateManaUI();
            if (amount > 0) StartCoroutine(AnimateManaCrystalGain(oldMana, amount));
        }
        else
        {
            enemyCurrentMana += amount;
            UpdateEnemyManaUI();
        }
    }

    System.Collections.IEnumerator AnimateManaCrystalGain(int startIndex, int count)
    {
        if (playerManaCrystals == null) yield break;

        // Collect targets (Newly active crystals)
        List<Transform> targets = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            int idx = startIndex + i;
            if (idx >= 0 && idx < playerManaCrystals.Count && playerManaCrystals[idx] != null)
            {
                targets.Add(playerManaCrystals[idx].transform);
            }
        }

        if (targets.Count == 0) yield break;
        
        // Scale Pulse Animation
        Vector3 originalScale = Vector3.one;
        float duration = 0.5f;
        float elapsed = 0f;
        
        // Pop up effect
        while(elapsed < duration)
        {
            float t = elapsed / duration;
            // Sine wave pulse
            float scale = Mathf.Lerp(1f, 1.8f, Mathf.Sin(t * Mathf.PI)); 
            
            foreach(var tObj in targets)
            {
                if (tObj != null) tObj.localScale = originalScale * scale;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Reset
        foreach(var tObj in targets)
        {
             if (tObj != null) tObj.localScale = originalScale;
        }
    }

    public void UpdateEnemyManaUI() 
    { 
        if (enemyManaText != null) enemyManaText.GetComponent<TextMeshProUGUI>().text = $"Mana: {enemyCurrentMana}/{enemyMaxMana}"; 
        
        int displayCount = Mathf.Max(enemyMaxMana, enemyCurrentMana);
        if (enemyManaCrystals != null) 
        { 
             while (enemyManaCrystals.Count < displayCount)
             {
                 if (enemyManaCrystals.Count > 0 && enemyManaCrystals[0] != null)
                 {
                     GameObject newObj = Instantiate(enemyManaCrystals[0].gameObject, enemyManaCrystals[0].transform.parent);
                     enemyManaCrystals.Add(newObj.GetComponent<Image>());
                 }
                 else break; // Safety
             }

            for (int i = 0; i < enemyManaCrystals.Count; i++) 
            { 
                if (enemyManaCrystals[i] == null) continue; 
                enemyManaCrystals[i].color = Color.white; 
                var crystalUI = enemyManaCrystals[i].GetComponent<ManaCrystalUI>(); 
                if (crystalUI == null) crystalUI = enemyManaCrystals[i].gameObject.AddComponent<ManaCrystalUI>(); 
                
                bool isVisible = i < displayCount;
                enemyManaCrystals[i].gameObject.SetActive(isVisible);

                if (isVisible)
                {
                    bool isActive = (i < enemyCurrentMana); 
                    
                    Sprite onSprite = manaOnSprite;
                    // Extra Mana -> Coin
                    if (i >= enemyMaxMana)
                    {
                        if (manaCoinSprite != null) onSprite = manaCoinSprite;
                    }

                    crystalUI.SetState(isActive, onSprite, manaOffSprite); 
                }
            } 
        } 
    }

    void UpdateHandState()
    {
        if(!handArea)return; 
        foreach(Transform c in handArea)
        {
            var v=c.GetComponent<CardView>(); 
            if(v&&v.cardData!=null)
            { 
                bool p=(currentMana>=v.cardData.cost); 
                if(!isPlayerTurn)p=false; 
                v.SetPlayableState(p);
            }
        }
    }

    void SetupBoard(Transform board, bool isEnemy) 
    { 
        if (board == null) return; 
        int index = 0; 
        foreach (Transform slot in board) 
        { 
            SlotInfo info = slot.gameObject.GetComponent<SlotInfo>(); 
            if (info == null) info = slot.gameObject.AddComponent<SlotInfo>(); 
            
            // ★User Defined Coordinate System
            // Layout: 6 slots. 3 Cols x 2 Rows.
            // Indices forms vertical pairs: [0,1], [2,3], [4,5]
            // Code x = Col (User y). Code y = Row (User x).
            
            int col = index / 2; // Col 0, 1, 2
            info.x = col; 
            
            if (!isEnemy) 
            {
                // Player: 0->Front(y0), 1->Back(y1)
                info.y = index % 2; 
            }
            else 
            {
                // Enemy: 0->Back(y1), 1->Front(y0)
                // User says: Enemy Index 1 is x0 (Front?). Enemy Index 0 is x1 (Back?).
                info.y = 1 - (index % 2); 
            }
            
            info.isEnemySlot = isEnemy; 
            DropPlace dropPlace = slot.GetComponent<DropPlace>(); 
            if (dropPlace != null) dropPlace.isEnemySlot = isEnemy; 
            index++; 
        } 
    }

    public void UpdateBuildUI() 
    { 
        if (playerBuildArea != null) foreach (Transform child in playerBuildArea) Destroy(child.gameObject); 
        if (enemyBuildArea != null) foreach (Transform child in enemyBuildArea) Destroy(child.gameObject); 

        if (activeBuilds == null) return; 

        foreach (var build in activeBuilds) 
        { 
            Transform parent = build.isPlayerOwner ? playerBuildArea : enemyBuildArea; 
            if (parent != null && buildIconPrefab != null) 
            { 
                GameObject iconObj = Instantiate(buildIconPrefab, parent); 
                BuildView view = iconObj.GetComponent<BuildView>(); 
                if (view != null) 
                { 
                    view.SetBuild(build); 
                    if (!build.isPlayerOwner) 
                    { 
                        view.SetFlip(true); 
                    } 
                } 
            } 
        } 
    }

    void SetupLeaderIcon() 
    { 
        if (PlayerDataManager.instance != null) 
        { 
            var data = PlayerDataManager.instance.playerData; 
            if (data.decks != null && data.decks.Count > 0) 
            { 
                int deckIndex = data.currentDeckIndex; 
                if (deckIndex >= 0 && deckIndex < data.decks.Count) 
                { 
                    int jobIndex = (int)data.decks[deckIndex].deckJob; 
                    if (leaderIcons != null && jobIndex < leaderIcons.Length && leaderIcons[jobIndex] != null) 
                    { 
                        if (playerLeader != null) 
                        { 
                            var leader = playerLeader.GetComponent<Leader>();
                            if (leader != null)
                            {
                                leader.SetIcon(leaderIcons[jobIndex]);
                            }
                        } 
                    } 
                } 
            } 
        } 
    }

    void SetupDeck() 
    { 
        mainDeck.Clear(); 
        playerLoadoutBuilds.Clear(); 

        if (PlayerDataManager.instance != null) 
        { 
            var data = PlayerDataManager.instance.playerData; 
            if (data.decks != null && data.decks.Count > 0) 
            { 
                if (data.currentDeckIndex < 0 || data.currentDeckIndex >= data.decks.Count) data.currentDeckIndex = 0; 
                var currentDeck = data.decks[data.currentDeckIndex]; 

                foreach (string cardId in currentDeck.cardIds) 
                { 
                    CardData card = PlayerDataManager.instance.GetCardById(cardId); 
                    if (card != null) mainDeck.Add(card); 
                } 
                if (currentDeck.buildIds != null) 
                { 
                    foreach (string buildId in currentDeck.buildIds) 
                    { 
                        CardData build = PlayerDataManager.instance.GetCardById(buildId); 
                        if (build != null) playerLoadoutBuilds.Add(build); 
                    } 
                } 
            } 
        } 
        ShuffleDeck();
    }

    public void ShuffleDeck()
    {
        // System.Randomを使う (UnityEngine.RandomはPhoton/Fusionで同期される可能性があるため)
        // Guidを使って、クライアントごとにユニークなシードを生成する
        var rng = new System.Random(System.Guid.NewGuid().GetHashCode());

        for (int i = 0; i < mainDeck.Count; i++)
        {
            CardData temp = mainDeck[i];
            int r = rng.Next(i, mainDeck.Count);
            mainDeck[i] = mainDeck[r];
            mainDeck[r] = temp;
        }
    }

    void DecreaseBuildDuration(bool isPlayer) 
    { 
        if (activeBuilds == null) return; 

        for (int i = activeBuilds.Count - 1; i >= 0; i--) 
        { 
            ActiveBuild build = activeBuilds[i]; 
            // Host Authority: Decrease duration and Sync
            if (activeBuilds[i].isPlayerOwner == isPlayer && !build.isUnderConstruction)
            {
                 // Only Host processes logic
                 bool isOnline = NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null && NetworkConnectionManager.instance.Runner.IsRunning;
                 bool isAuth = isOnline ? (NetworkConnectionManager.instance.Runner.IsServer || NetworkConnectionManager.instance.Runner.IsSharedModeMasterClient) : true;

                 if (isAuth)
                 {
                     build.remainingTurns--;
                     // Sync to everyone (including Host UI update via RPC or local)
                     // If Online, use RPC. If Offline, local update is effectively done by referencing object? No, struct is value type!
                     // We must update the list.
                     
                     // If destroyed
                     if (build.remainingTurns <= 0) 
                     { 
                         activeBuilds.RemoveAt(i); 
                         if (isOnline) 
                         {
                             var gameState = FindObjectOfType<GameStateController>();
                             if (gameState) gameState.RPC_UpdateBuildDurability(i, 0, true); // 0 = destroy
                         }
                         Debug.Log(build.data.cardName + " の効果が切れました"); 
                     }
                     else
                     {
                         // Just Update count
                         activeBuilds[i] = build; // Update struct in list
                         if (isOnline)
                         {
                             var gameState = FindObjectOfType<GameStateController>();
                             if (gameState) gameState.RPC_UpdateBuildDurability(i, build.remainingTurns, false);
                         }
                     }
                 }
            }
        } 
        UpdateBuildUI(); 
    }

    bool ExistsActiveTaunt(Transform board) 
    { 
        if(!board)return false; 
        foreach(Transform s in board)
        { 
            if(s.childCount>0)
            { 
                var u=s.GetChild(0).GetComponent<UnitMover>(); 
                if(u&&u.IsTauntActive&&!u.hasStealth) return true;
            }
        } 
        return false; 
    }

    public bool CanAttackUnit(UnitMover attacker, UnitMover target) 
    { 
        if(target.hasStealth) return false; 
        Transform tb=target.transform.parent.parent; 

        if(ExistsActiveTaunt(tb))
        { 
            if(!target.IsTauntActive||target.hasStealth) return false; 
        } 

        SlotInfo ts=target.transform.parent.GetComponent<SlotInfo>(); 
        if(!ts) return true; 

        if(ts.y==1)
        { 
            foreach(Transform s in tb)
            { 
                SlotInfo i=s.GetComponent<SlotInfo>(); 
                if(i.x==ts.x&&i.y==0&&s.childCount>0)
                { 
                    var f=s.GetChild(0).GetComponent<UnitMover>(); 
                    if(f&&!f.hasStealth) return false; 
                } 
            } 
        } 
        return true; 
    }

    public bool CanAttackLeader(UnitMover attacker) 
    { 
        Transform tb=attacker.isPlayerUnit?enemyBoard:GameObject.Find("PlayerBoard").transform; 
        if(!tb)return true; 

        if(ExistsActiveTaunt(tb)) return false; 

        bool[] b=new bool[3]; 
        foreach(Transform s in tb)
        { 
            SlotInfo i=s.GetComponent<SlotInfo>(); 
            if(i&&s.childCount>0)
            { 
                var u=s.GetChild(0).GetComponent<UnitMover>(); 
                if(u&&!u.hasStealth) b[i.x]=true; 
            } 
        } 
        if(b[0]&&b[1]&&b[2]) return false; 
        return true; 
    }

    bool CheckTargetValidity(EffectTarget targetType, UnitMover unit, Leader leader) 
    { 
        if (unit != null) 
        { 
            // Stealth check: Enemy cannot target Stealth unit
            // Note: Assuming 'isPlayerTurn' is true for the caster. 
            // Ideally we need 'source' context, but here we assume Local Player is casting.
            bool isEnemyUnit = !unit.isPlayerUnit;
            if (isEnemyUnit && unit.hasStealth)
            {
                // Can only target if effect explicitly allows stealth (rare)
                // Standard rule: Stealth = Untargetable by enemy
                return false;
            }

            if (targetType == EffectTarget.SELECT_ENEMY_UNIT || targetType == EffectTarget.SELECT_ANY_ENEMY) return !unit.isPlayerUnit; 
            if (targetType == EffectTarget.SELECT_UNDAMAGED_ENEMY) 
            { 
                return !unit.isPlayerUnit && (unit.health >= unit.maxHealth); 
            } 
            if (targetType == EffectTarget.SELECT_DAMAGED_ENEMY) return !unit.isPlayerUnit && (unit.health < unit.maxHealth); 
            if (targetType == EffectTarget.SELECT_ALLY_UNIT) return unit.isPlayerUnit; 
            if (targetType == EffectTarget.SELECT_ANY_UNIT) return true; 
        } 
        else if (leader != null) 
        { 
            bool isEnemyLeader = (leader.transform.parent.name == "EnemyBoard" || leader.name == "EnemyInfo"); 
            if (targetType == EffectTarget.SELECT_ENEMY_LEADER || targetType == EffectTarget.SELECT_ANY_ENEMY) return isEnemyLeader; 
        } 
        return false; 
    }

    GameObject GetObjectUnderMouse() 
    { 
        PointerEventData p = new PointerEventData(EventSystem.current); 
        p.position = Input.mousePosition; 
        List<RaycastResult> r = new List<RaycastResult>(); 
        EventSystem.current.RaycastAll(p, r); 
        if (r.Count > 0) return r[0].gameObject; 
        return null; 
    }

    public int GetColumnUnderMouse()
    {
        GameObject obj = GetObjectUnderMouse();
        if (obj != null)
        {
            SlotInfo info = obj.GetComponentInParent<SlotInfo>();
            if (info != null) return info.x;
        }
        return -1;
    }
    #endregion

    // ■ [NEW] ユニット生成の共通ヘルパー (Network/Local両対応)
    public GameObject SpawnUnit(GameObject prefab, Transform parent, PlayerRef? owner = null)
    {
        var runner = NetworkConnectionManager.instance != null ? NetworkConnectionManager.instance.Runner : null;
        
        // オンラインかつRunnerが有効で、PrefabにNetworkObjectがある場合
        // かつ、SharedモードではローカルプレイヤーがAuthorityを持つのでSpawn可能
        if (runner != null && runner.IsRunning && prefab.GetComponent<NetworkObject>() != null)
        {
             // Host Authority: Always Host spawns.
             PlayerRef inputAuth = owner.HasValue ? owner.Value : runner.LocalPlayer;
             NetworkObject no = runner.Spawn(prefab, Vector3.zero, Quaternion.identity, inputAuth);
             
             // Parent Sync needs RPC or Networked Property?
             // UnitMover SyncParentSlot handles it via NetworkedSlotX/Y.
             // But we should set parent locally for instant feedback?
             // No, wait for spawn or set directly if Host.
             no.transform.SetParent(parent, false);
             no.transform.localPosition = Vector3.zero;
             
             return no.gameObject;
        }
        else
        {
            // Offline
            return Instantiate(prefab, parent);
        }
    }
    
    // ■ [NEW] 手札から特定IDのカードを取得（最初に見つかったもの）
    public CardData GetCardFromHand(string cardId, bool isPlayer)
    {
        List<CardData> targetHand = isPlayer ? hand : enemyHandData;
        // 特定のインスタンスではなくIDで照合して最初の1枚を返す
        return targetHand.FirstOrDefault(c => c.id == cardId);
    }


    // ★Host Authority Logic: ユニットプレイ処理 (Host executes for both P1 and P2)
    public void ProcessOnlinePlayUnit(string cardId, int slotIndex, bool isHostAction, PlayerRef sender)
    {
        // 1. Identify Card Data
        CardData card = isHostAction ? GetCardFromHand(cardId, true) : GetCardFromHand(cardId, false);
        // Note: GetCardFromHand for Enemy (Guest) works on "enemyHandData" list.

        if (card == null) 
        {
            // If internal data missing, fallback to generic load (but stats might desync?)
            if (PlayerDataManager.instance != null) card = PlayerDataManager.instance.GetCardById(cardId);
            if (card == null) card = Resources.Load<CardData>("Cards/" + cardId);
            if (card == null) { Debug.LogError($"[GameManager] ProcessOnlinePlayUnit Failed: Card {cardId} not found."); return; }
        }

        // 2. Consume Mana & Hand (Logic Only)
        // Host Action: Consume my mana, remove from my hand
        // Guest Action: Consume enemy mana (dummy?), remove from enemy hand data
        
        if (isHostAction)
        {
             if (!TryUseMana(card.cost)) return;
             // ★修正：二重削除防止
             if (hand.Contains(card)) hand.Remove(card);
             // Destroy UI Object corresponding to this card (Visual Sync)
             DestroyHandCardVisual(card, true);
        }
        else
        {
             // Guest Action
             if (enemyCurrentMana < card.cost) 
             {
                 // Desync? Force consume anyway strictly? OR Validate?
                 Debug.LogWarning("[GameManager] Guest tried to play card but Host thinks insufficient mana. Allowing for sync.");
             }
             enemyCurrentMana = Mathf.Max(0, enemyCurrentMana - card.cost);
             UpdateEnemyManaUI();
             
             // Remove from enemyHandData
             CardData toRemove = enemyHandData.FirstOrDefault(c => c.id == cardId);
             if (toRemove != null) enemyHandData.Remove(toRemove);
             enemyDeckCount = Mathf.Max(0, enemyDeckCount - 1); // Adjust count if tracked together? No, deck count is separate.
             
             // Visual Update
             UpdateEnemyHandVisuals(enemyHandData.Count);
        }
        
        // 3. Resolve Target Slot
        // isHostAction=true -> PlayerBoard. isHostAction=false -> EnemyBoard.
        // User Defined Slot Index logic needs to be robust. 
        // We have 6 slots. Order is important.
        Transform targetBoard = isHostAction ? GameObject.Find("PlayerBoard").transform : enemyBoard;
        Transform targetSlot = targetBoard.GetChild(slotIndex); // Assuming same hierarchy order
        
        // 4. Spawn Unit
        // Host must use Runner.Spawn to create Networked Object
        if (NetworkConnectionManager.instance != null && NetworkConnectionManager.instance.Runner != null)
        {
            NetworkRunner runner = NetworkConnectionManager.instance.Runner;
            GameObject prefab = isHostAction ? playerUnitPrefab : unitPrefabForEnemy;
            
            // Validate prefab has NetworkObject
            if (prefab.GetComponent<NetworkObject>() != null)
            {
                // Spawn with Authority to the owner?
                // Shared Mode: Host spawns, gives Authority to Owner?
                // Host Authority Mode (Our Goal): Host spawns, Host keeps Authority (usually).
                // But for "Input Authority", Fusion uses Client Ownership.
                // In "Host/Server" Topology: Server has State Auth, Client has Input Auth.
                // In "Shared" Topology: Spawner has State Auth.
                
                // If we are migrating to Host Authority Model on Shared Mode:
                // Host (Master Client) should spawn and keep State Authority.
                // Guest effectively controls it via RPCs to Host.
                
                // [FIX] Always spawn as Host (State Authority) so we can initialize Networked Properties
                PlayerRef owner = runner.LocalPlayer; 
                NetworkObject no = runner.Spawn(prefab, Vector3.zero, Quaternion.identity, owner);
                
                // [FIX] Assign Input Authority to the actual sender (e.g. Guest)
                if (sender != runner.LocalPlayer)
                {
                    no.AssignInputAuthority(sender);
                }
                
                // Parent Setup
                no.transform.SetParent(targetSlot, false);
                no.transform.localPosition = Vector3.zero;
                
                // Initialize Unit Data
                // Host decides ownership context.
                // If I am Host and I spawned my unit -> isOwner=true
                // If I am Host and I spawned Enemy(Guest) unit -> isOwner=false
                bool isMyUnit = (sender == runner.LocalPlayer);
                UnitMover mover = no.GetComponent<UnitMover>();
                if (mover != null)
                {
                    mover.Initialize(card, isMyUnit);
                    
                // Host sets initial network state
                if (no.HasStateAuthority)
                {
                     mover._netCardId = card.id;
                     mover.OwnerPlayer = sender; 
                     
                     // ★FIX logic: Always use PlayerBoard to resolve X,Y from index.
                     // The Sender (Guest) clicked on *their* PlayerBoard.
                     // The Index corresponds to PlayerBoard layout.
                     // We must resolve X,Y from PlayerBoard, then set it.
                     // Previously we used targetSlot (Host's perspective), which might be EnemyBoard with reversed Y (Rotation).
                     
                     Transform refBoard = GameObject.Find("PlayerBoard").transform;
                     if (slotIndex >= 0 && slotIndex < refBoard.childCount)
                     {
                         SlotInfo info = refBoard.GetChild(slotIndex).GetComponent<SlotInfo>();
                         if (info != null)
                         {
                             mover.NetworkedSlotX = info.x;
                             mover.NetworkedSlotY = info.y;
                         }
                     }
                }
                }
                    // ON_PLAY ability?
                    AbilityManager.instance.ProcessAbilities(card, EffectTrigger.ON_SUMMON, mover);
                }
            }
        }
    // ★Host Authority Logic: スペルプレイ処理
    public void ProcessOnlinePlaySpell(string cardId, NetworkId targetUnitId, bool isTargetLeader, bool isHostAction, int targetColumn = -1)
    {
         // 1. Identify Card
         CardData card = isHostAction ? GetCardFromHand(cardId, true) : GetCardFromHand(cardId, false);
         if (card == null) 
         {
             if (cardId == "ManaCoin")
             {
                 // ★Virtual Card Generation for ManaCoin
                 card = ScriptableObject.CreateInstance<CardData>();
                 card.id = "ManaCoin";
                 card.cardName = "マナコイン";
                 card.cost = 0;
                 card.type = CardType.SPELL;
                 card.abilities = new List<CardAbility>();
                 CardAbility ability = new CardAbility();
                 ability.effect = EffectType.GAIN_MANA;
                 ability.value = 1;
                 ability.trigger = EffectTrigger.SPELL_USE;
                 ability.target = EffectTarget.SELF;
                 card.abilities.Add(ability);
             }
             else
             {
                 if (PlayerDataManager.instance != null) card = PlayerDataManager.instance.GetCardById(cardId);
                 if (card == null) card = Resources.Load<CardData>("Cards/" + cardId);
                 // If still null, log error
                 if (card == null) { Debug.LogError($"[GameManager] PlaySpell Failed: Card {cardId} not found"); return; }
             }
         }

         // 2. Consume Mana/Hand
         if (isHostAction)
         {
             if (!TryUseMana(card.cost)) return;
             if (hand.Contains(card)) hand.Remove(card);
             DestroyHandCardVisual(card, true);
         }
         else
         {
             enemyCurrentMana = Mathf.Max(0, enemyCurrentMana - card.cost);
             UpdateEnemyManaUI();
             CardData toRemove = enemyHandData.FirstOrDefault(c => c.id == cardId);
             if (toRemove != null) enemyHandData.Remove(toRemove);
             UpdateEnemyHandVisuals(enemyHandData.Count);
         }
          
          // 3. Resolve Target
          this.lastTargetX = targetColumn; // ★Set target column context
          object target = null;
         if (isTargetLeader)
         {
              // Check "Who is the Enemy of the Caster?"
              // AbilityManager checks "isPlayerSide" of Caster.
              // Host Caster (Player) -> Target: EnemyLeader or PlayerLeader?
              // The RPC boolean "isTargetLeader" just says "A leader was targeted".
              // Usually spells target SOME leader.
              // Logic usually relies on "EffectTarget" enum (ENEMY_LEADER, PLAYER_LEADER, etc.).
              // But "SELECT_ENEMY_LEADER" implies manual selection.
              // We need to know WHICH leader.
              
              // Simplification: In this game, SELECT usually targets "The Leader I clicked".
              // If Host clicked:
              //   - Enemy Leader (Dummy) -> True Target: P2 Leader
              //   - Player Leader -> True Target: P1 Leader
              
              // We need to know which one.
              // Current implementation of RPC: (string cardId, NetworkId targetUnitId, bool isTargetLeader)
              // It lacks "Which Leader".
              // BUT, currently Draggable/GameManager only allows targeting Valid Targets.
              // If `SELECT_ENEMY_LEADER`, we target the enemy.
              // If `SELECT_ALLY_LEADER`, we target self.
              // AbilityManager `ResolveTargetUnit` usually takes an Object.
              // Let's resolve closest logical target based on Card Data `target` type?
              // No, user might select specifically.
              // But strictly speaking, there are only 2 leaders.
              // If I am P1, and I cast "Damage", I target P2 Leader.
              // If I cast "Heal", I might target P1 Leader.
              // We need to pass "IsEnemyLeader" flag in RPC?
              
              // For now, let's assume standard "Offensive Spell targets Enemy, Defensive targets Self".
              // Or better: Pass `bool targetIsEnemyLeader` in RPC.
              // I defined `bool isTargetLeader`.
              // I should have defined `bool isTargetEnemyLeader` (Relative to Caster).
              
              // Let's rely on CardData.
              bool targetsEnemy = card.abilities.Any(a => a.target == EffectTarget.SELECT_ENEMY_LEADER || a.target == EffectTarget.SELECT_ANY_ENEMY || a.target == EffectTarget.ENEMY_LEADER);
              
              if (targetsEnemy)
              {
                  // Target is Caster's Enemy
                  target = isHostAction ? (object)GameObject.Find("EnemyInfo").GetComponent<Leader>() : (object)GameObject.Find("PlayerInfo").GetComponent<Leader>();
              }
              else
              {
                  // Target is Caster's Self
                  // Wait, "PlayerInfo" is ALWAYS Local Player (Host).
                  // If Guest Caster (isHostAction=false):
                  //   Enemy of Guest = Host (PlayerInfo).
                  //   Self of Guest = Host's "EnemyInfo" (Dummy).
                  
                  // Correct Mapping:
                  // Host Action:
                  //   Enemy -> EnemyInfo
                  //   Self -> PlayerInfo
                  // Guest Action:
                  //   Enemy -> PlayerInfo
                  //   Self -> EnemyInfo
                  
                  target = isHostAction ? 
                      (targetsEnemy ? GameObject.Find("EnemyInfo").GetComponent<Leader>() : GameObject.Find("PlayerInfo").GetComponent<Leader>()) :
                      (targetsEnemy ? GameObject.Find("PlayerInfo").GetComponent<Leader>() : GameObject.Find("EnemyInfo").GetComponent<Leader>());
              }
         }
         else if (targetUnitId.IsValid)
         {
             NetworkObject no = NetworkConnectionManager.instance.Runner.FindObject(targetUnitId);
             if (no != null) target = no.GetComponent<UnitMover>();
         }
         
         // 4. Execute Ability
         // Need a way to ProcessAbilities with "Offline/Host" context.
         // Warning: AbilityManager uses `GameManager.isPlayerTurn` to determine side?
         // We should fix AbilityManager to accept "Source Unit" or "IsPlayerSide" explicit arg.
         // `CastSpell` calls `ProcessAbilities`.
         // `ProcessAbilities` uses `GameManager.instance.isPlayerTurn` (Global) if sourceUnit is null.
         // We must override `isPlayerTurn` or modify AbilityManager?
         // In Host Authority, `isPlayerTurn` IS accurate (Host tracks turn).
         // So if it is Player Turn, Host is casting. If Enemy Turn, Guest is casting.
         // So `isPlayerTurn` check in AbilityManager is OK!
         
         AbilityManager.instance.ProcessAbilities(card, EffectTrigger.SPELL_USE, null, target);
         AbilityManager.instance.TriggerSpellReaction(isHostAction);
         
         // Log
         // Broadcast Log via RPC if online
         BroadcastLog($" {card.cardName} を唱えた", isHostAction, card);
    }

    private void DestroyHandCardVisual(CardData card, bool isPlayer)
    {
        Transform area = isPlayer ? handArea : enemyHandArea;
        if (!area) return;
        
        // 1. 指定されたエリア内を探す
        foreach(Transform t in area)
        {
            var cv = t.GetComponent<CardView>();
            if (cv && cv.cardData == card) 
            { 
                Destroy(t.gameObject); 
                return; 
            }
        }

        // 2. ★修正：ドラッグ中の可能性を考慮し、ルートからも探す
        if (isPlayer)
        {
            // ヒエラルキー全体からこのCardDataを持つCardViewを探す（ただし盤面に出ていないもの）
            CardView[] allViews = FindObjectsOfType<CardView>();
            foreach(var v in allViews)
            {
                // 手札エリア外（ドラッグ中）かつ、データが一致するもの
                if (v.cardData == card && (v.transform.parent == null || v.transform.parent.root == v.transform.root))
                {
                    Destroy(v.gameObject);
                    return;
                }
            }
        }
    }
    
    private PlayerRef GetEnemyRef()
    {
        var runner = NetworkConnectionManager.instance.Runner;
        foreach(var p in runner.ActivePlayers) if (p != runner.LocalPlayer) return p;
        return PlayerRef.None;
    }
}

[System.Serializable]
public class ActiveBuild
{
    public CardData data; 
    public int remainingTurns;
    public bool isPlayerOwner;
    public bool isUnderConstruction;
    public bool hasActed = false;

    public ActiveBuild(CardData data, bool isPlayer)
    {
        this.data = data;
        this.remainingTurns = data.duration;
        this.isPlayerOwner = isPlayer;
        this.isUnderConstruction = true;
        this.hasActed = false;
    }
}