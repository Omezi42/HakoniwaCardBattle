using UnityEngine;
using System.Collections.Generic;

public class AbilityManager : MonoBehaviour
{
    public static AbilityManager instance;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    // --- 外部から呼ばれる入り口 ---

    // スペル使用時
    public void CastSpell(CardData card)
    {
        ProcessAbilities(card, EffectTrigger.SPELL_USE, null);
    }

    // ビルドのアビリティ発動時
    public void ActivateBuildAbility(CardAbility ability, object target, ActiveBuild sourceBuild)
    {
        ApplyEffect(target, ability.effect, ability.value, null);
        
        if (sourceBuild != null)
        {
            sourceBuild.hasActed = true;
            // GameManagerのUI更新を呼ぶ（メソッドがあれば）
            // GameManager.instance.UpdateBuildUI(); // 必要ならpublicにして呼ぶ
        }
        
        Debug.Log($"ビルド効果発動: {ability.effect} -> {target}");
    }

    // ターン終了時効果
    public void ProcessTurnEndEffects(bool isPlayerEnding)
    {
        Transform board = isPlayerEnding ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        if (board != null)
        {
            foreach (UnitMover unit in board.GetComponentsInChildren<UnitMover>())
            {
                if (unit.sourceData != null)
                {
                    ProcessAbilities(unit.sourceData, EffectTrigger.ON_TURN_END, unit);
                }
            }
        }
    }

    // --- コア処理 ---

    public void ProcessAbilities(CardData card, EffectTrigger currentTrigger, UnitMover sourceUnit, object manualTarget = null)
    {
        foreach (CardAbility ability in card.abilities)
        {
            if (ability.trigger != currentTrigger) continue;

            List<object> targets = GetTargets(ability.target, sourceUnit, manualTarget);
            foreach (object target in targets)
            {
                ApplyEffect(target, ability.effect, ability.value, sourceUnit);
            }
        }
    }

