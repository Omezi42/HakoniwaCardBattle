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

    private void StartGame()
    {
        IsGameStarted = true;
        TurnCount = 1;
        
        // ホスト(自分)を先行にする
        // SharedModeのInputAuthorityはNoneになるため、Runner.LocalPlayerを使う
        ActivePlayer = Runner.LocalPlayer; 
        
        Debug.Log($"[GameState] Game Started! First Player: {ActivePlayer}");
    }

    private PlayerRef _lastActivePlayer = PlayerRef.None;

    void CheckTurnUpdate()
    {
        if (!IsGameStarted) return;

        if (_lastActivePlayer != ActivePlayer)
        {
            _lastActivePlayer = ActivePlayer;
            OnTurnChanged();
        }
    }

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
}
