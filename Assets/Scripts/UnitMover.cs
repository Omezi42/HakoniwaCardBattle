using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UnitMover : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private UnitView unitView;
    public CardData sourceData; 
    public bool canAttack = false;
    public bool canMove = false;
    public bool hasTaunt = false;
    public bool hasStealth = false;
    public bool isPlayerUnit = true;
    public int attackPower;
    public int health;
    public string scriptKey;
    public int maxHealth;
    public bool hasHaste = false; 
    public bool hasQuick = false;
    public bool hasPierce = false;
    public int spellDamageBonus = 0;
    private bool isAnimating = false;
    private Vector3 dragStartPos;

    public bool IsTauntActive
    {
        get
        {
            if (!hasTaunt) return false;
            if (hasStealth) return false;
            if (transform.parent == null) return false;
            SlotInfo slot = transform.parent.GetComponent<SlotInfo>();
            return slot != null && slot.y == 0;
        }
    }

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        unitView = GetComponent<UnitView>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(CardData data, bool isPlayer)
    {
        attackPower = data.attack;
        health = data.health;
        sourceData = data; 
        maxHealth = data.health;
        scriptKey = data.scriptKey;
        isPlayerUnit = isPlayer;
        originalParent = transform.parent;

        foreach(var ability in data.abilities)
        {
            if (ability.trigger == EffectTrigger.PASSIVE)
            {
                if (ability.effect == EffectType.TAUNT) hasTaunt = true;
                if (ability.effect == EffectType.STEALTH) { hasStealth = true; GetComponent<CanvasGroup>().alpha = 0.5f; }
                if (ability.effect == EffectType.QUICK) hasQuick = true;
                if (ability.effect == EffectType.HASTE) hasHaste = true;
                if (ability.effect == EffectType.PIERCE) hasPierce = true; 
                if (ability.effect == EffectType.SPELL_DAMAGE_PLUS) spellDamageBonus += ability.value;
            }
        }
        
        if (isPlayer)
        {
            if (hasHaste)
            {
                canAttack = true; canMove = true;
                GetComponent<UnityEngine.UI.Image>().color = Color.white;
            }
            else
            {
                canAttack = false; canMove = false;
                GetComponent<UnityEngine.UI.Image>().color = Color.gray;
            }
        }
        else
        {
            canAttack = false; canMove = false;
            canvasGroup.blocksRaycasts = true;
            if (unitView != null && unitView.iconImage != null)
            {
                Vector3 scale = unitView.iconImage.transform.localScale;
                scale.x = -Mathf.Abs(scale.x); 
                unitView.iconImage.transform.localScale = scale;
            }
        }

        if (unitView != null) unitView.RefreshStatusIcons(hasTaunt, hasStealth);
    }

    // ★修正：手札に戻る処理（デバッグ強化）
    public void ReturnToHand()
    {
        Debug.Log($"[UnitMover] ReturnToHand called for: {name} (NetID/InstanceID: {this.GetInstanceID()})");

        if (sourceData == null)
        {
            Debug.LogError("[UnitMover] sourceData is null. Destroying anyway.");
            Destroy(gameObject); 
            return;
        }

        if (GameManager.instance != null)
        {
            Debug.Log($"[UnitMover] Calling GameManager.ReturnCardToHand for {sourceData.cardName}");
            // 自分の現在地をアニメーション開始地点として渡す
            GameManager.instance.ReturnCardToHand(sourceData, isPlayerUnit, transform.position);
            
            if (BattleLogManager.instance != null)
            {
                BattleLogManager.instance.AddLog($"{sourceData.cardName} を手札に戻した", GameManager.instance.isPlayerTurn);
            }
        }
        else
        {
            Debug.LogError("Error: GameManager instance not found.");
        }

        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData) { if (eventData.pointerDrag != null) return; if (sourceData != null && GameManager.instance != null) GameManager.instance.ShowUnitDetail(sourceData); }
    public void OnPointerExit(PointerEventData eventData) { if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail(); }
    public void OnBeginDrag(PointerEventData eventData) { if (isAnimating) return; GameManager.instance.OnClickCloseDetail(); if (!isPlayerUnit) return; if (!canAttack && !canMove) return; originalParent = transform.parent; canvasGroup.blocksRaycasts = false; dragStartPos = transform.position; GameManager.instance.ShowArrow(dragStartPos); GameManager.instance.SetArrowColor(Color.gray); }
    public void OnDrag(PointerEventData eventData) { if (!isPlayerUnit) return; if (!canAttack && !canMove) return; GameManager.instance.UpdateArrow(dragStartPos, eventData.position); UpdateArrowColor(eventData); }
    public void OnEndDrag(PointerEventData eventData) { GameManager.instance.HideArrow(); if (canvasGroup != null) canvasGroup.blocksRaycasts = true; transform.localPosition = Vector3.zero; }
    public void OnDrop(PointerEventData eventData) { UnitMover attacker = eventData.pointerDrag.GetComponent<UnitMover>(); if (attacker != null && attacker.canAttack) { if (this.isPlayerUnit != attacker.isPlayerUnit) { if (GameManager.instance.CanAttackUnit(attacker, this)) attacker.AttackUnit(this); } return; } }
    void UpdateArrowColor(PointerEventData eventData) { GameObject hoverObj = eventData.pointerCurrentRaycast.gameObject; Color targetColor = Color.gray; string labelText = ""; bool showLabel = false; string tooltipText = ""; if (hoverObj != null) { UnitMover targetUnit = hoverObj.GetComponentInParent<UnitMover>(); Leader targetLeader = hoverObj.GetComponentInParent<Leader>(); if (canAttack) { if (targetUnit != null && !targetUnit.isPlayerUnit) { if (GameManager.instance.CanAttackUnit(this, targetUnit)) { targetColor = Color.red; labelText = "攻撃"; showLabel = true; int myDmg = this.attackPower; int enemyDmg = targetUnit.attackPower; SlotInfo mySlot = originalParent.GetComponent<SlotInfo>(); SlotInfo targetSlot = targetUnit.transform.parent.GetComponent<SlotInfo>(); if(mySlot!=null && targetSlot!=null && mySlot.x == targetSlot.x) { myDmg++; enemyDmg++; } int myRem = Mathf.Max(0, health - enemyDmg); int enRem = Mathf.Max(0, targetUnit.health - myDmg); tooltipText = $"自分: {health} -> <color={(myRem==0?"red":"white")}>{myRem}</color>\n敵: {targetUnit.health} -> <color={(enRem==0?"red":"white")}>{enRem}</color>"; } } else if (targetLeader != null) { if (GameManager.instance.CanAttackLeader(this)) { targetColor = Color.red; labelText = "攻撃"; showLabel = true; int current = targetLeader.currentHp; int predict = Mathf.Max(0, current - this.attackPower); tooltipText = $"敵リーダー: {current} -> <color=red>{predict}</color>"; } } } DropPlace slot = hoverObj.GetComponentInParent<DropPlace>(); if (canMove && slot != null && !slot.isEnemySlot) { if (slot.transform.childCount == 0) { SlotInfo mySlot = originalParent.GetComponent<SlotInfo>(); SlotInfo targetSlot = slot.GetComponent<SlotInfo>(); if (mySlot != null && targetSlot != null) { int dist = Mathf.Abs(mySlot.x - targetSlot.x) + Mathf.Abs(mySlot.y - targetSlot.y); if (dist == 1) { targetColor = Color.yellow; labelText = "移動"; showLabel = true; } } } } } GameManager.instance.SetArrowColor(targetColor); GameManager.instance.SetArrowLabel(labelText, showLabel); if (!string.IsNullOrEmpty(tooltipText)) GameManager.instance.ShowTooltip(tooltipText); else GameManager.instance.HideTooltip(); }
    public void Attack(Leader target, bool force = false) { if (!canAttack && !force) return; RemoveStealth(); AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_ATTACK, this); string targetName = "リーダー"; if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"{sourceData.cardName} が {targetName} に攻撃！", isPlayerUnit); Transform targetTransform = target.transform; if (target.atkArea != null) targetTransform = target.atkArea; StartCoroutine(TackleAnimation(targetTransform, () => { GameManager.instance.PlaySE(GameManager.instance.seAttack); target.TakeDamage(attackPower); ConsumeAttack(); })); }
    public void AttackUnit(UnitMover enemy) { if (!canAttack) return; RemoveStealth(); AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_ATTACK, this); if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"{sourceData.cardName} が {enemy.sourceData.cardName} に攻撃！", isPlayerUnit); StartCoroutine(TackleAnimation(enemy.transform, () => { int finalDamage = this.attackPower; int enemyDamage = enemy.attackPower; SlotInfo mySlot = originalParent.GetComponent<SlotInfo>(); SlotInfo enemySlot = enemy.transform.parent.GetComponent<SlotInfo>(); if (mySlot != null && enemySlot != null) { if (mySlot.x == enemySlot.x) { finalDamage += 1; enemyDamage += 1; Debug.Log("正面衝突ボーナス！ +1ダメージ"); } } enemy.TakeDamage(finalDamage); if (hasPierce) { UnitMover backUnit = GetBackUnit(enemy); if (backUnit != null) { if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog("貫通ダメージ！", isPlayerUnit); backUnit.TakeDamage(this.attackPower); } } this.TakeDamage(enemyDamage); ConsumeAttack(); })); }
    void RemoveStealth() { if (hasStealth) { hasStealth = false; GetComponent<CanvasGroup>().alpha = 1.0f; if (unitView != null) unitView.RefreshStatusIcons(hasTaunt, hasStealth); Debug.Log("潜伏が解除されました"); } }
    UnitMover GetBackUnit(UnitMover frontUnit) { if (frontUnit.transform.parent == null) return null; SlotInfo frontSlot = frontUnit.transform.parent.GetComponent<SlotInfo>(); if (frontSlot == null || frontSlot.y != 0) return null; Transform board = frontUnit.transform.parent.parent; if (board == null) return null; foreach (Transform slot in board) { SlotInfo info = slot.GetComponent<SlotInfo>(); if (info != null && info.x == frontSlot.x && info.y == 1 && slot.childCount > 0) { return slot.GetChild(0).GetComponent<UnitMover>(); } } return null; }
    public void TakeDamage(int damage) { GameManager.instance.SpawnDamageText(transform.position, damage); health -= damage; if (unitView != null) unitView.RefreshDisplay(); if (health <= 0) { if (AbilityManager.instance != null && sourceData != null) AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_DEATH, this); if (GameManager.instance != null) GameManager.instance.PlayDiscardAnimation(sourceData, isPlayerUnit); Destroy(gameObject); } if (damage > 0) GameManager.instance.PlaySE(GameManager.instance.seDamage); }
    private System.Collections.IEnumerator TackleAnimation(Transform target, System.Action onHitLogic) { isAnimating = true; transform.SetParent(transform.root); Vector3 startPos = transform.position; Vector3 targetPos = target.position; Vector3 attackEndPos = Vector3.Lerp(startPos, targetPos, 0.7f); float duration = 0.15f; float elapsed = 0f; while (elapsed < duration) { transform.position = Vector3.Lerp(startPos, attackEndPos, elapsed / duration); elapsed += Time.deltaTime; yield return null; } transform.position = attackEndPos; onHitLogic?.Invoke(); yield return new WaitForSeconds(0.05f); elapsed = 0f; while (elapsed < duration) { transform.position = Vector3.Lerp(attackEndPos, startPos, elapsed / duration); elapsed += Time.deltaTime; yield return null; } if (originalParent != null) { transform.SetParent(originalParent); transform.localPosition = Vector3.zero; } isAnimating = false; }
    public void MoveToSlot(Transform targetSlot) { StartCoroutine(MoveAnimation(targetSlot)); }
    private System.Collections.IEnumerator MoveAnimation(Transform targetSlot) { isAnimating = true; transform.SetParent(transform.root); Vector3 startPos = transform.position; Vector3 endPos = targetSlot.position; float duration = 0.2f; float elapsed = 0f; while (elapsed < duration) { transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration); elapsed += Time.deltaTime; yield return null; } transform.position = endPos; transform.SetParent(targetSlot); transform.localPosition = Vector3.zero; originalParent = targetSlot; ConsumeMove(); if (AbilityManager.instance != null && sourceData != null) { AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_MOVE, this); } isAnimating = false; }
    public void PlaySummonAnimation() { StartCoroutine(SummonAnimationCoroutine()); }
    private System.Collections.IEnumerator SummonAnimationCoroutine() { isAnimating = true; Vector3 originalScale = transform.localScale; Vector3 landPos = transform.localPosition; Vector3 startPos = landPos + new Vector3(0, 50f, 0); transform.localPosition = startPos; transform.localScale = originalScale * 1.2f; if(canvasGroup) canvasGroup.alpha = 0f; float duration = 0.25f; float elapsed = 0f; while (elapsed < duration) { float t = elapsed / duration; t = t * t * (3f - 2f * t); transform.localPosition = Vector3.Lerp(startPos, landPos, t); transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t); if(canvasGroup) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2); elapsed += Time.deltaTime; yield return null; } transform.localPosition = landPos; transform.localScale = originalScale; if(canvasGroup) canvasGroup.alpha = 1f; isAnimating = false; }
    public void Heal(int amount) { health += amount; if (health > maxHealth) health = maxHealth; if (unitView != null) unitView.RefreshDisplay(); }
    public void ConsumeAction() { canMove = false; canAttack = false; UpdateColor(); }
    public void ConsumeMove() { canMove = false; if (!hasQuick) canAttack = false; UpdateColor(); }
    public void ConsumeAttack() { canAttack = false; if (!hasQuick) canMove = false; UpdateColor(); }
    void UpdateColor() { if (GetComponent<UnityEngine.UI.Image>() == null) return; if (!canMove && !canAttack) GetComponent<UnityEngine.UI.Image>().color = Color.gray; else GetComponent<UnityEngine.UI.Image>().color = Color.white; }
}