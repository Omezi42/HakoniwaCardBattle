using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UIパーツ")]
    public Transform handArea;
    public Transform playerManaText;
    public Transform endTurnButton;

    [Header("リザルト・詳細表示")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;
    public GameObject detailPanel;
    public Image detailIcon;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public TextMeshProUGUI detailStats;

    [Header("ゲームデータ")]
    public int maxMana = 1;
    public int currentMana = 1;
    public bool isPlayerTurn = true;

    [Header("デッキ・手札")]
    public List<CardData> mainDeck = new List<CardData>();

    [Header("敵のステータス")]
    public Transform enemyBoard;
    public Transform playerLeader;
    public Transform enemyManaText;
    public int enemyMaxMana = 1;
    public int enemyCurrentMana = 1;

    [Header("Prefab")]
    public CardView cardPrefab;
    public GameObject unitPrefabForEnemy;

    [Header("効果音(SE)")]
    public AudioClip seSummon;
    public AudioClip seAttack;
    public AudioClip seDamage;
    public AudioClip seWin;
    public AudioClip seLose;
    private AudioSource audioSource;

    [Header("ビルドシステム")]
    public List<BuildData> playerLoadoutBuilds;
    public List<BuildData> enemyLoadoutBuilds;
    public List<ActiveBuild> activeBuilds = new List<ActiveBuild>();

    [Header("リーダー画像")]
    // [0]Neutral, [1]Knight, [2]Mage, [3]Priest, [4]Rogue
    public Sprite[] leaderIcons;
    
    // ★修正：型を BuildUIManager に変更
    public BuildUIManager buildUIManager; 

    // クールタイム管理
    public int playerBuildCooldown = 0;
    public int enemyBuildCooldown = 0;
    
    private const int COOLDOWN_PLAYER = 5; 
    private const int COOLDOWN_ENEMY = 4;

    [Header("演出用プレハブ")]
    public GameObject floatingTextPrefab;
    public Transform effectCanvasLayer;
    
    [Header("UI演出")]
    public TurnCutIn turnCutIn;
    public TargetArrow targetArrowPrefab;
    private TargetArrow currentArrow;
    public SimpleTooltip simpleTooltip;

    [Header("マナUI")]
    public List<Image> playerManaCrystals; 
    public List<Image> enemyManaCrystals;

    public Sprite manaOnSprite;
    public Sprite manaOffSprite;

    // ビルド表示用
    public Transform playerBuildArea;
    public Transform enemyBuildArea;
    public GameObject buildIconPrefab;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (playerManaText == null)
        {
            GameObject foundObj = GameObject.Find("ManaText");
            if (foundObj != null) playerManaText = foundObj.transform;
        }

        maxMana = 0;
        currentMana = 0;
        enemyMaxMana = 0;
        enemyCurrentMana = 0;

        UpdateManaUI();
        UpdateEnemyManaUI();

        SetupBoard(GameObject.Find("PlayerBoard").transform, false);
        SetupBoard(enemyBoard, true);

        SetupDeck();
        DealCards(3);
        
        UpdateBuildUI();

        SetupLeaderIcon();

        SetupBoard(GameObject.Find("PlayerBoard").transform, false);

        if (targetArrowPrefab != null)
        {
            Transform canvas = handArea.parent; 
            currentArrow = Instantiate(targetArrowPrefab, canvas);
            currentArrow.gameObject.SetActive(false);
        }

        StartPlayerTurn();
    }

    public void ShowTooltip(string text)
    {
        if (simpleTooltip != null)
        {
            simpleTooltip.Show(text);
        }
    }

    public void HideTooltip()
    {
        if (simpleTooltip != null)
        {
            simpleTooltip.Hide();
        }
    }

    void SetupLeaderIcon()
    {
        if (PlayerDataManager.instance != null)
        {
            var data = PlayerDataManager.instance.playerData;
            if (data.decks != null && data.decks.Count > 0)
            {
                // 現在のデッキのジョブを取得
                int deckIndex = data.currentDeckIndex;
                if (deckIndex >= 0 && deckIndex < data.decks.Count)
                {
                    JobType currentJob = data.decks[deckIndex].deckJob;
                    int jobIndex = (int)currentJob;

                    // アイコン画像があるか確認
                    if (leaderIcons != null && jobIndex < leaderIcons.Length && leaderIcons[jobIndex] != null)
                    {
                        // プレイヤーリーダーの中にある「LeaderFace」を探して画像を差し替える
                        if (playerLeader != null)
                        {
                            var face = playerLeader.GetComponentInChildren<LeaderFace>();
                            if (face != null)
                            {
                                var img = face.GetComponent<UnityEngine.UI.Image>();
                                if (img != null)
                                {
                                    img.sprite = leaderIcons[jobIndex];
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void SetupDeck()
    {
        mainDeck.Clear();
        List<string> deckIds = new List<string>();

        // 1. PlayerDataManagerから現在のデッキIDリストを取得
        if (PlayerDataManager.instance != null)
        {
            // ★修正：新しいデータ構造に対応
            var data = PlayerDataManager.instance.playerData;
            
            // デッキが存在するか確認
            if (data.decks != null && data.decks.Count > 0)
            {
                // インデックスが範囲外なら0に戻す
                if (data.currentDeckIndex >= data.decks.Count || data.currentDeckIndex < 0)
                {
                    data.currentDeckIndex = 0;
                }

                // 選択中のデッキのカードリストを取得
                deckIds = data.decks[data.currentDeckIndex].cardIds;
            }
        }

        // 2. データがない場合（エディタで直接再生時など）はテスト用デッキ作成
        if (deckIds == null || deckIds.Count == 0)
        {
            Debug.Log("デッキデータがないため、ランダムなテストデッキを使用します。");
            CardData[] allCards = Resources.LoadAll<CardData>("CardsData");
            if (allCards.Length > 0)
            {
                for (int i = 0; i < 30; i++)
                {
                    mainDeck.Add(allCards[UnityEngine.Random.Range(0, allCards.Length)]);
                }
            }
        }
        else
        {
            // IDリストをカードデータに変換して山札に入れる
            foreach (string id in deckIds)
            {
                CardData card = PlayerDataManager.instance.GetCardById(id);
                if (card != null)
                {
                    mainDeck.Add(card);
                }
            }
        }

        // 3. シャッフル
        int n = mainDeck.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            CardData value = mainDeck[k];
            mainDeck[k] = mainDeck[n];
            mainDeck[n] = value;
        }

        Debug.Log($"デッキ構築完了！残り枚数: {mainDeck.Count}");
    }

    public void DealCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (mainDeck.Count == 0)
            {
                Debug.Log("山札がありません！");
                return;
            }

            CardData drawCard = mainDeck[0];
            mainDeck.RemoveAt(0);

            CardView newCard = Instantiate(cardPrefab, handArea);
            newCard.SetCard(drawCard);
        }
        UpdateHandState();
    }

    public void OnClickEndTurn()
    {
        if (!isPlayerTurn) return;

        ProcessBuildEffects(EffectTrigger.ON_TURN_END, true);
        DecreaseBuildDuration(true);
        ProcessTurnEndEffects(true);

        Debug.Log("ターン終了！敵のターンです。");
        isPlayerTurn = false;
        Invoke("StartEnemyTurn", 1.0f);
    }

    void StartEnemyTurn()
    {
        if (turnCutIn != null) turnCutIn.Show("ENEMY TURN", Color.red);
        if (enemyBuildCooldown > 0) enemyBuildCooldown--;
        if (enemyMaxMana < 10) enemyMaxMana++;
        enemyCurrentMana = enemyMaxMana;
        UpdateEnemyManaUI();

        if (enemyBoard != null)
        {
            UnitMover[] enemyUnits = enemyBoard.GetComponentsInChildren<UnitMover>();
            foreach (UnitMover enemyUnit in enemyUnits)
            {
                enemyUnit.canAttack = true;
                enemyUnit.canMove = true;
                var img = enemyUnit.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = Color.white;
            }
        }

        if (activeBuilds != null)
        {
            foreach (var build in activeBuilds)
            {
                if (!build.isPlayerOwner && build.isUnderConstruction)
                {
                    build.isUnderConstruction = false;
                }
            }
        }
        UpdateBuildUI();

        if (enemyBoard != null)
        {
            foreach (UnitMover enemyUnit in enemyBoard.GetComponentsInChildren<UnitMover>())
            {
                if (playerLeader != null)
                {
                    Leader leader = playerLeader.GetComponent<Leader>();
                    enemyUnit.Attack(leader);
                }
            }
        }

        Invoke("EnemySummon", 1.0f);
    }

    void EnemySummon()
    {
        Transform emptySlot = null;
        if (enemyBoard != null)
        {
            foreach (Transform slot in enemyBoard)
            {
                if (slot.childCount == 0)
                {
                    emptySlot = slot;
                    break;
                }
            }
        }

        if (emptySlot != null)
        {
            CardData[] allCards = Resources.LoadAll<CardData>("CardsData");
            List<CardData> playableCards = new List<CardData>();

            foreach (CardData card in allCards)
            {
                if (card.cost <= enemyCurrentMana && card.type == CardType.UNIT)
                {
                    playableCards.Add(card);
                }
            }

            if (playableCards.Count > 0)
            {
                CardData selectedInfo = playableCards[UnityEngine.Random.Range(0, playableCards.Count)];
                enemyCurrentMana -= selectedInfo.cost;
                UpdateEnemyManaUI();

                GameObject newUnit = Instantiate(unitPrefabForEnemy, emptySlot);
                newUnit.GetComponent<UnitView>().SetUnit(selectedInfo);
                UnitMover mover = newUnit.GetComponent<UnitMover>();
                
                mover.Initialize(selectedInfo, false);
                ProcessAbilities(selectedInfo, EffectTrigger.ON_SUMMON, mover);

                // ★追加：敵ユニットも召喚アニメーションを再生！
                mover.PlaySummonAnimation();

                Debug.Log("敵召喚: " + selectedInfo.cardName);
                PlaySE(seSummon);
            }
        }
        
        ProcessBuildEffects(EffectTrigger.ON_TURN_END, false);
        DecreaseBuildDuration(false);
        ProcessTurnEndEffects(false);

        Invoke("StartPlayerTurn", 1.0f);
    }

    void StartPlayerTurn()
    {
        isPlayerTurn = true;
        if (turnCutIn != null) turnCutIn.Show("YOUR TURN", Color.cyan);
        Debug.Log("自分のターン開始！");
        if (playerBuildCooldown > 0) playerBuildCooldown--;

        if (maxMana < 10) maxMana++;
        currentMana = maxMana;
        UpdateManaUI();
        DealCards(1);

        // ★修正：安全なユニット取得処理
        GameObject board = GameObject.Find("PlayerBoard");
        if (board != null)
        {
            UnitMover[] myUnits = board.GetComponentsInChildren<UnitMover>();
            foreach (UnitMover unit in myUnits)
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
                if (build.isPlayerOwner && build.isUnderConstruction)
                {
                    build.isUnderConstruction = false;
                    Debug.Log(build.data.buildName + " の建築が完了しました！");
                }
                build.hasActed = false;
            }
        }
        
        // ★修正：BuildUIManagerを使う形に修正
        if (buildUIManager != null && buildUIManager.gameObject.activeSelf) 
        {
            buildUIManager.OpenMenu(true);
        }
        
        UpdateBuildUI();
    }

    public void OpenBuildMenu()
    {
        // ★修正：BuildUIManagerを使用
        if (buildUIManager != null)
        {
            // 単純なトグルではなく、開く処理を呼ぶ（閉じるのはUI側のボタン等で）
            if (buildUIManager.gameObject.activeSelf)
                buildUIManager.CloseMenu();
            else
                buildUIManager.OpenMenu(true);
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
        BuildData data = playerLoadoutBuilds[buildIndex];

        if (TryUseMana(data.cost))
        {
            if (activeBuilds == null) activeBuilds = new List<ActiveBuild>();
            activeBuilds.Add(new ActiveBuild(data, true));
            
            playerBuildCooldown = COOLDOWN_PLAYER;

            Debug.Log(data.buildName + " の建築を開始します...");
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

    // ★修正：引数追加 (OpenBuildMenu(bool) で呼び出せるように)
    public void OpenBuildMenu(bool isPlayer)
    {
        if (buildUIManager != null)
        {
            buildUIManager.OpenMenu(isPlayer);
        }
    }

    public void ProcessBuildEffects(EffectTrigger trigger, bool isPlayerTurnStart)
    {
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
                    foreach (object target in targets)
                    {
                        ApplyEffect(target, ability.effect, ability.value, null);
                    }
                }
            }
        }
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
                    Debug.Log(build.data.buildName + " の効果が切れました");
                }
            }
        }
        UpdateBuildUI();
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

                    // ★追加：敵のビルドならアイコンを反転させる
                    if (!build.isPlayerOwner)
                    {
                        view.SetFlip(true);
                    }
                }
            }
        }
    }

    public void ShowBuildDetail(BuildData data, int currentLife = -1)
    {
        if (detailPanel == null) return;

        detailPanel.SetActive(true);

        if (data.icon != null) detailIcon.sprite = data.icon;

        detailName.text = data.buildName;
        detailDesc.text = data.description;

        int life = (currentLife != -1) ? currentLife : data.duration;
        detailStats.text = $"COST: {data.cost} / LIFE: {life}";
    }

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

    void ProcessTurnEndEffects(bool isPlayerEnding)
    {
        Transform board = isPlayerEnding ? GameObject.Find("PlayerBoard").transform : enemyBoard;
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

    List<object> GetTargets(EffectTarget targetType, UnitMover source, object manualTarget) 
    {
        List<object> results = new List<object>();
        bool isPlayerSide = (source != null) ? source.isPlayerUnit : isPlayerTurn;
        Transform enemyBoardTrans = isPlayerSide ? enemyBoard : GameObject.Find("PlayerBoard").transform;
        Transform myBoardTrans = isPlayerSide ? GameObject.Find("PlayerBoard").transform : enemyBoard;

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
                    var enemies = enemyBoardTrans.GetComponentsInChildren<UnitMover>();
                    if (enemies.Length > 0) results.Add(enemies[UnityEngine.Random.Range(0, enemies.Length)]);
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
        }
        return results;
    }

    void ApplyEffect(object target, EffectType effectType, int value, UnitMover source)
    {
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
                    if(u.GetComponent<UnitView>()) u.GetComponent<UnitView>().attackText.text = u.attackPower.ToString();
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
            case EffectType.GAIN_MANA:
                bool isPlayer = (source != null) ? source.isPlayerUnit : isPlayerTurn;
                if (isPlayer) { maxMana += value; currentMana += value; UpdateManaUI(); }
                else { enemyMaxMana += value; enemyCurrentMana += value; UpdateEnemyManaUI(); }
                break;
            case EffectType.DRAW_CARD:
                if (isPlayerTurn) DealCards(value);
                break;
            case EffectType.DESTROY:
                if (target is UnitMover) ((UnitMover)target).TakeDamage(9999);
                break;
        }
    }

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

    public void ShowArrow(Vector3 startPos)
    {
        if (currentArrow != null) currentArrow.Show(startPos);
    }

    public void UpdateArrow(Vector3 startPos, Vector3 currentPos)
    {
        if (currentArrow != null) currentArrow.UpdatePosition(startPos, currentPos);
    }

    public void HideArrow()
    {
        if (currentArrow != null) currentArrow.Hide();
    }

    public void SetArrowColor(Color color)
    {
        if (currentArrow != null) currentArrow.SetColor(color);
    }
    public void SetArrowLabel(string text, bool visible)
    {
        if (currentArrow != null)
        {
            currentArrow.SetLabel(text, visible);
        }
    }
    public bool IsArrowActive
    {
        get { return currentArrow != null && currentArrow.gameObject.activeSelf; }
    }

    public void ActivateBuildAbility(CardAbility ability, object target, ActiveBuild sourceBuild)
    {
        // 効果適用
        ApplyEffect(target, ability.effect, ability.value, null);
        
        // ★追加：行動済みにする
        if (sourceBuild != null)
        {
            sourceBuild.hasActed = true;
            UpdateBuildUI(); // 見た目を更新（暗くする）
        }
        
        Debug.Log($"ビルド効果発動: {ability.effect} -> {target}");
    }

    void UpdateManaUI()
    {
        if (playerManaText != null)
            playerManaText.GetComponent<TextMeshProUGUI>().text = $"Mana: {currentMana}/{maxMana}";
        
        if (playerManaCrystals != null)
        {
            for (int i = 0; i < playerManaCrystals.Count; i++)
            {
                if (playerManaCrystals[i] == null) continue;

                if (i < currentMana)
                {
                    playerManaCrystals[i].sprite = manaOnSprite;
                    playerManaCrystals[i].color = Color.white; 
                }
                else if (i < maxMana)
                {
                    playerManaCrystals[i].sprite = manaOffSprite;
                    playerManaCrystals[i].color = Color.white;
                }
                else
                {
                    playerManaCrystals[i].gameObject.SetActive(false); 
                }
                
                if (i < maxMana) playerManaCrystals[i].gameObject.SetActive(true);
            }
        }
        UpdateHandState();
    }

    void UpdateEnemyManaUI()
    {
        if (enemyManaText != null)
            enemyManaText.GetComponent<TextMeshProUGUI>().text = $"Mana: {enemyCurrentMana}/{enemyMaxMana}";

        if (enemyManaCrystals != null)
        {
            for (int i = 0; i < enemyManaCrystals.Count; i++)
            {
                if (enemyManaCrystals[i] == null) continue;

                if (i < enemyCurrentMana)
                {
                    enemyManaCrystals[i].sprite = manaOnSprite;
                    enemyManaCrystals[i].color = Color.white;
                }
                else if (i < enemyMaxMana)
                {
                    enemyManaCrystals[i].sprite = manaOffSprite;
                    enemyManaCrystals[i].color = Color.white;
                }
                else
                {
                    enemyManaCrystals[i].gameObject.SetActive(false);
                }

                if (i < enemyMaxMana)
                {
                    enemyManaCrystals[i].gameObject.SetActive(true);
                }
            }
        }
    }

    public void CastSpell(CardData card)
    {
        ProcessAbilities(card, EffectTrigger.SPELL_USE, null);
    }

    public bool CanAttackUnit(UnitMover attacker, UnitMover target)
    {
        if (target.hasStealth) return false;
        SlotInfo targetSlot = target.transform.parent.GetComponent<SlotInfo>();
        if (targetSlot == null) return true;
        if (targetSlot.x == 1) 
        {
            Transform board = target.transform.parent.parent;
            foreach (Transform slot in board)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                if (info.y == targetSlot.y && info.x == 0 && slot.childCount > 0) return false;
            }
        }
        return true;
    }

    public bool CanAttackLeader(UnitMover attacker)
    {
        Transform targetBoard = attacker.isPlayerUnit ? enemyBoard : GameObject.Find("PlayerBoard").transform;
        if (targetBoard == null) return true;

        bool[] hasUnitInRow = new bool[3]; 
        foreach (Transform slot in targetBoard)
        {
            if (slot.childCount > 0)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                if (info != null) hasUnitInRow[info.y] = true;
            }
        }
        if (hasUnitInRow[0] && hasUnitInRow[1] && hasUnitInRow[2]) return false;
        return true;
    }

    UnitMover GetFrontEnemy(UnitMover me)
    {
        SlotInfo mySlot = me.transform.parent.GetComponent<SlotInfo>();
        if (mySlot == null) return null;
        Transform targetBoard = me.isPlayerUnit ? enemyBoard : GameObject.Find("PlayerBoard").transform;
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

    void SetupBoard(Transform board, bool isEnemy)
    {
        if (board == null) return;
        int index = 0;
        foreach (Transform slot in board)
        {
            SlotInfo info = slot.gameObject.GetComponent<SlotInfo>();
            if (info == null) info = slot.gameObject.AddComponent<SlotInfo>();
            info.x = index % 2;
            info.y = index / 2;
            info.isEnemySlot = isEnemy;
            DropPlace dropPlace = slot.GetComponent<DropPlace>();
            if (dropPlace != null) dropPlace.isEnemySlot = isEnemy;
            index++;
        }
    }

    public void ShowUnitDetail(CardData data)
    {
        if (detailPanel == null) return;
        detailPanel.SetActive(true);
        if (data.cardIcon != null) detailIcon.sprite = data.cardIcon;
        detailName.text = data.cardName;
        detailDesc.text = data.description;
        detailStats.text = (data.type == CardType.UNIT) ? $"ATK: {data.attack} / HP: {data.health}" : "SPELL";
    }

    public void SpawnDamageText(Vector3 worldPos, int damage)
    {
        if (floatingTextPrefab == null) return;

        Transform parent = effectCanvasLayer != null ? effectCanvasLayer : (handArea != null ? handArea.parent : null);
        if (parent == null) return;
        
        GameObject obj = Instantiate(floatingTextPrefab, parent);
        obj.transform.position = worldPos + new Vector3(0, 50f, 0); 
        obj.GetComponent<FloatingText>().Setup(damage);
    }

    public void OnClickCloseDetail()
    {
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    void UpdateHandState()
    {
        if (handArea == null) return;
        foreach (Transform child in handArea)
        {
            CardView cardView = child.GetComponent<CardView>();
            if (cardView != null && cardView.cardData != null)
            {
                bool isPlayable = (currentMana >= cardView.cardData.cost);
                if (!isPlayerTurn) isPlayable = false;
                cardView.SetPlayableState(isPlayable);
            }
        }
    }

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

    public void PlaySE(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }
}

[System.Serializable]
public class ActiveBuild
{
    public BuildData data;
    public int remainingTurns;
    public bool isPlayerOwner;
    public bool isUnderConstruction;
    
    // ★追加：このターンに行動したかどうかのフラグ
    public bool hasActed = false;

    public ActiveBuild(BuildData data, bool isPlayer)
    {
        this.data = data;
        this.remainingTurns = data.duration;
        this.isPlayerOwner = isPlayer;
        this.isUnderConstruction = true;
        this.hasActed = false;
    }
}