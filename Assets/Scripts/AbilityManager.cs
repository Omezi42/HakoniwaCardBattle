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
        // 1. スペル本来の効果を発動（これは今まで通り）
        ProcessAbilities(card, EffectTrigger.SPELL_USE, null);

        // 2. ★追加：「スペルを使ったとき」に反応するユニット/ビルドの効果を発動
        ProcessSpellCastReaction(GameManager.instance.isPlayerTurn);
    }

    // ★追加：スペル使用時の反応処理
    void ProcessSpellCastReaction(bool isPlayerAction)
    {
        // アクションした側の盤面を取得
        Transform myBoard = isPlayerAction ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        
        // A. ユニットの反応
        if (myBoard != null)
        {
            foreach (UnitMover unit in myBoard.GetComponentsInChildren<UnitMover>())
            {
                if (unit.sourceData != null)
                {
                    // ユニット自身を発動元(sourceUnit)として効果処理を実行
                    ProcessAbilities(unit.sourceData, EffectTrigger.SPELL_USE, unit);
                }
            }
        }

        // B. ビルドの反応
        var activeBuilds = GameManager.instance.activeBuilds;
        if (activeBuilds != null)
        {
            foreach (var build in activeBuilds)
            {
                // 自分のビルドかつ、建設完了しているもの
                if (build.isPlayerOwner == isPlayerAction && !build.isUnderConstruction)
                {
                    // ビルドの能力を走査
                    foreach (var ability in build.data.abilities)
                    {
                        if (ability.trigger == EffectTrigger.SPELL_USE)
                        {
                            // ターゲット取得（ターゲット不要な効果も含む）
                            List<object> targets = GetTargets(ability.target, null, null);
                            
                            if (targets.Count == 0 && (ability.effect == EffectType.DRAW_CARD || ability.effect == EffectType.GAIN_MANA))
                            {
                                ActivateBuildAbility(ability, null, build);
                            }
                            else
                            {
                                foreach(var target in targets)
                                {
                                    ActivateBuildAbility(ability, target, build);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // ビルドのアビリティ発動時
    public void ActivateBuildAbility(CardAbility ability, object target, ActiveBuild sourceBuild)
    {
        ApplyEffect(target, ability.effect, ability.value, null);
        if (sourceBuild != null) sourceBuild.hasActed = true;
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
            
            // ★追加：スペルダメージ計算
            int finalValue = ability.value;
            if (card.type == CardType.SPELL && ability.effect == EffectType.DAMAGE)
            {
                // プレイヤーのターンならプレイヤー盤面、敵なら敵盤面を参照
                bool isPlayerTurn = GameManager.instance.isPlayerTurn;
                Transform myBoard = isPlayerTurn ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
                
                if (myBoard != null)
                {
                    foreach (var unit in myBoard.GetComponentsInChildren<UnitMover>())
                    {
                        if (unit.spellDamageBonus > 0)
                        {
                            finalValue += unit.spellDamageBonus;
                        }
                    }
                }
                
                if (finalValue > ability.value) 
                {
                    Debug.Log($"Spell Damage Boosted! {ability.value} -> {finalValue}");
                }
            }

            foreach (object target in targets)
            {
                // ability.value の代わりに finalValue を渡す
                ApplyEffect(target, ability.effect, finalValue, sourceUnit);
            }
        }
    }

    List<object> GetTargets(EffectTarget targetType, UnitMover source, object manualTarget) 
    {
        List<object> results = new List<object>();
        bool isPlayerSide = (source != null) ? source.isPlayerUnit : GameManager.instance.isPlayerTurn;
        
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
                    
                    if (source != null && source.sourceData.cardName.Contains("突撃槍兵"))
                    {
                        SlotInfo mySlot = source.originalParent.GetComponent<SlotInfo>();
                        foreach(var e in allEnemies)
                        {
                            SlotInfo eSlot = e.transform.parent.GetComponent<SlotInfo>();
                            if (mySlot != null && eSlot != null && mySlot.y == eSlot.y) candidates.Add(e);
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
            case EffectTarget.SELECT_UNDAMAGED_ENEMY: 
            case EffectTarget.SELECT_ALLY_UNIT:
            case EffectTarget.SELECT_ANY_UNIT:
                if (manualTarget != null) results.Add(manualTarget);
                break;
            case EffectTarget.FRONT_ALLY:
                if (source != null)
                {
                    UnitMover frontAlly = GetFrontAlly(source);
                    if (frontAlly != null) results.Add(frontAlly);
                }
                break;
            case EffectTarget.RANDOM_ALLY:
                if (myBoardTrans != null)
                {
                    var allies = myBoardTrans.GetComponentsInChildren<UnitMover>();
                    if (allies.Length > 0) results.Add(allies[UnityEngine.Random.Range(0, allies.Length)]);
                }
                break;
            case EffectTarget.ALL_UNITS:
                if (myBoardTrans != null) foreach (var unit in myBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                if (enemyBoardTrans != null) foreach (var unit in enemyBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                break;
        }
        return results;
    }

    void ApplyEffect(object target, EffectType effectType, int value, UnitMover source)
    {
        switch (effectType)
        {
            case EffectType.GAIN_MANA:
                bool isPlayer = (source != null) ? source.isPlayerUnit : GameManager.instance.isPlayerTurn;
                if (isPlayer) { GameManager.instance.maxMana += value; GameManager.instance.currentMana += value; GameManager.instance.UpdateManaUI(); }
                else { GameManager.instance.enemyMaxMana += value; GameManager.instance.enemyCurrentMana += value; GameManager.instance.UpdateEnemyManaUI(); }
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
            case EffectType.RETURN_TO_HAND:
                if (target is UnitMover)
                {
                    UnitMover u = (UnitMover)target;
                    Destroy(u.gameObject);
                }
                break;
            case EffectType.TAUNT:
                if (target is UnitMover)
                {
                    UnitMover u = (UnitMover)target;
                    u.hasTaunt = true;
                    if(u.GetComponent<UnitView>()) u.GetComponent<UnitView>().RefreshStatusIcons(u.hasTaunt, u.hasStealth);
                }
                break;
            case EffectType.STEALTH:
                if (target is UnitMover)
                {
                    UnitMover u = (UnitMover)target;
                    u.hasStealth = true;
                    if(u.GetComponent<UnitView>()) u.GetComponent<UnitView>().RefreshStatusIcons(u.hasTaunt, u.hasStealth);
                }
                break;
            case EffectType.PIERCE:
                if (target is UnitMover) ((UnitMover)target).hasPierce = true;
                break;
        }
    }

    UnitMover GetFrontEnemy(UnitMover me)
    {
        if (me.originalParent == null) return null;
        SlotInfo mySlot = me.originalParent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        
        Transform targetBoard = me.isPlayerUnit ? GameManager.instance.enemyBoard : GameObject.Find("PlayerBoard").transform;
        if (targetBoard == null) return null;

        UnitMover frontEnemy = null;
        UnitMover backEnemy = null;

        foreach (Transform slot in targetBoard)
        {
            SlotInfo info = slot.GetComponent<SlotInfo>();
            if (info.x == mySlot.x && slot.childCount > 0)
            {
                var unit = slot.GetChild(0).GetComponent<UnitMover>();
                if (info.y == 0) frontEnemy = unit;
                else if (info.y == 1) backEnemy = unit;
            }
        }
        return frontEnemy != null ? frontEnemy : backEnemy;
    }

    UnitMover GetFrontAlly(UnitMover me)
    {
        if (me.originalParent == null) return null;
        SlotInfo mySlot = me.originalParent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        
        Transform myBoard = me.isPlayerUnit ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        if (myBoard == null) return null;

        int targetY = -1;
        if (mySlot.y == 1) targetY = 0; 

        if (targetY != -1)
        {
            foreach (Transform slot in myBoard)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                if (info.x == mySlot.x && info.y == targetY && slot.childCount > 0)
                {
                    return slot.GetChild(0).GetComponent<UnitMover>();
                }
            }
        }
        return null;
    }

    public void ProcessBuildEffects(EffectTrigger trigger, bool isPlayerTurnStart)
    {
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
                    List<object> targets = GetTargets(ability.target, null, null); 
                    
                    if (targets.Count == 0 && (ability.effect == EffectType.DRAW_CARD || ability.effect == EffectType.GAIN_MANA))
                    {
                        ApplyEffect(null, ability.effect, ability.value, null);
                    }
                    else
                    {
                        foreach (object target in targets)
                        {
                            ApplyEffect(target, ability.effect, ability.value, null);
                        }
                    }
                }
            }
        }
    }
}