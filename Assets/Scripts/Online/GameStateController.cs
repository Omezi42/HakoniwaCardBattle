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
        // _logTimer moved to Render for heartbeat debug (Removed for production cleanliness)
        // _logTimer++;
        
        CheckTurnUpdate();
        CheckJobUpdate();
    }

    // State Tracking
    private int _lastP1JobId = 0;
    private int _lastP2JobId = 0;
    private PlayerRef _lastActivePlayer = PlayerRef.None;
    private int _lastTurnCount = 0;
    private bool _descTurn1RPCReceived = false;
    private PlayerRef _predictedActivePlayer = PlayerRef.None;

    void CheckJobUpdate()
    {
        if (Player1JobId != _lastP1JobId || Player2JobId != _lastP2JobId)
        {
            _lastP1JobId = Player1JobId;
            _lastP2JobId = Player2JobId;
            
            // UI Update
            if (GameManager.instance != null)
            {
                // Identify which one is ME and which is ENEMY
                // Sorted list
                var sorted = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
                int myJob = -1;
                int enemyJob = -1;

                if (sorted.Count > 0)
                {
                    if (sorted[0] == Runner.LocalPlayer) { myJob = Player1JobId; enemyJob = Player2JobId; }
                    else if (sorted.Count > 1 && sorted[1] == Runner.LocalPlayer) { myJob = Player2JobId; enemyJob = Player1JobId; }
                }

                // Apply
                if (enemyJob != 0) GameManager.instance.SetEnemyLeaderIcon(enemyJob);
                // My job is usually set locally, but we can ensure sync if needed.
                // For now, only Enemy Job is requested to be synced.
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
        
        Debug.Log($"[GameState] Turn Toggled. New Active: {ActivePlayer}");
    }

    // ★追加: スペル発動同期用RPC
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_CastSpell(string cardId, int seed, NetworkId targetUnitId, bool isTargetEnemyLeader)
    {
        // RNG同期
        UnityEngine.Random.InitState(seed);
        
        // カードデータのロード
        CardData card = null;
        if (PlayerDataManager.instance != null)
        {
            card = PlayerDataManager.instance.GetCardById(cardId);
        }
        if (card == null)
        {
             card = Resources.Load<CardData>("Cards/" + cardId);
        }
        if (card == null) return;
        
        // ターゲットの特定
        object target = null;
        if (targetUnitId.IsValid)
        {
            NetworkObject netObj = Runner.FindObject(targetUnitId);
            if (netObj != null) target = netObj.GetComponent<UnitMover>();
        }
        else if (isTargetEnemyLeader)
        {
            // 発動者視点で「敵リーダー」
            // 受信者視点で「自分」が発動者ならEnemyInfo
            // 相手ならPlayerInfo
            
            // 簡易判定: GameStateController.ActivePlayer が発動者
            // (RPCが届くのは実行後なので、ターンが変わっていなければActivePlayer=発動者)
            bool amIActive = (Runner.LocalPlayer == ActivePlayer);
            
            if (amIActive) target = GameObject.Find("EnemyInfo")?.GetComponent<Leader>(); 
            else target = GameObject.Find("PlayerInfo")?.GetComponent<Leader>(); 
        }

        // スペル効果発動
        if (target != null)
        {
            AbilityManager.instance.ProcessAbilities(card, EffectTrigger.SPELL_USE, null, target);
        }
        else
        {
             // ターゲットなし
             AbilityManager.instance.ProcessAbilities(card, EffectTrigger.SPELL_USE, null);
        }

        AbilityManager.instance.TriggerSpellReaction(target == null ? true : false); // 簡易 reaction
        // GameStateControllerはNetworkBehaviourなのでRunnerが使える
        // アニメーション用
        bool isMyAction = (Runner.LocalPlayer == ActivePlayer); 
        GameManager.instance.PlayDiscardAnimation(card, isMyAction);
        
        if (BattleLogManager.instance != null) 
        {
            string actor = isMyAction ? "自分" : "敵";
            BattleLogManager.instance.AddLog($"{actor}は {card.cardName} を唱えた", isMyAction, card);
        }
        GameManager.instance.PlaySE(GameManager.instance.seSummon);
    }
    // 先攻プレイヤー (Coin Tossで決定)
    // [Removed duplicate FirstPlayer]
    
    // マリガン完了フラグ
    [Networked] public NetworkBool IsP1MulliganDone { get; set; }
    [Networked] public NetworkBool IsP2MulliganDone { get; set; }

    // Leader Job IDs
    [Networked] public int Player1JobId { get; set; }
    [Networked] public int Player2JobId { get; set; }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitJobId(int jobId, RpcInfo info = default)
    {
        // Identify if Sender is P1 or P2
        var sorted = Runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
        if (sorted.Count > 0 && sorted[0] == info.Source) Player1JobId = jobId;
        else if (sorted.Count > 1 && sorted[1] == info.Source) Player2JobId = jobId;
        
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
        Debug.Log($"[GameState] RPC_StartMulligan Called. First Player: {firstPlayer}. Checking GameManager instance...");
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
        if (_logTimer % 60 == 0)
        {
             // Debug.Log($"[GameState] CheckTurnUpdate. Started:{IsGameStarted} Turn:{TurnCount} Active:{ActivePlayer} LastActive:{_lastActivePlayer}");
             Debug.Log($"[GameState] Heartbeat: Started={IsGameStarted} Turn={TurnCount} Active={ActivePlayer} LastActive={_lastActivePlayer} Local={Runner.LocalPlayer}");
        }

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
            if (!_descTurn1RPCReceived && _logTimer % 60 == 0)
            {
                Debug.Log($"[GameState] Waiting for Turn 1... Turn={TurnCount} P1Mul:{IsP1MulliganDone} P2Mul:{IsP2MulliganDone}");
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
            OnTurnChanged();
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
                 Debug.Log($"[GameState] RPC Prediction causing immediate Turn Change! From {_lastActivePlayer} to {ActivePlayer}");
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
                Debug.Log($"[GameState] Force Draw {count} cards via RPC.");
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

    // ★追加: 建築同期用RPC
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ConstructBuild(string cardId, bool isPlayerOwner, RpcInfo info = default)
    {
        // 発動者が isPlayerOwner = true で送ってくる。
        // 受信側が「自分」なら isPlayerOwnerそのまま。
        // 受信側が「相手」なら、論理反転させる必要がある？
        
        // いや、RPCの引数は「発動者視点」で送るとややこしい。
        // ここでは「絶対的なOwnership」を送るべきだが、P1/P2の概念がないと難しい。
        
        // なので、Senderと比較する。
        bool isMine = (Runner.LocalPlayer == info.Source); // RPCを送ったのが自分ならTrue
        
        if (isMine) return; // 送信元は既に処理済み(投機実行)としているならリターン

        if (GameManager.instance != null)
        {
            // 相手が建てた = isPlayer=falseとして処理
            GameManager.instance.ConstructBuildByEffect(cardId, false);
        }
    }

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
}