    List<object> GetTargets(EffectTarget targetType, UnitMover source, object manualTarget) 
    {
        List<object> results = new List<object>();
        bool isPlayerSide = (source != null) ? source.isPlayerUnit : GameManager.instance.isPlayerTurn;
        
        // GameManagerの参照を使用
        Transform enemyBoardTrans = isPlayerSide ? GameManager.instance.enemyBoard : GameObject.Find("PlayerBoard").transform;
        Transform myBoardTrans = isPlayerSide ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        Transform playerLeader = GameManager.instance.playerLeader;

        switch (targetType)
        {
            case EffectTarget.SELF:
                if (source != null) results.Add(source);
                break;
            case EffectTarget.ENEMY_LEADER:
                var eLeader = isPlayerSide ? GameObject.Find("EnemyInfo") : (playerLeader != null ? playerLeader.gameObject : null);
                if (eLeader != null) results.Add(eLeader.GetComponent<Leader>());
                break;
            case EffectTarget.PLAYER_LEADER:
                var pLeader = isPlayerSide ? (playerLeader != null ? playerLeader.gameObject : null) : GameObject.Find("EnemyInfo");
                if (pLeader != null) results.Add(pLeader.GetComponent<Leader>());
                break;
            case EffectTarget.FRONT_ENEMY:
                if (source != null)
                {
                    UnitMover front = GetFrontEnemy(source);
                    if (front != null) results.Add(front);
                }
                break;
            case EffectTarget.ALL_ENEMIES:
                if (enemyBoardTrans != null)
                    foreach (var unit in enemyBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                break;
            case EffectTarget.RANDOM_ENEMY:
                if (enemyBoardTrans != null)
                {
                    List<UnitMover> candidates = new List<UnitMover>();
                    var allEnemies = enemyBoardTrans.GetComponentsInChildren<UnitMover>();
                    
                    // ★突撃槍兵の特例ロジック（自分と同じ列のみ）
                    // 本来はターゲットタイプを分けるべきですが、簡易実装します
                    if (source != null && source.sourceData.cardName.Contains("突撃槍兵"))
                    {
                        SlotInfo mySlot = source.originalParent.GetComponent<SlotInfo>();
                        foreach(var e in allEnemies)
                        {
                            SlotInfo eSlot = e.transform.parent.GetComponent<SlotInfo>();
                            if (mySlot != null && eSlot != null && mySlot.y == eSlot.y)
                            {
                                candidates.Add(e);
                            }
                        }
                    }
                    else
                    {
                        candidates.AddRange(allEnemies);
                    }

                    if (candidates.Count > 0) results.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
                }
                break;
            case EffectTarget.ALL_ALLIES:
                if (myBoardTrans != null)
                    foreach (var unit in myBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                break;
            case EffectTarget.SELECT_ENEMY_UNIT:
            case EffectTarget.SELECT_ENEMY_LEADER:
            case EffectTarget.SELECT_ANY_ENEMY:
                if (manualTarget != null) results.Add(manualTarget);
                break;
            case EffectTarget.SELECT_UNDAMAGED_ENEMY: 
                if (manualTarget != null) results.Add(manualTarget);
                break;
            case EffectTarget.FRONT_ALLY:
                if (source != null)
                {
                    UnitMover frontAlly = GetFrontAlly(source);
                    if (frontAlly != null) results.Add(frontAlly);
                }
                break;
        }
        return results;
    }

    void ApplyEffect(object target, EffectType effectType, int value, UnitMover source)
    {
        // ターゲット不要な効果（マナ加速、ドローなど）
        switch (effectType)
        {
            case EffectType.GAIN_MANA:
                bool isPlayer = (source != null) ? source.isPlayerUnit : GameManager.instance.isPlayerTurn;
                if (isPlayer) 
                { 
                    GameManager.instance.maxMana += value; 
                    GameManager.instance.currentMana += value; 
                    GameManager.instance.UpdateManaUI(); // publicにする必要あり
                }
                else 
                { 
                    GameManager.instance.enemyMaxMana += value; 
                    GameManager.instance.enemyCurrentMana += value; 
                    GameManager.instance.UpdateEnemyManaUI(); // publicにする必要あり
                }
                return;

            case EffectType.DRAW_CARD:
                if (GameManager.instance.isPlayerTurn) GameManager.instance.DealCards(value);
                return;
        }

        if (target == null) return;

        switch (effectType)
        {
            case EffectType.DAMAGE:
                if (target is UnitMover) ((UnitMover)target).TakeDamage(value);
                else if (target is Leader) ((Leader)target).TakeDamage(value);
                break;
            case EffectType.HEAL:
                if (target is UnitMover) ((UnitMover)target).Heal(value);
                else if (target is Leader) ((Leader)target).TakeDamage(-value);
                break;
            case EffectType.BUFF_ATTACK:
                if (target is UnitMover)
                {
                    UnitMover u = (UnitMover)target;
                    u.attackPower += value;
                    // ★重要：UnitViewの更新を忘れずに！
                    if(u.GetComponent<UnitView>()) u.GetComponent<UnitView>().RefreshDisplay(); 
                }
                break;
            case EffectType.BUFF_HEALTH:
                if (target is UnitMover)
                {
                    UnitMover u = (UnitMover)target;
                    u.maxHealth += value; 
                    u.Heal(value); 
                }
                break;
            case EffectType.DESTROY:
                if (target is UnitMover) ((UnitMover)target).TakeDamage(9999);
                break;
            case EffectType.FORCE_MOVE:
                if (target is UnitMover)
                {
                    UnitMover u = (UnitMover)target;
                    // ランダムな空きスロットへ飛ばす、あるいは手札に戻す
                    // ここでは「手札に戻す（バウンス）」として実装します
                    Destroy(u.gameObject);
                    // 本当は手札にカードを生成すべきですが、簡易的に「消滅」または「ダメージ」扱いにします
                    // もし「移動」なら、隣の空きスロットを探して u.MoveToSlot(...) を呼ぶ
                }
                break;
        }
    }

    UnitMover GetFrontEnemy(UnitMover me)
    {
        SlotInfo mySlot = me.transform.parent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        Transform targetBoard = me.isPlayerUnit ? GameManager.instance.enemyBoard : GameObject.Find("PlayerBoard").transform;
        if (targetBoard == null) return null;

        foreach (Transform slot in targetBoard)
        {
            SlotInfo info = slot.GetComponent<SlotInfo>();
            if (info.y == mySlot.y && info.x == 0 && slot.childCount > 0)
            {
                return slot.GetChild(0).GetComponent<UnitMover>();
            }
        }
        return null;
    }

    // ★追加：正面の味方を探すメソッド
    UnitMover GetFrontAlly(UnitMover me)
    {
        if (me.originalParent == null) return null;
        SlotInfo mySlot = me.originalParent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        
        // 自分のボードを取得
        Transform myBoard = me.isPlayerUnit ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        if (myBoard == null) return null;

        // 正面の味方のX座標を計算
        // プレイヤー陣: 後列(0) -> 前列(1)
        // 敵陣: 後列(1) -> 前列(0) と仮定
        int targetX = -1;
        
        if (me.isPlayerUnit)
        {
            // 自分が後列(0)なら、前(1)を見る
            if (mySlot.x == 0) targetX = 1;
        }
        else
        {
            // 敵が後列(1)なら、前(0)を見る
            if (mySlot.x == 1) targetX = 0;
        }

        // 該当するスロットを探す
        if (targetX != -1)
        {
            foreach (Transform slot in myBoard)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                // 同じ列(Y) かつ 前方の列(X) にユニットがいるか
                if (info.y == mySlot.y && info.x == targetX && slot.childCount > 0)
                {
                    return slot.GetChild(0).GetComponent<UnitMover>();
                }
            }
        }
        return null;
    }

    public void ProcessBuildEffects(EffectTrigger trigger, bool isPlayerTurnStart)
    {
        // GameManagerのデータを参照
        var activeBuilds = GameManager.instance.activeBuilds;
        if (activeBuilds == null) return;

        for (int i = activeBuilds.Count - 1; i >= 0; i--)
        {
            ActiveBuild build = activeBuilds[i];

            if (build.isUnderConstruction) continue;
            if (build.isPlayerOwner != isPlayerTurnStart) continue;

            foreach (var ability in build.data.abilities)
            {
                if (ability.trigger == trigger)
                {
                    // 内部メソッドなのでそのまま呼べる
                    List<object> targets = GetTargets(ability.target, null, null); 
                    foreach (object target in targets)
                    {
                        ApplyEffect(target, ability.effect, ability.value, null);
                    }
                }
            }
        }
    }
}