using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Fusion; // [NEW]

public class UnitMover : NetworkBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private UnitView unitView;
    public CardData sourceData; 
    public bool canAttack = false;
    public bool canMove = false;
    public bool hasTaunt = false;

    private bool _localHasStealth;
    public bool hasStealth
    {
        get => (Object != null && Object.IsValid) ? (bool)_netHasStealth : _localHasStealth;
        set 
        { 
            if (Object != null && Object.IsValid) _netHasStealth = value; 
            else _localHasStealth = value; 
            
            // Visual update
            if (value) GetComponent<CanvasGroup>().alpha = 0.5f;
            else GetComponent<CanvasGroup>().alpha = 1.0f;
            
            if (unitView != null) unitView.RefreshStatusIcons(hasTaunt, value);
        }
    }
    public bool isPlayerUnit = true;

    // [Hybrid Networked Properties]
    // オフライン時は _local 変数、オンライン時は Fusion の [Networked] 変数を使う

    private int _localAttackPower;
    public int attackPower
    {
        get => (Object != null && Object.IsValid) ? _netAttackPower : _localAttackPower;
        set { if (Object != null && Object.IsValid) _netAttackPower = value; else _localAttackPower = value; }
    }


    private int _localHealth;
    public int health
    {
        get => (Object != null && Object.IsValid) ? _netHealth : _localHealth;
        set { if (Object != null && Object.IsValid) _netHealth = value; else _localHealth = value; }
    }
    

    private string _lastCardId;
    
    // ChangeDetector used in Render below

    private void OnCardIdSync()
    {
        if (string.IsNullOrEmpty(_netCardId.ToString())) return;
        // すでに初期化済みならスキップ(必要に応じて)
        if (sourceData != null && sourceData.id == _netCardId) return;  

        CardData data = null;
        if (PlayerDataManager.instance != null)
        {
            data = PlayerDataManager.instance.GetCardById(_netCardId.ToString());
        }
        else
        {
            // Fallback
             data = Resources.Load<CardData>("Cards/" + _netCardId);
        }

        // Proxy(相手)として初期化、あるいは自分のユニットの同期受信
        // bool isMine = (Object != null && Object.IsValid) ? Object.HasInputAuthority : false;
        bool isMine = (Object != null && Object.IsValid) ? (OwnerPlayer == Runner.LocalPlayer) : false;
        
        if (data != null) 
        {
            Initialize(data, isMine);
        }
        else 
        {
            Debug.LogError($"[UnitMover] Failed to load CardData for ID: {_netCardId}");
        }
    }
    
    public string scriptKey; // Deprecated but kept for compatibility if needed locally
    public int maxHealth;
    public bool hasHaste = false; 
    public bool hasQuick = false;
    public bool hasPierce = false;
    public int spellDamageBonus = 0;
    private bool isAnimating = false;
    private Vector3 dragStartPos;
    private int _currentVisualHealth; // ★Added for change detection

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

    [Networked] public int NetworkedSlotX { get; set; } = -1;
    [Networked] public int NetworkedSlotY { get; set; } = -1;
    [Networked] public PlayerRef OwnerPlayer { get; set; } // ★Added for robust ownership check
    
    [Networked] public NetworkString<_32> _netCardId { get; set; }
    [Networked] public int _netAttackPower { get; set; }
    [Networked] public int _netHealth { get; set; }
    [Networked] public NetworkBool _netHasStealth { get; set; }
    [Networked] public NetworkBool _netCanAttack { get; set; }
    [Networked] public NetworkBool _netCanMove { get; set; }

    private ChangeDetector _changes;

    public override void Spawned()
    {
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // ★FIX: Disable NetworkTransform to allow Local Mirroring
        // Since Host(Top) and Guest(Bottom) have different World Positions for the "Same" logical slot,
        // we cannot use NetworkTransform for position sync.
        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

        // ネットワークオブジェクトが生成された時の初期化
        if (HasStateAuthority)
        {
            // ホスト(生成者)側:
            if (sourceData != null)
            {
                _netCardId = sourceData.id;
                _netAttackPower = attackPower;
                _netHealth = health;
                _netHasStealth = hasStealth; // Sync initial stealth
                _netCanAttack = canAttack;
                _netCanMove = canMove;
            }
        }
        else
        {
            // クライアント(受信/Proxy)側: card data sync
            if (!string.IsNullOrEmpty(_netCardId.ToString()))
            {
                OnCardIdSync();
            }

            SyncParentSlot();
            // Initial visual update for stealth
            if (_netHasStealth) GetComponent<CanvasGroup>().alpha = 0.5f;
            UpdateColor();
        }
    }
    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            // Sync Logic Vars -> Network Vars
            _netAttackPower = attackPower;
            _netHealth = health;
            _netHasStealth = hasStealth;
            _netCanAttack = canAttack;
            _netCanMove = canMove;
        }
    }

    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(_netCardId):
                    OnCardIdSync();
                    break;
                case nameof(NetworkedSlotX):
                case nameof(NetworkedSlotY):
                    SyncParentSlot();
                    break;
                case nameof(OwnerPlayer): // ★Fix: Re-sync parent if ownership changes (e.g. late assignment)
                    SyncParentSlot();
                    break;
                case nameof(_netAttackPower):
                case nameof(_netHealth):
                    // ★Fix: Detect Damage for Visual Text on ALL clients
                    int damage = _currentVisualHealth - _netHealth;
                    if (damage > 0)
                    {
                        if (GameManager.instance != null) GameManager.instance.SpawnDamageText(transform.position, damage);
                    }
                    _currentVisualHealth = _netHealth;
                    
                    if (unitView != null) unitView.RefreshDisplay();
                    break;
                case nameof(_netHasStealth):
                    bool stealth = _netHasStealth;
                    // ★Fix: Sync Logic Variable too, not just Visuals!
                    // This ensures CheckTargetValidity works on Proxy.
                    hasStealth = stealth; 
                    
                    if (stealth) GetComponent<CanvasGroup>().alpha = 0.5f;
                    else GetComponent<CanvasGroup>().alpha = 1.0f;
                    if (unitView != null) unitView.RefreshStatusIcons(hasTaunt, stealth);
                    break;
                case nameof(_netCanAttack):
                case nameof(_netCanMove):
                    canAttack = _netCanAttack;
                    canMove = _netCanMove;
                    UpdateColor();
                    break;
            }
        }
    }
    
    // ... (SyncParentSlot and Initialize remain similar, removed duplicates for brevity in edit)
    
    void SyncParentSlot()
    {
        if (GameManager.instance == null) return;
        
        // ★FIX: Use OwnerPlayer (Networked) to determine board.
        bool isMyUnit = (Object != null && Object.IsValid) ? (OwnerPlayer == Runner.LocalPlayer) : isPlayerUnit;
        Transform targetBoard = isMyUnit ? GameObject.Find("PlayerBoard")?.transform : GameObject.Find("EnemyBoard")?.transform;

        if (targetBoard != null && NetworkedSlotX != -1 && NetworkedSlotY != -1)
        {
            foreach(Transform slot in targetBoard)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                if (info != null && info.x == NetworkedSlotX && info.y == NetworkedSlotY)
                {
                    if (transform.parent == slot) return;
                    transform.SetParent(slot, false);
                    transform.localPosition = Vector3.zero;
                    originalParent = slot; 
                    return;
                }
            }
            Debug.Log($"[UnitMover] SyncParentSlot Failed: Slot ({NetworkedSlotX}, {NetworkedSlotY}) not found on {targetBoard.name}. IsPlayerUnit: {isPlayerUnit}, HasInputAuth: {Object?.HasInputAuthority}");
        }
    }
    
    public void Setup(CardData data)
    {
        Initialize(data, isPlayerUnit);
    }

    public void Initialize(CardData data, bool isPlayer)
    {
        isPlayerUnit = isPlayer; 
        
        attackPower = data.attack;
        health = data.health;
        sourceData = data;  
        maxHealth = data.health;
        
        originalParent = transform.parent;

        // ★ DIFFICULTY BUFF (Hard: +1/+1 for CPU)
        if (!isPlayer && PlayerDataManager.instance != null && PlayerDataManager.instance.cpuDifficulty == 2)
        {
             attackPower += 1;
             health += 1;
             maxHealth += 1;
        }

        if (Object != null && Object.HasStateAuthority)
        {
             _netCardId = data.id; 
             _netAttackPower = attackPower; // Use buffed values
             _netHealth = health;         // Use buffed values
             
             SlotInfo slotInfo = transform.parent?.GetComponent<SlotInfo>();
             if (slotInfo != null)
             {
                 NetworkedSlotX = slotInfo.x;
                 NetworkedSlotY = slotInfo.y;
             }
        }

        if (unitView != null) 
        {
            unitView.SetUnit(data);
            unitView.RefreshDisplay();
            unitView.RefreshStatusIcons(hasTaunt, hasStealth); 
        }
        else
        {
            unitView = GetComponent<UnitView>();
            if (unitView != null)
            {
                unitView.SetUnit(data);
                unitView.RefreshDisplay();
                unitView.RefreshStatusIcons(hasTaunt, hasStealth);
            }
        }
        
        // Initialize Visual Health Tracker
        _currentVisualHealth = health;

        foreach(var ability in data.abilities)
        {
            if (ability.trigger == EffectTrigger.PASSIVE)
            {
                if (ability.effect == EffectType.TAUNT) hasTaunt = true;
                if (ability.effect == EffectType.STEALTH) { hasStealth = true; } // Setter handles visuals
                if (ability.effect == EffectType.QUICK) hasQuick = true;
                if (ability.effect == EffectType.HASTE) hasHaste = true;
                if (ability.effect == EffectType.PIERCE) hasPierce = true; 
                if (ability.effect == EffectType.SPELL_DAMAGE_PLUS) spellDamageBonus += ability.value;
            }
        }
        
        // Host Update stealth prop
        if (Object != null && Object.HasStateAuthority) _netHasStealth = hasStealth;
        
        if (isPlayer)
        {
            if (hasHaste)
            {
                canAttack = true; canMove = true;
                if(GetComponent<UnityEngine.UI.Image>()) GetComponent<UnityEngine.UI.Image>().color = Color.white;
            }
            else
            {
                canAttack = false; canMove = false;
                if(GetComponent<UnityEngine.UI.Image>()) GetComponent<UnityEngine.UI.Image>().color = Color.gray;
            }
        }
        else
        {
            canAttack = false; canMove = false;
            canvasGroup.blocksRaycasts = true; 
            if (unitView != null && unitView.iconImage != null)
            {
                Vector3 scale = unitView.iconImage.transform.localScale;
                if (scale.x > 0) scale.x = -scale.x; 
                unitView.iconImage.transform.localScale = scale;
            }
        }
    }


    
    // ... (Pointer handlers remain similar) ...

    public void OnPointerEnter(PointerEventData eventData) { if (eventData.pointerDrag != null) return; if (sourceData != null && GameManager.instance != null) GameManager.instance.ShowUnitDetail(sourceData); }
    public void OnPointerExit(PointerEventData eventData) { if (GameManager.instance != null) GameManager.instance.OnClickCloseDetail(); }
    public void OnBeginDrag(PointerEventData eventData) { if (isAnimating) return; GameManager.instance.OnClickCloseDetail(); if (!isPlayerUnit) return; if (!canAttack && !canMove) return; originalParent = transform.parent; canvasGroup.blocksRaycasts = false; dragStartPos = transform.position; GameManager.instance.ShowArrow(dragStartPos); GameManager.instance.SetArrowColor(Color.gray); }
    public void OnDrag(PointerEventData eventData) { if (!isPlayerUnit) return; if (!canAttack && !canMove) return; GameManager.instance.UpdateArrow(dragStartPos, eventData.position); UpdateArrowColor(eventData); }
    public void OnEndDrag(PointerEventData eventData) { GameManager.instance.HideArrow(); if (canvasGroup != null) canvasGroup.blocksRaycasts = true; transform.localPosition = Vector3.zero; }
    public void OnDrop(PointerEventData eventData) 
    { 
        UnitMover attacker = eventData.pointerDrag.GetComponent<UnitMover>(); 
        if (attacker != null && attacker.canAttack) 
        { 
            // 自分のユニット同士ではない場合
            if (this.isPlayerUnit != attacker.isPlayerUnit) 
            { 
                if (GameManager.instance.CanAttackUnit(attacker, this)) 
                    attacker.AttackUnit(this); 
            } 
            return; 
        } 
    }
    
    // ... (UpdateArrowColor remains similar) ...
    void UpdateArrowColor(PointerEventData eventData) { GameObject hoverObj = eventData.pointerCurrentRaycast.gameObject; Color targetColor = Color.gray; string labelText = ""; bool showLabel = false; string tooltipText = ""; if (hoverObj != null) { UnitMover targetUnit = hoverObj.GetComponentInParent<UnitMover>(); Leader targetLeader = hoverObj.GetComponentInParent<Leader>(); if (canAttack) { if (targetUnit != null && !targetUnit.isPlayerUnit) { if (GameManager.instance.CanAttackUnit(this, targetUnit)) { targetColor = Color.red; labelText = "攻撃"; showLabel = true; int myDmg = this.attackPower; int enemyDmg = targetUnit.attackPower; SlotInfo mySlot = originalParent.GetComponent<SlotInfo>(); SlotInfo targetSlot = targetUnit.transform.parent.GetComponent<SlotInfo>(); if(mySlot!=null && targetSlot!=null && mySlot.x == targetSlot.x) { myDmg++; enemyDmg++; } int myRem = Mathf.Max(0, health - enemyDmg); int enRem = Mathf.Max(0, targetUnit.health - myDmg); tooltipText = $"自分: {health} -> <color={(myRem==0?"red":"white")}>{myRem}</color>\n敵: {targetUnit.health} -> <color={(enRem==0?"red":"white")}>{enRem}</color>"; } } else if (targetLeader != null) { if (GameManager.instance.CanAttackLeader(this)) { targetColor = Color.red; labelText = "攻撃"; showLabel = true; int current = targetLeader.currentHp; int predict = Mathf.Max(0, current - this.attackPower); tooltipText = $"敵リーダー: {current} -> <color=red>{predict}</color>"; } } } DropPlace slot = hoverObj.GetComponentInParent<DropPlace>(); if (canMove && slot != null && !slot.isEnemySlot) { if (slot.transform.childCount == 0) { SlotInfo mySlot = originalParent.GetComponent<SlotInfo>(); SlotInfo targetSlot = slot.GetComponent<SlotInfo>(); if (mySlot != null && targetSlot != null) { int dist = Mathf.Abs(mySlot.x - targetSlot.x) + Mathf.Abs(mySlot.y - targetSlot.y); if (dist == 1) { targetColor = Color.yellow; labelText = "移動"; showLabel = true; } } } } } GameManager.instance.SetArrowColor(targetColor); GameManager.instance.SetArrowLabel(labelText, showLabel); if (!string.IsNullOrEmpty(tooltipText)) GameManager.instance.ShowTooltip(tooltipText); else GameManager.instance.HideTooltip(); }

    // ★修正: AttackをRPC経由に変更 (Host Authority)
    public void Attack(Leader target, bool force = false) 
    { 
        if (!canAttack && !force) return; 

        // ★追加: 自分（味方）のリーダーには攻撃できないようにする
        if (target != null && target.isPlayer == this.isPlayerUnit)
        {
            Debug.Log("[UnitMover] Cannot attack allied leader.");
            return;
        }
        
        // オンラインなら
        if (Object != null && Object.IsValid)
        {
            if (HasStateAuthority)
            {
                // 自分（ホスト/サーバー）は直接ブロードキャスト
                RPC_AttackLeader();
            }
            else
            {
                // クライアントはホストにリクエスト
                var gameState = FindObjectOfType<GameStateController>();
                if (gameState != null)
                {
                    gameState.RPC_RequestAttack(Object.Id, default, true);
                }
            }
        }
        else
        {
            // オフラインフォールバック
            PerformAttackLeader(target);
        }
    }

    public void AttackUnit(UnitMover enemy) 
    { 
        if (!canAttack) return; 
        
        // オンラインなら
        if (Object != null && Object.IsValid && enemy.Object != null)
        {
            if (HasStateAuthority)
            {
                // 自分（ホスト）は直接ブロードキャスト
                RPC_AttackUnit(enemy.Object.InputAuthority, enemy.Object.Id);
            }
            else
            {
                 // クライアントはホストにリクエスト
                var gameState = FindObjectOfType<GameStateController>();
                if (gameState != null)
                {
                    gameState.RPC_RequestAttack(Object.Id, enemy.Object.Id, false);
                }
            }
        }
        else
        {
            // オフラインフォールバック
            PerformAttackUnit(enemy);
        }
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AttackLeader()
    {
        // 攻撃実行 (受信側で「敵リーダー」への攻撃として再生)
        // 注意: 実行者が「自分(ローカル)」なら EnemyLeader、実行者が「敵」なら PlayerLeader が対象
        Leader target = null;
        
        // ★FIX: Use OwnerPlayer property for robust target determination
        // ★修正: 物理名（EnemyInfo/PlayerInfo）に依存せず、論理的な属性でターゲットを決定
        // 攻撃者が「自分」側か「相手」側かを、そのクライアントにおける isPlayerUnit で判定
        // RPC_AttackLeader は StateAuthority(Host) から全クライアントへ飛ぶ
        
        if (this.isPlayerUnit) 
        {
            // このユニットが「プレイヤー（下側）」所属の場合 -> 敵リーダー（上側）を攻撃
            target = GameObject.Find("EnemyInfo")?.GetComponent<Leader>();
            // 念のためフォールバック
            if (target == null) target = GameObject.Find("EnemyLeader")?.GetComponent<Leader>();
        }
        else
        {
            // このユニットが「エネミー（上側）」所属の場合 -> プレイヤーリーダー（下側）を攻撃
            target = GameObject.Find("PlayerInfo")?.GetComponent<Leader>();
            // 念のためフォールバック
            if (target == null) target = GameObject.Find("PlayerLeader")?.GetComponent<Leader>();
        }

        if (target != null)
        {
            if (BattleLogManager.instance != null) 
            {
                 // Determine actor for log color
                 bool isMyUnit = HasStateAuthority; // Likely true if I am Sender? No, RPC runs on ALL.
                 // Source Data is available? Yes, unit instance holds it.
                 // "SourceData.cardName が リーダー に攻撃!"
                 // isPlayerUnit is correct context on each client?
                 // If I am Guest, Host Unit has isPlayerUnit=false. Correct.
                 BattleLogManager.instance.AddLog($"{sourceData.cardName} が リーダー に攻撃！", isPlayerUnit);
            }

            StartCoroutine(TackleAnimation(target.atkArea != null ? target.atkArea : target.transform, () => 
            {
                GameManager.instance.PlaySE(GameManager.instance.seAttack);
                // ★FIX: Spawn text at the 'Attack Point' (current unit position) instead of leader position
                GameManager.instance.SpawnDamageText(transform.position + new Vector3(0, 50, 0), attackPower);
                
                target.TakeDamage(attackPower);
                ConsumeAttack(); 
            }));
            
            // 自分の行動権消費
            if(HasStateAuthority) ConsumeAttack();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AttackUnit(PlayerRef targetOwner, NetworkId targetId)
    {
        // NetworkIdから対象オブジェクトを検索
        NetworkObject targetObj = Runner.FindObject(targetId);
        if (targetObj != null)
        {
            UnitMover enemy = targetObj.GetComponent<UnitMover>();
            if (enemy != null)
            {
                PerformAttackUnit(enemy);
            }
        }
    }

    // 既存ロジックを分離 (RPCと共有)
    private void PerformAttackLeader(Leader target)
    {
        RemoveStealth();
        
        bool isAuth = (Object != null && Object.IsValid) ? HasStateAuthority : true;

        // [Fix] Logic only on Authority (or Offline)
        if (isAuth) AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_ATTACK, this);
        
        string targetName = "リーダー";
        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"{sourceData.cardName} が {targetName} に攻撃！", isPlayerUnit);
        
        Transform targetTransform = target.transform;
        if (target.atkArea != null) targetTransform = target.atkArea;

        // Offline / Local execution of Coroutine
        StartCoroutine(TackleAnimation(targetTransform, () => 
        {
            GameManager.instance.PlaySE(GameManager.instance.seAttack);
            // [Fix] Damage only on Authority (or Offline)
            if (isAuth)
            {
                target.TakeDamage(attackPower);
                ConsumeAttack(); // State update
            }
        }));
    }

    private void PerformAttackUnit(UnitMover enemy)
    {
        RemoveStealth();
        
        bool isAuth = (Object != null && Object.IsValid) ? HasStateAuthority : true;

        // [Fix] Logic only on Authority (or Offline)
        if (isAuth) AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_ATTACK, this);
        
        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"{sourceData.cardName} が {enemy.sourceData.cardName} に攻撃！", isPlayerUnit);

        StartCoroutine(TackleAnimation(enemy.transform, () => 
        {
            int finalDamage = this.attackPower;
            int enemyDamage = enemy.attackPower;
            
            // 正面衝突ボーナス計算 (親スロットが必要)
            SlotInfo mySlot = (originalParent != null) ? originalParent.GetComponent<SlotInfo>() : null;
            SlotInfo enemySlot = (enemy.originalParent != null) ? enemy.originalParent.GetComponent<SlotInfo>() : null;
            if (enemySlot == null && enemy.transform.parent != null) enemySlot = enemy.transform.parent.GetComponent<SlotInfo>();
            
            if (mySlot != null && enemySlot != null) 
            {
                if (mySlot.x == enemySlot.x) 
                {
                    finalDamage += 1;
                    enemyDamage += 1;
                }
            }
            
            // [Fix] Logic only on Authority (or Offline)
            if (isAuth)
            {
                enemy.TakeDamage(finalDamage);
                
                if (hasPierce) 
                {
                    UnitMover backUnit = GetBackUnit(enemy);
                    if (backUnit != null) 
                    {
                        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog("貫通ダメージ！", isPlayerUnit);
                        backUnit.TakeDamage(this.attackPower);
                    }
                }
                
                this.TakeDamage(enemyDamage);
                ConsumeAttack();
            }
        }));
    }


    void RemoveStealth() { if (hasStealth) { hasStealth = false; GetComponent<CanvasGroup>().alpha = 1.0f; if (unitView != null) unitView.RefreshStatusIcons(hasTaunt, hasStealth); Debug.Log("潜伏が解除されました"); } }
    
    // Unsummon Logic
    public void ReturnToHand()
    {
        if (Object != null && Object.IsValid && !Object.HasStateAuthority)
        {
             // Request Host to handle return
             RPC_ReturnToHand();
             return;
        }

        // Logic (Host or Offline)
        if (GameManager.instance != null)
        {
            GameManager.instance.ReturnCardToHand(sourceData, isPlayerUnit, transform.position);
            
            // Add Log (Broadcast via RPC if needed, but BattleLogManager does local log usually)
            // GameManager.ReturnCardToHand might broadcast? 
            // Let's add explicit log here if GameManager doesn't.
            // But wait, GameManager.ReturnCardToHand creates a new card instance.
            // Let's rely on GameManager for visual, but Log here is good practice.
            if (BattleLogManager.instance != null && sourceData != null) 
                BattleLogManager.instance.AddLog($"{sourceData.cardName} を手札に戻した", isPlayerUnit); 
        }
        
        // Destroy Unit
        if (Object != null && Object.IsValid) Runner.Despawn(Object);
        else Destroy(gameObject);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ReturnToHand()
    {
        ReturnToHand();
    }

    UnitMover GetBackUnit(UnitMover frontUnit) { if (frontUnit.transform.parent == null) return null; SlotInfo frontSlot = frontUnit.transform.parent.GetComponent<SlotInfo>(); if (frontSlot == null || frontSlot.y != 0) return null; Transform board = frontUnit.transform.parent.parent; if (board == null) return null; foreach (Transform slot in board) { SlotInfo info = slot.GetComponent<SlotInfo>(); if (info != null && info.x == frontSlot.x && info.y == 1 && slot.childCount > 0) { return slot.GetChild(0).GetComponent<UnitMover>(); } } return null; }
    
    public void TakeDamage(int damage) 
    {
        // 権限チェック: HPを変更できるのはStateAuthorityのみ
        if (Object != null && Object.IsValid && !HasStateAuthority)
        {
            // 自分に権限がない場合、RPCでAuthorityに依頼する
            RPC_ApplyDamage(damage);
            
            // 演出だけ先行して見せる(HPは変わらない)
            return;
        }

        // GameManager.instance.SpawnDamageText(transform.position, damage); // ★Removed: Handled in Render
        health -= damage;
        
        if (unitView != null) unitView.RefreshDisplay();
        
        if (health <= 0) 
        {
            // [Fix] OnDeath logic should only run on Authority in Online Mode
            if (Object == null || !Object.IsValid || Object.HasStateAuthority)
            {
                if (AbilityManager.instance != null && sourceData != null) 
                    AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_DEATH, this);
            }
            
            if (GameManager.instance != null) GameManager.instance.PlayDiscardAnimation(sourceData, isPlayerUnit);
            
            // Network Despawn
            if (Object != null && Object.IsValid) Runner.Despawn(Object);
            else Destroy(gameObject);
        }
        if (damage > 0) GameManager.instance.PlaySE(GameManager.instance.seDamage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ApplyDamage(int damage)
    {
        TakeDamage(damage);
    }

    private System.Collections.IEnumerator TackleAnimation(Transform target, System.Action onHitLogic) 
    { 
        isAnimating = true; 
        
        // [Fix] Capture current parent IMMEDIATELY before detaching.
        // This ensures the unit returns to its actual slot even if originalParent wasn't set.
        Transform currentParent = transform.parent;
        
        transform.SetParent(transform.root); 
        Vector3 startPos = transform.position; 
        Vector3 targetPos = target.position; 
        Vector3 attackEndPos = Vector3.Lerp(startPos, targetPos, 0.7f); 
        float duration = 0.15f; 
        float elapsed = 0f; 
        while (elapsed < duration) 
        { 
            transform.position = Vector3.Lerp(startPos, attackEndPos, elapsed / duration); 
            elapsed += Time.deltaTime; 
            yield return null; 
        } 
        transform.position = attackEndPos; 
        onHitLogic?.Invoke(); 
        
        yield return new WaitForSeconds(0.05f); 
        elapsed = 0f; 
        while (elapsed < duration) 
        { 
            transform.position = Vector3.Lerp(attackEndPos, startPos, elapsed / duration); 
            elapsed += Time.deltaTime; 
            yield return null; 
        } 
        
        // [Fix] Restore to the captured parent
        if (currentParent != null) 
        { 
            transform.SetParent(currentParent); 
            transform.localPosition = Vector3.zero; 
            originalParent = currentParent; // Sync our internal tracker as well
        } 
        isAnimating = false; 
    }
    
    // ... (rest of methods) ...
    public void MoveToSlot(Transform targetSlot, System.Action onComplete = null) 
    { 
        // ★FIX: Online Sync - Request move to Host
        if (Object != null && Object.IsValid && !HasStateAuthority)
        {
             SlotInfo info = targetSlot.GetComponent<SlotInfo>();
             if (info != null)
             {
                 RPC_MoveToSlot(info.x, info.y);
             }
        }

        // ★FIX: Online Sync - Host broadcasts move to others
        if (Object != null && Object.IsValid && HasStateAuthority)
        {
             SlotInfo info = targetSlot.GetComponent<SlotInfo>();
             if (info != null)
             {
                 RPC_BroadcastAnimateMove(info.x, info.y);
             }
        }

        StartCoroutine(MoveAnimation(targetSlot, onComplete)); 
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastAnimateMove(int x, int y)
    {
        // 既にアニメーション中ならスキップ（自分が移動元なら既に動いているはずだが念のため）
        if (isAnimating) return;

        bool isMyUnit = (Object != null && Object.IsValid) ? (OwnerPlayer == Runner.LocalPlayer) : isPlayerUnit;
        Transform targetBoard = isMyUnit ? GameObject.Find("PlayerBoard")?.transform : GameObject.Find("EnemyBoard")?.transform;

        if (targetBoard != null)
        {
            foreach (Transform slot in targetBoard)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                if (info != null && info.x == x && info.y == y)
                {
                    StartCoroutine(MoveAnimation(slot, null));
                    return;
                }
            }
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_MoveToSlot(int x, int y)
    {
         // Host receives request from Guest to move THIS unit to (x, y)
         // Determine target board (EnemyBoard from Host perspective)
         // However, standard logic: Host has UnitMover.isPlayerUnit = false (for Guest unit).
         // So MoveToSlot should naturally target EnemyBoard?
         // UnitMover.MoveToSlot() takes a Transform.
         // We must find the slot Transform on the correct board.
         
         Transform targetBoard = null;
         
         // If Owner is Host -> PlayerBoard
         // If Owner is Guest -> EnemyBoard
         if (OwnerPlayer == Runner.LocalPlayer) targetBoard = GameObject.Find("PlayerBoard").transform;
         else targetBoard = GameObject.Find("EnemyBoard").transform;
         
         if (targetBoard != null)
         {
             foreach(Transform slot in targetBoard)
             {
                 SlotInfo info = slot.GetComponent<SlotInfo>();
                 if (info != null && info.x == x && info.y == y)
                 {
                     MoveToSlot(slot); // Host moves it physically + Updates NetworkedSlotX/Y at end
                     return;
                 }
             }
         }
    }

    private System.Collections.IEnumerator MoveAnimation(Transform targetSlot, System.Action onComplete) 
    { 
        isAnimating = true; 
        transform.SetParent(transform.root); 
        Vector3 startPos = transform.position; 
        Vector3 endPos = targetSlot.position; 
        float duration = 0.2f; 
        float elapsed = 0f; 
        while (elapsed < duration) 
        { 
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration); 
            elapsed += Time.deltaTime; 
            yield return null; 
        } 
        transform.position = endPos; 
        
        // Finalize Parent (Local Logic)
        if (targetSlot != null) 
        {
             // Verify if we are fighting sync?
             // Since MoveToSlot is called on Guest, it sets Parent locally.
             // Then SyncParentSlot will later confirm it.
             transform.SetParent(targetSlot);
             transform.localPosition = Vector3.zero;
             originalParent = targetSlot;
        }

        // Network Update (Sync Slot Position) - ONLY HOST
        if (Object != null && Object.IsValid && HasStateAuthority)
        {
            SlotInfo info = targetSlot != null ? targetSlot.GetComponent<SlotInfo>() : null;
            if (info != null)
            {
                NetworkedSlotX = info.x;
                NetworkedSlotY = info.y;
            }
        }
        
        // Trigger ON_MOVE effects (Host Only or Local Offline)
        bool isAuth = (Object == null || !Object.IsValid || HasStateAuthority);
        if (isAuth && AbilityManager.instance != null && sourceData != null) 
        { 
            SlotInfo currentSlot = targetSlot != null ? targetSlot.GetComponent<SlotInfo>() : null;
            if (currentSlot != null) Debug.Log($"[UnitMover] Processing ON_MOVE for {sourceData.cardName} at Slot({currentSlot.x}, {currentSlot.y})");
            
            AbilityManager.instance.ProcessAbilities(sourceData, EffectTrigger.ON_MOVE, this); 
        }
        
        // Consume Action Logic (Host Only or Local Offline)
        if (isAuth) ConsumeAction();

        isAnimating = false; 
        
        // Trigger Callback
        onComplete?.Invoke();
    }
    public void PlaySummonAnimation() { StartCoroutine(SummonAnimationCoroutine()); }
    private System.Collections.IEnumerator SummonAnimationCoroutine() { isAnimating = true; Vector3 originalScale = transform.localScale; Vector3 landPos = transform.localPosition; Vector3 startPos = landPos + new Vector3(0, 50f, 0); transform.localPosition = startPos; transform.localScale = originalScale * 1.2f; if(canvasGroup) canvasGroup.alpha = 0f; float duration = 0.25f; float elapsed = 0f; while (elapsed < duration) { float t = elapsed / duration; t = t * t * (3f - 2f * t); transform.localPosition = Vector3.Lerp(startPos, landPos, t); transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t); if(canvasGroup) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t * 2); elapsed += Time.deltaTime; yield return null; } transform.localPosition = landPos; transform.localScale = originalScale; if(canvasGroup) canvasGroup.alpha = 1f; isAnimating = false; }
    public void Heal(int amount) { health += amount; if (health > maxHealth) health = maxHealth; if (unitView != null) unitView.RefreshDisplay(); }
    public void ConsumeAction() { canMove = false; canAttack = false; UpdateColor(); }
    public void ConsumeMove() { canMove = false; if (!hasQuick) canAttack = false; UpdateColor(); }
    public void ConsumeAttack() { canAttack = false; if (!hasQuick) canMove = false; UpdateColor(); }
    void UpdateColor() { if (GetComponent<UnityEngine.UI.Image>() == null) return; if (!canMove && !canAttack) GetComponent<UnityEngine.UI.Image>().color = Color.gray; else GetComponent<UnityEngine.UI.Image>().color = Color.white; }
}