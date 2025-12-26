using UnityEngine;
using Fusion;
using System.Linq; // Added for OrderBy
using System.Collections.Generic;

// ゲームの進行(ターン管理など)を同期する NetworkBehaviour
public class GameStateController : NetworkBehaviour
{
    // 現在のターンプレイヤー
    [Networked] public PlayerRef ActivePlayer { get; set; }

    // ターン数
    [Networked] public int TurnCount { get; set; }

    // ゲーム開始フラグ
    [Networked] public bool IsGameStarted { get; set; }

    // 先攻プレイヤー (Late Joiner用)
    [Networked] public PlayerRef FirstPlayer { get; set; }

    public override void Spawned()
    {
        // 初期化: まだゲーム開始しない
        if (Object.HasStateAuthority)
        {
            TurnCount = 0;
            IsGameStarted = false;
        }
        Debug.Log($"[GameState] Spawned. Active Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}. IsServer: {Runner.IsServer}, IsSharedModeMaster: {Runner.IsSharedModeMasterClient}");
    }

    private int _logTimer = 0;
    public override void FixedUpdateNetwork()
    {
        // _logTimer++; // Timer moved to Render
        /*
        if (_logTimer % 60 == 0) // Log once per second approx
        {
             // Debug.Log($"[GameState] FUN Running. Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, IsGameStarted: {IsGameStarted}");
        }
        */

        // プレイヤー人数チェック (ホストのみ)
        if (Object.HasStateAuthority && !IsGameStarted)
        {
            if (Runner.SessionInfo.PlayerCount >= 2)
            {
                StartGame();
            }
        }

        // CheckTurnUpdate(); // Moved to Render to ensure execution on Proxy
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Debug.Log($"[GameState] Despawned. Active Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
    }

    void OnDestroy()
    {
        Debug.LogWarning($"[GameState] OnDestroy Called! Active Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
    }

    public override void Render()
    {
        CheckTurnUpdate();
        CheckJobUpdate();
    }

    // State Tracking
    private int _lastEnemyJobId = 0;
    private PlayerRef _lastActivePlayer = PlayerRef.None;
    private int _lastTurnCount = 0;
    private bool _descTurn1RPCReceived = false;
    private PlayerRef _predictedActivePlayer = PlayerRef.None;

    void CheckJobUpdate()
    {
        // Simple polling for Enemy Job ID changes or just rely on manual trigger?
        // Since we removed Player1JobId props, this polling logic needs to check NPC.
        // Or we can assume GameManager polls it.
        // For simplicity: Check Enemy NPC Job ID change.
        // ★修正：有効なNetworkObjectのみを対象にし、現在のRunnerに属するもの、最新のものを取得する
        var enemyPC = NetworkPlayerController.Instances.LastOrDefault(pc => 
            pc != null &&
            pc.Object != null && 
            pc.Object.IsValid && 
            pc.Runner == Runner &&
            pc.Owner != Runner.LocalPlayer);

        if (enemyPC != null)
        {
             int currentEnemyJob = enemyPC.JobId;
             if (currentEnemyJob != _lastEnemyJobId)
             {
                 _lastEnemyJobId = currentEnemyJob;
            
            // UI Update
            if (GameManager.instance != null)
            {
                if (currentEnemyJob != 0) GameManager.instance.SetEnemyLeaderIcon(currentEnemyJob);
            }
        }
    }
    }


    void OnTurnChanged()
    {
        // 自分がActivePlayerなら「自分のターン」
        bool isMyTurn = (Runner.LocalPlayer == ActivePlayer);
        
        Debug.Log($"[GameState] Turn Changed. Active: {ActivePlayer} (ID:{ActivePlayer.PlayerId}), Local: {Runner.LocalPlayer} (ID:{Runner.LocalPlayer.PlayerId}), IsMyTurn: {isMyTurn}");

        // Fallback: Check by ID if direct comparison fails
        if (!isMyTurn && Runner.LocalPlayer.PlayerId == ActivePlayer.PlayerId)
        {
             Debug.LogWarning("[GameState] PlayerRef comparison failed but IDs match! Forcing isMyTurn = true.");
             isMyTurn = true;
        }
        
        if (GameManager.instance != null)
        {
            if (isMyTurn)
            {
                 GameManager.instance.StartPlayerTurn();
            }
            else
            {
                GameManager.instance.OnOnlineEnemyTurnStart();
            }
        }
    }

    // ターン終了時に呼ぶ
    public void EndTurn()
    {
        if (Object.HasStateAuthority)
        {
            // Host logic (executed locally)
            if (GameManager.instance != null)
            {
                 bool isHostTurn = (ActivePlayer == Runner.LocalPlayer);
                 GameManager.instance.ProcessOnlineTurnEnd(isHostTurn);
            }
            ToggleTurn();
        }
        else
        {
            RPC_RequestEndTurn();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEndTurn()
    {
        // Host executes effects for Current Turn Player
        if (GameManager.instance != null)
        {
             // Determine if ending turn for Host or Guest
             bool isHostTurn = (ActivePlayer == Runner.LocalPlayer);
             GameManager.instance.ProcessOnlineTurnEnd(isHostTurn);
        }
        ToggleTurn();
    }

    void ToggleTurn()
    {
        // プレイヤーリストをID順にソートして取得 (安定した順序にするため)
        var sortedPlayers = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();

        if (sortedPlayers.Count == 0) return;

        // 現在のプレイヤーのインデックスを探す
        int currentIndex = sortedPlayers.IndexOf(ActivePlayer);

        // 次のプレイヤーへ (見つからない場合は -1 + 1 = 0 で最初の人になる)
        int nextIndex = (currentIndex + 1) % sortedPlayers.Count;

        ActivePlayer = sortedPlayers[nextIndex];
        TurnCount++;
        // Debug.Log($"[GameState] Turn Toggled. New Active: {ActivePlayer}"); // Removed Debug.Log
    }


    // ★Networked HP & Counts (For Sync)
    // [Removed Monolithic Stats: Moved to NetworkPlayerController]

    // ★追加: スペル発動同期用RPC
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)] // Logic runs ONLY on Host
    public void RPC_CastSpell(string cardId, int seed, NetworkId targetUnitId, bool isTargetEnemyLeader, RpcInfo info = default)
    {
        // RNG Sync
        UnityEngine.Random.InitState(seed);
        
        // Load Card Data
        CardData card = null;
        if (PlayerDataManager.instance != null) card = PlayerDataManager.instance.GetCardById(cardId);
        if (card == null) card = Resources.Load<CardData>("Cards/" + cardId);
        if (card == null) return;
        
        // Resolve Target
        object target = null;
        if (targetUnitId.IsValid)
        {
             NetworkObject netObj = Runner.FindObject(targetUnitId);
             if (netObj != null) target = netObj.GetComponent<UnitMover>();
        }
        else if (isTargetEnemyLeader)
        {
             // Sender was targeting "Enemy Leader".
             // We need to map this to P1/P2 Leader based on who sent the RPC.
             // If P1 sent it, target is P2. If P2 sent it, target is P1.
             
             // Simple Logic: If Sender == ActivePlayer, then target is the OTHER player.
             PlayerRef sender = info.Source;
             
             // We can find Player1/Player2 refs from Runner.ActivePlayers (assuming 2 players)
             // But simpler: just apply effect to "Opponent of Sender".
             // Since we have Logic on Host, we can manipulate P1_Hp / P2_Hp directly?
             // Or let AbilityManager handle it. 
             // AbilityManager needs "Target Object". 
             // We can pass the actual Leader GameObject (Local Host Object).
             
             bool senderIsP1 = (sender == Runner.ActivePlayers.OrderBy(p=>p.PlayerId).First());
             // Target is P2 Leader if P1 cast, P1 Leader if P2 cast.
             
             // However, AbilityManager expects "Leader" component.
             // On Host, we have PlayerLeader (Host's Leader) and EnemyInfo (Guest's Leader Representation).
             // If Host is P1: P1=PlayerLeader, P2=EnemyInfo.
             // If P1 cast (Host): Target is P2 (EnemyInfo).
             // If P2 cast (Guest): Target is P1 (PlayerLeader).
             
             if (Runner.LocalPlayer == sender) target = GameObject.Find("EnemyInfo")?.GetComponent<Leader>();
             else target = GameObject.Find("PlayerInfo")?.GetComponent<Leader>();
        }

        // Execute Logic (Host Only)
        if (target != null) AbilityManager.instance.ProcessAbilities(card, EffectTrigger.SPELL_USE, null, target);
        else AbilityManager.instance.ProcessAbilities(card, EffectTrigger.SPELL_USE, null);

        AbilityManager.instance.TriggerSpellReaction(target == null); 

        // Broadcast Visuals back to everyone
        RPC_ShowSpellVisual(cardId, targetUnitId, isTargetEnemyLeader, info.Source);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ShowSpellVisual(string cardId, NetworkId targetUnitId, bool isTargetEnemyLeader, PlayerRef actor)
    {
        CardData card = null;
        if (PlayerDataManager.instance != null) card = PlayerDataManager.instance.GetCardById(cardId);
        if (card == null) card = Resources.Load<CardData>("Cards/" + cardId);
        if (card == null) return;

        bool isMyAction = (Runner.LocalPlayer == actor);
        
        GameManager.instance.PlayDiscardAnimation(card, isMyAction);
        
        if (BattleLogManager.instance != null) 
        {
            string actorName = isMyAction ? "自分" : "敵";
            BattleLogManager.instance.AddLog($"{actorName}は {card.cardName} を唱えた", isMyAction, card);
        }
        GameManager.instance.PlaySE(GameManager.instance.seSummon);
    }

    // ★Sync Draw (Notify others I drew)
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SyncDraw(PlayerRef actor, int count)
    {
        if (actor == Runner.LocalPlayer) return; // I drew, so I ignore.
        
        // Others (Enemy)
        if (GameManager.instance != null)
        {
             GameManager.instance.EnemyDrawCard(count, false);
        }
    }

    // Update Networked HP from Logic
    // Update Networked HP from Logic
    public void UpdateNetworkHealth(bool isPlayer1, int newHp)
    {
        if (Object.HasStateAuthority)
        {
            var sorted = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            int index = isPlayer1 ? 0 : 1;
            if (index < sorted.Count)
            {
                var pc = NetworkPlayerController.Get(sorted[index]);
                if (pc != null) pc.CurrentHp = newHp;
            }
        }
    }
    
    public void UpdateNetworkCounts(bool isPlayer1, int hand, int deck, int grave)
    {
        if (Object.HasStateAuthority)
        {
            var sorted = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            int index = isPlayer1 ? 0 : 1;
            if (index < sorted.Count)
            {
                var pc = NetworkPlayerController.Get(sorted[index]);
                if (pc != null)
                {
                    pc.HandCount = hand;
                    pc.DeckCount = deck;
                    pc.GraveCount = grave;
                }
            }
        }
    }
    // 先攻プレイヤー (Coin Tossで決定)
    // [Removed duplicate FirstPlayer]
    
    // マリガン完了フラグ
    [Networked] public NetworkBool IsP1MulliganDone { get; set; }
    [Networked] public NetworkBool IsP2MulliganDone { get; set; }

    // [Removed Monolithic Job IDs]

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitJobId(int jobId, RpcInfo info = default)
    {
        // Identify if Sender is P1 or P2
        // Update specific player's JobID
        var pc = NetworkPlayerController.Get(info.Source);
        if (pc != null)
        {
            pc.JobId = jobId;
        }
        
        Debug.Log($"[GameState] Received JobID {jobId} from {info.Source}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_FinishMulligan(PlayerRef player)
    {
        // プレイヤーID順で判定 (ホストがP1とは限らないが、通常ID小=P1)
        var sorted = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
        if (sorted.Count > 0 && sorted[0] == player) IsP1MulliganDone = true;
        else if (sorted.Count > 1 && sorted[1] == player) IsP2MulliganDone = true;
        
        Debug.Log($"[GameState] Mulligan Done: {player}. P1:{IsP1MulliganDone} P2:{IsP2MulliganDone}");
    }

    // ゲーム開始時: マリガン待機
    private void StartGame()
    {
        // ルームシーンなど、ゲームシーン以外では開始ロジックを走らせない
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
        {
            return;
        }

        // 先攻後攻をランダムに決定するためにActivePlayersを確認
        var players = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
        
        // アクティブなプレイヤーが2人揃っているか確認 (SessionInfoだけでなく実体も必要)
        if (players.Count >= 2)
        {
            IsGameStarted = true; // ここで初めて開始フラグを立てる
            TurnCount = 0; 
            
            int r = UnityEngine.Random.Range(0, 2);
            FirstPlayer = players[r];
            ActivePlayer = FirstPlayer; // 最初のターンプレイヤー
            Debug.Log($"[GameState] Coin Toss! First Player: {FirstPlayer}");
            
            // 全クライアントにマリガン開始を通知
            RPC_StartMulligan(FirstPlayer);
        }
        else
        {
            // まだ揃っていない (SessionInfoは2でもSimulationが追いついていない場合など)
            // 次のフレームで再トライさせるため、何もしない
             Debug.Log($"[GameState] Waiting for ActivePlayers sync... (Count: {players.Count})");
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartMulligan(PlayerRef firstPlayer)
    {
        Debug.Log($"[GameState] RPC_StartMulligan Called. First Player: {firstPlayer}. Checking GameManager instance.");
        if (GameManager.instance != null)
        {
            Debug.Log("[GameState] GameManager.instance found. invoking StartMulliganSequence.");
            // 自分が先攻か後攻か判定
            bool amIFirst = (Runner.LocalPlayer == firstPlayer);
            int drawCount = amIFirst ? 3 : 4;
            
            // コイン演出などを出すならここ
            // GameManagerのマリガン開始
            GameManager.instance.StartMulliganSequence(drawCount, amIFirst);
        }
        else
        {
            Debug.LogError("[GameState] CRITICAL: GameManager.instance is NULL! Cannot start Mulligan.");
        }
    }

    void CheckTurnUpdate()
    {
        /*
        if (_logTimer % 60 == 0)
        {
             // Debug.Log($"[GameState] Heartbeat: Started={IsGameStarted} Turn={TurnCount} Active={ActivePlayer} LastActive={_lastActivePlayer} Local={Runner.LocalPlayer}");
        }
        */

        if (!IsGameStarted)
    {
        if (_descTurn1RPCReceived && _logTimer % 60 == 0)
        {
             Debug.LogWarning($"[GameState] CheckTurnUpdate Skipped. IsGameStarted is FALSE. (Turn1RPCReceived={_descTurn1RPCReceived})");
        }
        return;
    }

        // マリガン完了待ち
        // ネットワークラグ対策: RPC受信済みなら TurnCount=0 でも 1扱いする
        // Fusionは毎フレームNetworked変数を正規の状態にリセットするため、まだ来ていないなら毎回上書きが必要
        // ★修正: ActivePlayerが None または 予測されたプレイヤー(P1) と一致する場合も強制適用する
        // (ActivePlayerだけ同期されて TurnCountが0のままの場合、デッドロックになるのを防ぐ)
        if (_descTurn1RPCReceived && TurnCount == 0 && (ActivePlayer == PlayerRef.None || ActivePlayer == _predictedActivePlayer))
        {
             TurnCount = 1;
             ActivePlayer = _predictedActivePlayer;
             IsGameStarted = true;
             IsP1MulliganDone = true;
             IsP2MulliganDone = true;
             // Debug.Log($"[GameState] Re-applying prediction: Turn=1 Active={ActivePlayer}");
        }

        if (TurnCount == 0)
        {
            if (IsP1MulliganDone && IsP2MulliganDone)
            {
                TurnCount = 1;
                ActivePlayer = FirstPlayer; 
                Debug.Log($"[GameState] All Mulligan Done! Start Turn 1. Active: {ActivePlayer}");
                if (Object.HasStateAuthority) RPC_StartTurn1(ActivePlayer);
            }

            // Guest also needs to know we are waiting here (Check flag to suppress wait if we know turn started)
            if (!_descTurn1RPCReceived && _logTimer % 300 == 0) // Reduced frequency (once per 5s)
            {
                // Debug.Log($"[GameState] Waiting for Turn 1... Turn={TurnCount} P1Mul:{IsP1MulliganDone} P2Mul:{IsP2MulliganDone}");
            }
            return;
        }

        // Check for Turn Change
        // ★修正: ActivePlayerの変化だけでなく、TurnCountの変化も監視する
        // (ネットワークラグで ActivePlayerの変更パケットが落ちて、同じプレイヤーの手番が連続したように見えた場合でもターン切替を検知する)
        if (_lastActivePlayer != ActivePlayer || _lastTurnCount != TurnCount)
        {
            Debug.Log($"[GameState] Turn Change Detected! From {_lastActivePlayer} to {ActivePlayer}. TurnCount: {_lastTurnCount} -> {TurnCount}");
            _lastActivePlayer = ActivePlayer;
            _lastTurnCount = TurnCount;
            OnTurnChanged(); // UI Update

            // ★FIX: Visualize Enemy Turn Start (Draw/Mana) locally
            if (ActivePlayer != Runner.LocalPlayer && ActivePlayer != PlayerRef.None && GameManager.instance != null)
            {
                 // Opponent Turn Started effectively
                 Debug.Log($"[GameState] Enemy Turn Started. Visualizing Draw/Mana.");
                 // 1. Draw Animation (Standard Turn Draw = 1)
                 GameManager.instance.EnemyDrawCard(1);
                 // 2. Mana Update? (Usually handled by Networked var sync, but EnemyManaUI might need refresh)
                 // Note: EnemyDrawCard will animate hand and count.
                 // Actual Mana value Sync happens via RPC_UpdatePlayerStats.
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartTurn1(PlayerRef activePlayer)
    {
        Debug.Log($"[GameState] RPC_StartTurn1 Received. Active: {activePlayer}");
        
        // If we are the Guest (Proxy), we might receive this RPC before the Networked Vars (TurnCount/IsP1MulliganDone) have replicated.
        // We TRUST the Host's signal here and force the local state to proceed.
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning($"[GameState] Forcing Local State Update via RPC_StartTurn1 (Guest Prediction). Active: {activePlayer}");
            _descTurn1RPCReceived = true; // Set local flag
            _predictedActivePlayer = activePlayer; // Store for re-application
            
            TurnCount = 1;
            ActivePlayer = activePlayer;
            // ★修正: ゲーム開始フラグも強制的に立てる (同期遅延で CheckTurnUpdate が止まるのを防ぐ)
            IsGameStarted = true;
            
            // Should also set Mulligan flags to true to ensure logic flow consistency if checked elsewhere
            IsP1MulliganDone = true;
            IsP2MulliganDone = true;
            
            // Force turn update check immediately to ensure UI/GameManager syncs NOW
             if (_lastActivePlayer != ActivePlayer)
             {
                  // Debug.Log($"[GameState] RPC Prediction causing immediate Turn Change! From {_lastActivePlayer} to {ActivePlayer}");
                  _lastActivePlayer = ActivePlayer;
                  OnTurnChanged();
             }
        }
    }

    // Cleaned up comment


    // ★追加: 強制ドローRPC (相手にドローさせる効果用)
    // ★追加: 強制ドローRPC (相手にドローさせる効果用)
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ForceDraw(PlayerRef targetPlayer, int count)
    {
        if (Runner.LocalPlayer == targetPlayer)
        {
            if (GameManager.instance != null)
            {
                // Debug.Log($"[GameState] Force Draw {count} cards via RPC.");
                GameManager.instance.DealCards(count);
            }
        }
        else
        {
            // 他人のドロー: 敵の手札が増える演出を行う
            if (GameManager.instance != null)
            {
                 // 自分がObserverで、TargetがEnemyなら「EnemyDrawCard」を呼ぶ
                 // ターゲットが自分でないなら、それは「敵」とみなして良い（2人対戦前提）
                 GameManager.instance.EnemyDrawCard(count);
            }
        }
    }

    // (Duplicate RPC_ConstructBuild removed)

    // ★追加: リタイア用RPC
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_Resign(PlayerRef resigningPlayer)
    {
        Debug.Log($"[GameState] Player {resigningPlayer} resigned.");
        
        // Host updates the networked state to stop the game loop
        if (Object.HasStateAuthority)
        {
            IsGameStarted = false;
        }

        if (GameManager.instance != null)
        {
             // 自分が辞めたなら負け、相手が辞めたなら勝ち
             bool amIResigner = (Runner.LocalPlayer == resigningPlayer);
             GameManager.instance.GameEnd(!amIResigner);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastLog(string text, PlayerRef actor, string cardId = "")
    {
        // Log consistency check could be added here
        
        // Determine if this is "my" action or "enemy" action for color coding
        bool isPlayerAction = (actor == Runner.LocalPlayer);
        
        // Card data retrieval (optional, for hover tooltip)
        CardData card = null;
        if (!string.IsNullOrEmpty(cardId) && PlayerDataManager.instance != null)
        {
             card = PlayerDataManager.instance.GetCardById(cardId);
        }

        if (BattleLogManager.instance != null)
        {
            BattleLogManager.instance.AddLog(text, isPlayerAction, card);
        }
    }
    // ★修正: クライアントからホストへステータスを通知する
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_UpdatePlayerStats(NetworkId pcId, int handCount, int deckCount, int graveCount, int currentHp, int currentMana, int maxMana, int jobId, RpcInfo info = default)
    {
        // NetworkIdから直接オブジェクトを取得 (PlayerRef経由のGetより確実)
        var obj = Runner.FindObject(pcId);
        NetworkPlayerController pc = null;
        if (obj != null) pc = obj.GetComponent<NetworkPlayerController>();

        if (pc != null)
        {
            pc.HandCount = handCount;
            pc.DeckCount = deckCount;
            pc.GraveCount = graveCount;
            pc.CurrentHp = currentHp;
            pc.CurrentMana = currentMana;
            pc.MaxMana = maxMana;
            pc.JobId = jobId;

            // ★FIX: Force UI sync after stats update
            if (GameManager.instance != null) GameManager.instance.SyncNetworkInfo();
        }
        else
        {
            // ゲーム開始直後はスポーンラグで見つからないことがあるため、警告を限定する
            // NetworkId指定なら通常は見つかるはずだが、ラグでまだ届いていない可能性もある
            if (TurnCount > 0)
            {
                // Debug.LogWarning($"[GameState] Received Stats for ID {pcId} but object not found.");
            }
        }
    }
    // ★追加: ターン終了リクエストRPC
    // ★追加: ターン終了リクエストRPC
    // ★追加: ターン終了リクエストRPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_UpdateBuildDurability(int buildIndex, int remainingTurns, bool isDestroy)
    {
         // Host (StateAuthority) already applied logic locally. Ignore RPC echo.
         if (Object.HasStateAuthority) return;

         if (GameManager.instance == null) return;
         if (GameManager.instance.activeBuilds == null || buildIndex < 0 || buildIndex >= GameManager.instance.activeBuilds.Count) return;
         
         // Host manages the list, RPC syncs state by index
         if (isDestroy)
         {
             GameManager.instance.activeBuilds.RemoveAt(buildIndex);
             GameManager.instance.UpdateBuildUI();
         }
         else
         {
             var b = GameManager.instance.activeBuilds[buildIndex];
             b.remainingTurns = remainingTurns;
             GameManager.instance.activeBuilds[buildIndex] = b;
             GameManager.instance.UpdateBuildUI();
         }
    }
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEndTurn(RpcInfo info = default)
    {
        // Guard 1: Game must be started
        if (!IsGameStarted) return;
        
        // Guard 2: Mulligan must be done (TurnCount > 0)
        if (TurnCount == 0) return;

        // Guard 3: Must be Active Player
        if (ActivePlayer != info.Source) return;

        // Execute Turn Change
        if (GameManager.instance != null)
        {
             GameManager.instance.ProcessTurnEndEffects(ActivePlayer);
        }
        
        // Switch Active Player
        var sorted = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
        if (sorted.Count == 0) return;
        
        int currentIndex = sorted.IndexOf(ActivePlayer);
        int nextIndex = (currentIndex + 1) % sorted.Count;
        ActivePlayer = sorted[nextIndex];
        
        TurnCount++;
    }

    // ★追加: 建築同期用RPC (全員に送信)
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ConstructBuild(string buildId, bool isPlayerOwner, RpcInfo info = default)
    {
        if (GameManager.instance == null) return;
        
        // HostとGuestで視点が異なるため補正
        // RPCの送信者が自分なら isPlayerOwner そのまま。
        // 送信者が相手なら、isPlayerOwner の反転（自分から見て敵の建築）
        bool isMeRecipient = (info.Source == Runner.LocalPlayer);
        bool localIsPlayer = isMeRecipient ? isPlayerOwner : !isPlayerOwner;

        GameManager.instance.ConstructBuildByEffect(buildId, localIsPlayer);
    }

    // ★追加: 効果ダメージなどを相手リーダーに与えるためのRPC
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_DirectDamageToLeader(PlayerRef targetPlayer, int damage)
    {
        // 自分宛てならダメージを受ける
        if (Runner.LocalPlayer == targetPlayer)
        {
            if (GameManager.instance != null && GameManager.instance.playerLeader != null)
            {
                var leader = GameManager.instance.playerLeader.GetComponent<Leader>();
                if (leader != null) 
                {
                    Debug.Log($"[GameState] Received Direct Damage from Host: {damage}");
                    leader.TakeDamage(damage);
                }
            }
        }
    }

    // ★追加: カードプレイリクエストRPC
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestPlayUnit(string cardId, int slotIndex, NetworkId targetUnitId, bool isTargetLeader, RpcInfo info = default)
    {
        // 1. Validate (Is it sender's turn?)
        if (ActivePlayer != info.Source) 
        {
            Debug.LogWarning($"[GameState] Ignore PlayUnit from {info.Source} (Not Active Player)");
            return;
        }

        // 2. Execute Logic on Host
        if (GameManager.instance != null)
        {
            // isHostAction should be true if info.Source == Runner.LocalPlayer
            bool isHostAction = (info.Source == Runner.LocalPlayer);
            
            // ★FIX: Process play but DO NOT consume mana AGAIN if it's the Host's own action (already consumed locally)
            // If it's a Guest action (Host acting as Authority), then consumeMana should be true?
            // Wait, Guest already consumed mana locally. Host tracking should reflects Guest's mana.
            // Host tracking: enemyCurrentMana = Mathf.Max(0, enemyCurrentMana - card.cost); (Inside ProcessOnlinePlayUnit)
            // So for Guest, we pass 'isHostAction=false'.
            // For Host, we pass 'isHostAction=true', but consumeMana=false.
            
            GameManager.instance.ProcessOnlinePlayUnit(cardId, slotIndex, isHostAction, info.Source, targetUnitId, isTargetLeader, !isHostAction);
        }
    }
    // ★追加: 攻撃リクエストRPC
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestAttack(NetworkId attackerId, NetworkId targetId, bool toLeader, RpcInfo info = default)
    {
        if (ActivePlayer != info.Source) return;

        NetworkObject attackerObj = Runner.FindObject(attackerId);
        if (attackerObj == null) return;
        
        UnitMover attacker = attackerObj.GetComponent<UnitMover>();
        if (attacker == null) return;

        // Host Logic: Validate and Execute
        // 1. Validate
        if (!attacker.canAttack) return;
        
        // 2. Execute
        if (toLeader)
        {
             // Validate Target? GameManager.CanAttackLeader(attacker)
             if (GameManager.instance != null && GameManager.instance.CanAttackLeader(attacker)) 
             {
                 attacker.RPC_AttackLeader();
             }
        }
        else
        {
            NetworkObject targetObj = Runner.FindObject(targetId);
            if (targetObj != null)
            {
                UnitMover targetUnit = targetObj.GetComponent<UnitMover>();
                if (targetUnit != null && GameManager.instance != null && GameManager.instance.CanAttackUnit(attacker, targetUnit))
                {
                    // Call RPC_AttackUnit on Attacker
                    // Target Owner is needed for RPC arg?
                    // RPC_AttackUnit signature: (PlayerRef targetOwner, NetworkId targetId)
                    // The "TargetOwner" arg in RPC_AttackUnit seems to be for searching? 
                    // Let's check UnitMover again. It uses Runner.FindObject(targetId). Owner arg is unused or check?
                    // Actually UnitMover.RPC_AttackUnit uses `NetworkId targetId` to find object. 
                    // The first arg `PlayerRef targetOwner` might be legacy or for log? 
                    // Let's pass targetObj.InputAuthority ??
                     
                    attacker.RPC_AttackUnit(targetObj.InputAuthority, targetId);
                }
            }
        }
    }
    // ★追加: スペルプレイリクエストRPC
    // ★追加: スペルプレイリクエストRPC
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestPlaySpell(string cardId, NetworkId targetUnitId, bool isTargetLeader, int targetColumn = -1, RpcInfo info = default)
    {
        bool isHostAction = (info.Source == Runner.LocalPlayer);
        // ★FIX: Same as PlayUnit. Host already paid locally. Guest paid locally.
        // Host tracking of Guest mana is handled inside ProcessOnlinePlaySpell (enemyCurrentMana -= cost).
        GameManager.instance.ProcessOnlinePlaySpell(cardId, targetUnitId, isTargetLeader, isHostAction, targetColumn, !isHostAction);
    }
}
