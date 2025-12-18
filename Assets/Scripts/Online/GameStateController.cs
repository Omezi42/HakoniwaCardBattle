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

    public override void Spawned()
    {
        // 初期化: まだゲーム開始しない
        if (Object.HasStateAuthority)
        {
            TurnCount = 0;
            IsGameStarted = false;
        }
    }

    public override void Render()
    {
        // プレイヤー人数チェック (ホストのみ)
        if (Object.HasStateAuthority && !IsGameStarted)
        {
            if (Runner.SessionInfo.PlayerCount >= 2)
            {
                StartGame();
            }
        }

        CheckTurnUpdate();
    }

    // Old methods removed
    
    private PlayerRef _lastActivePlayer = PlayerRef.None;


    void OnTurnChanged()
    {
        // 自分がActivePlayerなら「自分のターン」
        bool isMyTurn = (Runner.LocalPlayer == ActivePlayer);
        
        Debug.Log($"[GameState] Turn Changed. Active: {ActivePlayer}, IsMyTurn: {isMyTurn}");

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
        CardData card = Resources.Load<CardData>("Cards/" + cardId);
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
    // マリガン完了フラグ
    [Networked] public NetworkBool IsP1MulliganDone { get; set; }
    [Networked] public NetworkBool IsP2MulliganDone { get; set; }

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
        IsGameStarted = true; // ゲームセッション開始
        TurnCount = 0; // まだターンは始めない
        
        // UI側でマリガン開始
        // GameManager側で接続数検知してマリガンUI出す想定
    }

    void CheckTurnUpdate()
    {
        if (!IsGameStarted) return;

        // マリガン完了待ち
        if (TurnCount == 0)
        {
            if (Object.HasStateAuthority)
            {
                if (IsP1MulliganDone && IsP2MulliganDone)
                {
                    TurnCount = 1;
                    ActivePlayer = Runner.ActivePlayers.OrderBy(p => p.PlayerId).First();
                    Debug.Log($"[GameState] All Mulligan Done! Start Turn 1. Active: {ActivePlayer}");
                }
            }
            return;
        }

        if (_lastActivePlayer != ActivePlayer)
        {
            _lastActivePlayer = ActivePlayer;
            OnTurnChanged();
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
        
        // 自分が送った場合 -> isMine=true. BuildConstruction内でローカル処理済みなら二重になる？
        // GameManager側で「RPCから呼ばれたら追加」にするか、
        // 「ローカル実行時にRPCを送る」なら、自分はスキップする。
        
        if (isMine) return; // 送信元は既に処理済み(投機実行)としているならリターン

        if (GameManager.instance != null)
        {
            // 相手が建てた = isPlayer=falseとして処理
            GameManager.instance.ConstructBuildByEffect(cardId, false);
        }
    }
}
