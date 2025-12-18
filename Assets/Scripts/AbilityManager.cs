using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AbilityManager : MonoBehaviour
{
    public static AbilityManager instance;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    public void CastSpell(CardData card)
    {
        ProcessAbilities(card, EffectTrigger.SPELL_USE, null);
        TriggerSpellReaction(GameManager.instance.isPlayerTurn);
    }

    public void TriggerSpellReaction(bool isPlayerAction)
    {
        Transform myBoard = isPlayerAction ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        if (myBoard != null)
        {
            foreach (UnitMover unit in myBoard.GetComponentsInChildren<UnitMover>())
            {
                if (unit.sourceData != null) ProcessAbilities(unit.sourceData, EffectTrigger.SPELL_USE, unit);
            }
        }

        var activeBuilds = GameManager.instance.activeBuilds;
        if (activeBuilds != null)
        {
            foreach (var build in activeBuilds)
            {
                if (build.isPlayerOwner == isPlayerAction && !build.isUnderConstruction)
                {
                    foreach (var ability in build.data.abilities)
                    {
                        if (ability.trigger == EffectTrigger.SPELL_USE)
                        {
                            ExecuteBuildAbility(ability, build);
                        }
                    }
                }
            }
        }
    }

    private void ExecuteBuildAbility(CardAbility ability, ActiveBuild build)
    {
        List<object> targets = GetTargets(ability.target, null, null);
        if (targets.Count == 0 && (ability.effect == EffectType.DRAW_CARD || ability.effect == EffectType.GAIN_MANA))
        {
            ActivateBuildAbility(ability, null, build);
        }
        else
        {
            foreach(var target in targets) ActivateBuildAbility(ability, target, build);
        }
    }

    public void ActivateBuildAbility(CardAbility ability, object target, ActiveBuild sourceBuild)
    {
        ApplyEffect(target, ability.effect, ability.value, null, sourceBuild);
        if (sourceBuild != null) sourceBuild.hasActed = true;
    }

    public void ProcessTurnEndEffects(bool isPlayerEnding)
    {
        Transform board = isPlayerEnding ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        if (board != null)
        {
            foreach (UnitMover unit in board.GetComponentsInChildren<UnitMover>())
            {
                if (unit.sourceData != null) ProcessAbilities(unit.sourceData, EffectTrigger.ON_TURN_END, unit);
            }
        }
    }

    public void ProcessAbilities(CardData card, EffectTrigger currentTrigger, UnitMover sourceUnit, object manualTarget = null)
    {
        foreach (CardAbility ability in card.abilities)
        {
            if (ability.trigger != currentTrigger) continue;

            List<object> targets = GetTargets(ability.target, sourceUnit, manualTarget);
            int finalValue = CalculateFinalValue(card, ability);

            ApplyAbilityToTargets(ability, targets, finalValue, sourceUnit);
        }
    }

    private int CalculateFinalValue(CardData card, CardAbility ability)
    {
        int finalValue = ability.value;
        if (card.type == CardType.SPELL && ability.effect == EffectType.DAMAGE)
        {
            finalValue += CalculateSpellDamageBonus();
        }
        return finalValue;
    }

    private int CalculateSpellDamageBonus()
    {
        int bonus = 0;
        bool isPlayerTurn = GameManager.instance.isPlayerTurn;
        Transform myBoard = isPlayerTurn ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        
        if (myBoard != null)
        {
            foreach (var unit in myBoard.GetComponentsInChildren<UnitMover>())
                if (unit.spellDamageBonus > 0) bonus += unit.spellDamageBonus;
        }

        var builds = GameManager.instance.activeBuilds;
        if (builds != null)
        {
            foreach (var b in builds)
            {
                if (b.isPlayerOwner == isPlayerTurn && !b.isUnderConstruction)
                {
                    foreach (var ba in b.data.abilities)
                        if (ba.effect == EffectType.SPELL_DAMAGE_PLUS) bonus += ba.value;
                }
            }
        }
        return bonus;
    }

    private void ApplyAbilityToTargets(CardAbility ability, List<object> targets, int finalValue, UnitMover sourceUnit)
    {
        if (targets.Count == 0 && (ability.effect == EffectType.DRAW_CARD || ability.effect == EffectType.GAIN_MANA))
        {
            ApplyEffect(null, ability.effect, finalValue, sourceUnit);
        }
        else
        {
            foreach (object target in targets) ApplyEffect(target, ability.effect, finalValue, sourceUnit);
        }
    }

    List<object> GetTargets(EffectTarget targetType, UnitMover source, object manualTarget) 
    {
        List<object> results = new List<object>();
        bool isPlayerSide = (source != null) ? source.isPlayerUnit : GameManager.instance.isPlayerTurn;
        
        Transform enemyBoardTrans = isPlayerSide ? GameManager.instance.enemyBoard : GameObject.Find("PlayerBoard").transform;
        Transform myBoardTrans = isPlayerSide ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;

        switch (targetType)
        {
            case EffectTarget.SELF: 
                if (source != null) results.Add(source); 
                break;
            case EffectTarget.ENEMY_LEADER:
            case EffectTarget.PLAYER_LEADER:
                var leader = GetLeaderTarget(targetType, isPlayerSide);
                if (leader != null) results.Add(leader);
                break;
            case EffectTarget.FRONT_ENEMY:
            case EffectTarget.ALL_ENEMIES:
            case EffectTarget.RANDOM_ENEMY:
                results.AddRange(GetEnemyTargets(targetType, source, enemyBoardTrans));
                break;
            case EffectTarget.ALL_ALLIES:
            case EffectTarget.FRONT_ALLY:
            case EffectTarget.RANDOM_ALLY:
                results.AddRange(GetAllyTargets(targetType, source, myBoardTrans));
                break;
            case EffectTarget.SELECT_ENEMY_UNIT:
            case EffectTarget.SELECT_ENEMY_LEADER:
            case EffectTarget.SELECT_ANY_ENEMY:
            case EffectTarget.SELECT_UNDAMAGED_ENEMY: 
            case EffectTarget.SELECT_DAMAGED_ENEMY:
            case EffectTarget.SELECT_ALLY_UNIT:
            case EffectTarget.SELECT_ANY_UNIT:
                if (manualTarget != null) results.Add(manualTarget);
                break;
            case EffectTarget.ALL_UNITS:
                if (myBoardTrans != null) foreach (var unit in myBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                if (enemyBoardTrans != null) foreach (var unit in enemyBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                break;
        }
        return results;
    }

    private Leader GetLeaderTarget(EffectTarget targetType, bool isPlayerSide)
    {
        Transform playerLeader = GameManager.instance.playerLeader;
        GameObject targetObj = null;

        if (targetType == EffectTarget.ENEMY_LEADER)
        {
            targetObj = isPlayerSide ? GameObject.Find("EnemyInfo") : (playerLeader != null ? playerLeader.gameObject : null);
        }
        else if (targetType == EffectTarget.PLAYER_LEADER)
        {
            targetObj = isPlayerSide ? (playerLeader != null ? playerLeader.gameObject : null) : GameObject.Find("EnemyInfo");
        }

        return targetObj != null ? targetObj.GetComponent<Leader>() : null;
    }

    private List<object> GetEnemyTargets(EffectTarget targetType, UnitMover source, Transform enemyBoardTrans)
    {
        List<object> results = new List<object>();
        if (enemyBoardTrans == null) return results;

        var allEnemies = enemyBoardTrans.GetComponentsInChildren<UnitMover>();

        switch (targetType)
        {
            case EffectTarget.FRONT_ENEMY:
                if (source != null) 
                { 
                    UnitMover front = GetFrontEnemy(source); 
                    if (front != null) results.Add(front); 
                }
                break;
            case EffectTarget.ALL_ENEMIES:
                 foreach (var unit in allEnemies) results.Add(unit);
                break;
            case EffectTarget.RANDOM_ENEMY:
                results.AddRange(GetRandomEnemy(source, allEnemies));
                break;
        }
        return results;
    }

    private List<object> GetRandomEnemy(UnitMover source, UnitMover[] allEnemies)
    {
        List<object> results = new List<object>();
        List<UnitMover> candidates = new List<UnitMover>();

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

        if (candidates.Count > 0) 
        {
            results.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }
        return results;
    }

    private List<object> GetAllyTargets(EffectTarget targetType, UnitMover source, Transform myBoardTrans)
    {
        List<object> results = new List<object>();
        if (myBoardTrans == null) return results;

        switch (targetType)
        {
            case EffectTarget.ALL_ALLIES:
                foreach (var unit in myBoardTrans.GetComponentsInChildren<UnitMover>()) results.Add(unit);
                break;
            case EffectTarget.FRONT_ALLY:
                if (source != null) { UnitMover frontAlly = GetFrontAlly(source); if (frontAlly != null) results.Add(frontAlly); }
                break;
            case EffectTarget.RANDOM_ALLY:
                var allies = myBoardTrans.GetComponentsInChildren<UnitMover>();
                if (allies.Length > 0) results.Add(allies[UnityEngine.Random.Range(0, allies.Length)]);
                break;
        }
        return results;
    }

    void ApplyEffect(object target, EffectType effectType, int value, UnitMover source, ActiveBuild sourceBuild = null)
    {
        bool isPlayerSide = true;
        if (source != null) isPlayerSide = source.isPlayerUnit;
        else if (sourceBuild != null) isPlayerSide = sourceBuild.isPlayerOwner;
        else isPlayerSide = GameManager.instance.isPlayerTurn;

        if (HandleGlobalEffect(effectType, value, isPlayerSide, target)) return;
        if (target == null) return;

        UnitMover targetUnit = ResolveTargetUnit(target);

        HandleUnitEffect(effectType, value, targetUnit, target);
    }

    private UnitMover ResolveTargetUnit(object target)
    {
        if (target is UnitMover unit) return unit;
        if (target is GameObject go) return go.GetComponent<UnitMover>();
        if (target is Component comp) return comp.GetComponent<UnitMover>();
        return null;
    }

    private bool HandleGlobalEffect(EffectType effectType, int value, bool isPlayerSide, object target)
    {
        switch (effectType)
        {
            case EffectType.GAIN_MANA:
                if (isPlayerSide) { GameManager.instance.maxMana += value; GameManager.instance.UpdateManaUI(); }
                else { GameManager.instance.enemyMaxMana += value; GameManager.instance.UpdateEnemyManaUI(); }
                return true;
            case EffectType.DRAW_CARD:
                PerformDrawCard(value, isPlayerSide, target);
                return true;
        }
        return false;
    }

    private void PerformDrawCard(int value, bool isPlayerSide, object target)
    {
        bool drawForPlayer = isPlayerSide;
        if (target != null)
        {
            if (target is Leader leader) drawForPlayer = leader.isPlayer;
            else if (target is UnitMover unit) drawForPlayer = unit.isPlayerUnit;
        }
        
        // オンライン対応
        var gameState = FindObjectOfType<GameStateController>();
        if (gameState != null && gameState.Object.IsValid)
        {
             // 相手に引かせる場合 (drawForPlayer == false)
             if (!drawForPlayer)
             {
                  // 相手PlayerRefを探す
                  var myRef = gameState.Runner.LocalPlayer;
                  Fusion.PlayerRef enemyRef = Fusion.PlayerRef.None;
                  foreach(var p in gameState.Runner.ActivePlayers) { if (p != myRef) { enemyRef = p; break; } }
                  
                  if (enemyRef != Fusion.PlayerRef.None)
                  {
                      gameState.RPC_ForceDraw(enemyRef, value);
                      return;
                  }
             }
        }

        if (drawForPlayer) GameManager.instance.DealCards(value);
        else GameManager.instance.EnemyDrawCard(value);
    }

    private void HandleUnitEffect(EffectType effectType, int value, UnitMover targetUnit, object target)
    {
        switch (effectType)
        {
            case EffectType.DAMAGE:
                if (targetUnit != null) targetUnit.TakeDamage(value);
                else if (target is Leader leader) leader.TakeDamage(value);
                break;
            case EffectType.HEAL:
                if (targetUnit != null) targetUnit.Heal(value);
                else if (target is Leader leader) leader.TakeDamage(-value);
                break;
            case EffectType.BUFF_ATTACK:
                if (targetUnit != null) 
                { 
                    targetUnit.attackPower += value; 
                    if(targetUnit.GetComponent<UnitView>()) targetUnit.GetComponent<UnitView>().RefreshDisplay(); 
                }
                break;
            case EffectType.BUFF_HEALTH:
                if (targetUnit != null) { targetUnit.maxHealth += value; targetUnit.Heal(value); }
                break;
            case EffectType.DESTROY:
                if (targetUnit != null) targetUnit.TakeDamage(9999);
                break;
            case EffectType.FORCE_MOVE:
                break;
            case EffectType.RETURN_TO_HAND:
                if (targetUnit != null) targetUnit.ReturnToHand();
                else Debug.LogWarning($"RETURN_TO_HAND Failed: Target is not a unit. Target={target}");
                break;
            case EffectType.TAUNT:
                if (targetUnit != null) { targetUnit.hasTaunt = true; RefreshUnitStatus(targetUnit); }
                break;
            case EffectType.STEALTH:
                if (targetUnit != null) { targetUnit.hasStealth = true; RefreshUnitStatus(targetUnit); }
                break;
            case EffectType.PIERCE:
                if (targetUnit != null) targetUnit.hasPierce = true;
                break;
        }
    }

    private void RefreshUnitStatus(UnitMover unit)
    {
        if (unit.GetComponent<UnitView>()) 
            unit.GetComponent<UnitView>().RefreshStatusIcons(unit.hasTaunt, unit.hasStealth);
    }

    UnitMover GetFrontEnemy(UnitMover me)
    {
        if (me.originalParent == null) return null;
        SlotInfo mySlot = me.originalParent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        
        Transform targetBoard = me.isPlayerUnit ? GameManager.instance.enemyBoard : GameObject.Find("PlayerBoard").transform;
        if (targetBoard == null) return null;

        List<UnitMover> frontEnemies = new List<UnitMover>();
        List<UnitMover> backEnemies = new List<UnitMover>();

        foreach (Transform slot in targetBoard)
        {
            SlotInfo info = slot.GetComponent<SlotInfo>();
            if (slot.childCount > 0)
            {
                var unit = slot.GetChild(0).GetComponent<UnitMover>();
                if (info.y == 0) frontEnemies.Add(unit);
                else if (info.y == 1) backEnemies.Add(unit);
            }
        }

        if (frontEnemies.Count > 0) return frontEnemies[UnityEngine.Random.Range(0, frontEnemies.Count)];
        else if (backEnemies.Count > 0) return backEnemies[UnityEngine.Random.Range(0, backEnemies.Count)];

        return null;
    }

    UnitMover GetFrontAlly(UnitMover me)
    {
        if (me.originalParent == null) return null;
        SlotInfo mySlot = me.originalParent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        Transform myBoard = me.isPlayerUnit ? GameObject.Find("PlayerBoard").transform : GameManager.instance.enemyBoard;
        if (myBoard == null) return null;
        
        int targetY = (mySlot.y == 1) ? 0 : -1;
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
                    ExecuteBuildAbility(ability, build);
                }
            }
        }
    }
}