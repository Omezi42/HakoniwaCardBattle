using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq; 

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UIパーツ")]
    public Transform handArea;
    public Transform playerManaText;
    public Transform endTurnButton;
    public Transform playerDeckIsland;
    public GameObject playerDeckVisual; 
    public GameObject playerGraveVisual;

    [Header("情報表示")]
    public TextMeshProUGUI playerDeckCountText;
    public TextMeshProUGUI enemyDeckCountText;

    [Header("リザルト・詳細表示")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;

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
    public List<CardData> mainDeck = new List<CardData>();

    [Header("敵のステータス")]
    public Transform enemyBoard;
    public Transform playerLeader;
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
    private const int COOLDOWN_PLAYER = 5; 
    private const int COOLDOWN_ENEMY = 4;

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

    private int turnCount = 0;

    #region Singleton & Lifecycle
    private void Awake() { if (instance == null) instance = this; }

    void Start()
    {
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

        UpdateManaUI();
        UpdateEnemyManaUI();
        SetupBoard(GameObject.Find("PlayerBoard").transform, false);
        SetupBoard(enemyBoard, true);
        SetupDeck();
        SetupEnemyDeck();
        UpdateBuildUI();
        SetupLeaderIcon();
        UpdateDeckGraveyardVisuals();

        if (targetArrowPrefab != null)
        {
            Transform canvas = handArea.parent; 
            currentArrow = Instantiate(targetArrowPrefab, canvas);
            currentArrow.gameObject.SetActive(false);
        }

        StartMulliganSequence();
    }
    #endregion

    #region Mulligan
    void StartMulliganSequence()
    {
        tempHand.Clear();
        for (int i = 0; i < 3; i++) { if (mainDeck.Count > 0) { tempHand.Add(mainDeck[0]); mainDeck.RemoveAt(0); } }
        if (mulliganManager != null) mulliganManager.ShowMulligan(tempHand);
        else StartGameAfterMulligan();
    }

    public void EndMulligan(List<bool> replaceFlags)
    {
        for (int i = 0; i < replaceFlags.Count; i++) { if (replaceFlags[i] && tempHand[i] != null) { mainDeck.Add(tempHand[i]); tempHand[i] = null; } }
        ShuffleDeck();
        for (int i = 0; i < tempHand.Count; i++) { if (tempHand[i] == null && mainDeck.Count > 0) { tempHand[i] = mainDeck[0]; mainDeck.RemoveAt(0); } }
        StartGameAfterMulligan();
    }
    #endregion

    #region Card Manipulation
    public void StartGameAfterMulligan()
    {
        foreach (var data in tempHand)
        {
            if (data == null) continue;
            CardView newCard = Instantiate(cardPrefab, handArea);
            newCard.SetCard(data);
            newCard.transform.localScale = Vector3.one;
            newCard.transform.localRotation = Quaternion.identity;
            newCard.ShowBack(false);
        }
        UpdateHandState();
        UpdateDeckGraveyardVisuals();
        InitializeEnemyHand(3); 
        StartPlayerTurn();
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
    void StartPlayerTurn() 
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
            turnCutIn.Show("YOUR TURN", Color.cyan); 
        } 
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
    }

    public void OnClickEndTurn() 
    { 
        if (!isPlayerTurn) return; 
        StartCoroutine(EndTurnSequence()); 
    }

    System.Collections.IEnumerator EndTurnSequence() 
    { 
        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, true); 
        DecreaseBuildDuration(true); 
        AbilityManager.instance.ProcessTurnEndEffects(true); 

        isPlayerTurn = false; 
        yield return new WaitForSeconds(1.0f); 
        StartEnemyTurn(); 
    }

    void StartEnemyTurn() 
    { 
        StartCoroutine(EnemyTurnSequence()); 
    }

    System.Collections.IEnumerator EnemyTurnSequence() 
    { 
        if (turnCutIn != null) turnCutIn.Show("ENEMY TURN", Color.red); 
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
    }
    #endregion

    #region Enemy AI
    System.Collections.IEnumerator EnemyAttackPhase() 
    { 
        if (enemyBoard == null) yield break; 

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
                    if (u.IsTauntActive) tauntUnits.Add(u); 
                } 
            } 

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

            enemyBuildCooldown = COOLDOWN_ENEMY; 
            if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"敵は {buildable.cardName} を建設した", false, buildable); 
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

        GameObject newUnit = Instantiate(unitPrefabForEnemy, emptySlot); 
        newUnit.GetComponent<UnitView>().SetUnit(card); 

        UnitMover mover = newUnit.GetComponent<UnitMover>(); 
        mover.Initialize(card, false); 

        object manualTarget = null; 
        if (HasSelectTargetAbility(card)) manualTarget = DecideBestTarget(card, mover); 

        AbilityManager.instance.ProcessAbilities(card, EffectTrigger.ON_SUMMON, mover, manualTarget); 
        mover.PlaySummonAnimation(); 

        if (mover.hasHaste) mover.canAttack = true; 

        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"敵は {card.cardName} を召喚した", false, card); 
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

        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"敵は {card.cardName} を唱えた", false, card); 
        PlaySE(seSummon); 
        return true; 
    }

    bool TryBuildEnemy(CardData card) 
    { 
        if (activeBuilds == null) activeBuilds = new List<ActiveBuild>(); 
        activeBuilds.Add(new ActiveBuild(card, false)); 

        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($"敵は {card.cardName} を建設した", false, card); 
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
            var targets = playerBoard.GetComponentsInChildren<UnitMover>().OrderByDescending(u => u.attackPower).ToList(); 
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

            Transform tempParent = effectCanvasLayer != null ? effectCanvasLayer : handArea.root; 
            GameObject cardObj = Instantiate(isPlayer ? cardPrefab.gameObject : cardBackPrefab, tempParent); 
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

            if (isPlayer && !silent && BattleLogManager.instance != null) BattleLogManager.instance.AddLog("カードを引いた", true); 
            else if (!isPlayer && !silent && BattleLogManager.instance != null) BattleLogManager.instance.AddLog("相手がカードを引いた", false); 

            UpdateHandState(); 
            UpdateDeckGraveyardVisuals(); 
            yield return new WaitForSeconds(0.05f); 
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
    }

    public void PlayDiscardAnimation(CardData card, bool isPlayer) 
    { 
        if (cardPrefab == null) return; 

        Vector3 endPos = isPlayer ? (playerDeckIsland ? playerDeckIsland.position : Vector3.zero) : (enemyDeckIsland ? enemyDeckIsland.position : Vector3.zero); 
        if (isPlayer && playerGraveVisual != null) endPos = playerGraveVisual.transform.position; 
        if (!isPlayer && enemyGraveVisual != null) endPos = enemyGraveVisual.transform.position; 

        Vector3 startPos = endPos + new Vector3(0, 100f, 0); 

        GameObject obj = Instantiate(cardPrefab.gameObject, effectCanvasLayer); 
        CardView view = obj.GetComponent<CardView>(); 
        if (view != null) view.SetCard(card); 

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
        if (isTargetingMode && currentCastingCard != null) 
        { 
            if (Input.GetMouseButtonDown(1)) 
            { 
                CancelSpellCast(); 
                return; 
            } 

            UpdateArrow(arrowStartPos, Input.mousePosition); 
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
        TryUseMana(currentCastingCard.cardData.cost);
        AbilityManager.instance.ProcessAbilities(currentCastingCard.cardData, EffectTrigger.SPELL_USE, null, manualTarget);
        AbilityManager.instance.TriggerSpellReaction(isPlayerTurn);
        PlayDiscardAnimation(currentCastingCard.cardData, true);
        if(BattleLogManager.instance!=null) BattleLogManager.instance.AddLog($" {currentCastingCard.cardData.cardName} を唱えた", true, currentCastingCard.cardData);
        Destroy(currentCastingCard.gameObject);
        CleanupSpellMode();
        PlaySE(seSummon);
    }

    public void StartUnitTargeting(CardView card, Transform slot)
    {
        if (currentMana < card.cardData.cost)
        {
            Debug.Log("No Mana");
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

    public void StartSpellCast(CardView card)
    {
        if (currentMana < card.cardData.cost) { Debug.Log("No Mana"); return; }
        currentCastingCard = card; 
        card.transform.SetParent(spellCastCenter); 
        card.transform.localPosition = Vector3.zero;
        arrowStartPos = spellCastCenter.position;
        if (CheckIfSpellNeedsTarget(card.cardData)) { isTargetingMode = true; ShowArrow(arrowStartPos); SetArrowColor(Color.white); ShowUnitDetail(card.cardData); }
        else { if (card.cardData.type == CardType.SPELL) ExecuteSpell(null); else CancelSpellCast(); }
    }

    void TryCastSpellToTarget(GameObject targetObj)
    {
        UnitMover unit = targetObj.GetComponentInParent<UnitMover>(); 
        Leader leader = targetObj.GetComponentInParent<Leader>(); 
        object target = (unit != null) ? (object)unit : leader;
        if (target != null) { if (currentCastingCard.cardData.type == CardType.UNIT) SummonPlayerUnit(target); else ExecuteSpell(target); }
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
        GameObject newUnit = Instantiate(playerUnitPrefab != null ? playerUnitPrefab : unitPrefabForEnemy, targetSlot);
        UnitView view = newUnit.GetComponent<UnitView>(); if (view != null) view.SetUnit(card);
        UnitMover mover = newUnit.GetComponent<UnitMover>(); if (mover != null) mover.Initialize(card, true);
        AbilityManager.instance.ProcessAbilities(card, EffectTrigger.ON_SUMMON, mover, manualTarget);
        mover.PlaySummonAnimation();
        if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog($" {card.cardName} を召喚した", true, card);
        PlaySE(seSummon);
        Destroy(currentCastingCard.gameObject);
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

    void ShuffleDeck() 
    { 
        for (int i = 0; i < mainDeck.Count; i++) 
        { 
            CardData temp = mainDeck[i]; 
            int r = Random.Range(i, mainDeck.Count); 
            mainDeck[i] = mainDeck[r]; 
            mainDeck[r] = temp; 
        } 
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
            playerBuildCooldown = COOLDOWN_PLAYER; 
            Debug.Log(data.cardName + " の建築を開始します..."); 
            PlaySE(seSummon); 
            UpdateBuildUI(); 
        } 
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
        if (resultPanel != null) resultPanel.SetActive(true); 

        GameObject bgm = GameObject.Find("BGM Player"); 
        if(bgm != null) bgm.GetComponent<AudioSource>().Stop(); 

        if (resultText != null) 
        { 
            if (isPlayerWin) 
            { 
                resultText.text = "YOU WIN!"; 
                resultText.color = Color.yellow; 
                PlaySE(seWin); 
            } 
            else 
            { 
                resultText.text = "YOU LOSE..."; 
                resultText.color = Color.red; 
                PlaySE(seLose); 
            } 
        } 
        isPlayerTurn = false; 
    }

    public void OnClickRetry() 
    { 
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
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
            Transform t=effectCanvasLayer?effectCanvasLayer:(handArea?handArea.parent:null); 
            if(t)
            { 
                var o=Instantiate(floatingTextPrefab,t); 
                o.transform.position=p+new Vector3(0,50,0); 
                o.GetComponent<FloatingText>().Setup(d);
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
        if (playerManaText != null) playerManaText.GetComponent<TextMeshProUGUI>().text = $"Mana: {currentMana}/{maxMana}"; 
        if (playerManaCrystals != null) 
        { 
            for (int i = 0; i < playerManaCrystals.Count; i++) 
            { 
                if (playerManaCrystals[i] == null) continue; 
                playerManaCrystals[i].color = Color.white; 
                var crystalUI = playerManaCrystals[i].GetComponent<ManaCrystalUI>(); 
                if (crystalUI == null) crystalUI = playerManaCrystals[i].gameObject.AddComponent<ManaCrystalUI>(); 
                bool isActive = (i < currentMana); 
                crystalUI.SetState(isActive, manaOnSprite, manaOffSprite); 
                playerManaCrystals[i].gameObject.SetActive(i < maxMana); 
            } 
        } 
        UpdateHandState(); 
    }

    public void UpdateEnemyManaUI() 
    { 
        if (enemyManaText != null) enemyManaText.GetComponent<TextMeshProUGUI>().text = $"Mana: {enemyCurrentMana}/{enemyMaxMana}"; 
        if (enemyManaCrystals != null) 
        { 
            for (int i = 0; i < enemyManaCrystals.Count; i++) 
            { 
                if (enemyManaCrystals[i] == null) continue; 
                enemyManaCrystals[i].color = Color.white; 
                var crystalUI = enemyManaCrystals[i].GetComponent<ManaCrystalUI>(); 
                if (crystalUI == null) crystalUI = enemyManaCrystals[i].gameObject.AddComponent<ManaCrystalUI>(); 
                bool isActive = (i < enemyCurrentMana); 
                crystalUI.SetState(isActive, manaOnSprite, manaOffSprite); 
                enemyManaCrystals[i].gameObject.SetActive(i < enemyMaxMana); 
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
            int col = index / 2; 
            info.x = col; 
            if (!isEnemy) info.y = index % 2; 
            else info.y = 1 - (index % 2); 
            info.isEnemySlot = isEnemy; 
            DropPlace dropPlace = slot.GetComponent<DropPlace>(); 
            if (dropPlace != null) dropPlace.isEnemySlot = isEnemy; 
            index++; 
        } 
    }

    void UpdateBuildUI() 
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
                            var face = playerLeader.GetComponentInChildren<LeaderFace>(); 
                            if (face != null) face.GetComponent<UnityEngine.UI.Image>().sprite = leaderIcons[jobIndex]; 
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

    void DecreaseBuildDuration(bool isPlayer) 
    { 
        if (activeBuilds == null) return; 

        for (int i = activeBuilds.Count - 1; i >= 0; i--) 
        { 
            ActiveBuild build = activeBuilds[i]; 
            if (build.isPlayerOwner == isPlayer && !build.isUnderConstruction) 
            { 
                build.remainingTurns--; 
                if (build.remainingTurns <= 0) 
                { 
                    activeBuilds.RemoveAt(i); 
                    Debug.Log(build.data.cardName + " の効果が切れました"); 
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
            if(i&&i.y==0&&s.childCount>0)
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
    #endregion
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