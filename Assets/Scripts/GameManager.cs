using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

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
    public List<BuildData> enemyLoadoutBuilds; // 敵用リスト
    public List<ActiveBuild> activeBuilds = new List<ActiveBuild>();
    
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
    public SimpleTooltip simpleTooltip; // ツールチップ

    [Header("マナUI")]
    public List<Image> playerManaCrystals; 
    public List<Image> enemyManaCrystals;

    public Sprite manaOnSprite;
    public Sprite manaOffSprite;

    [Header("リーダー画像")]
    public Sprite[] leaderIcons; 

    [Header("スペル詠唱")]
    public Transform spellCastCenter; // 画面中央の座標（Canvas内に空オブジェクトを作って割り当てる）
    public CardView currentCastingCard; // 現在詠唱待機中のカード
    public bool isTargetingMode = false; // ターゲット選択中か

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

        if (targetArrowPrefab != null)
        {
            Transform canvas = handArea.parent; 
            currentArrow = Instantiate(targetArrowPrefab, canvas);
            currentArrow.gameObject.SetActive(false);
        }

        StartPlayerTurn();
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
                    JobType currentJob = data.decks[deckIndex].deckJob;
                    int jobIndex = (int)currentJob;

                    if (leaderIcons != null && jobIndex < leaderIcons.Length && leaderIcons[jobIndex] != null)
                    {
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

        if (PlayerDataManager.instance != null)
        {
            var data = PlayerDataManager.instance.playerData;
            if (data.decks != null && data.decks.Count > 0)
            {
                if (data.currentDeckIndex >= data.decks.Count || data.currentDeckIndex < 0)
                {
                    data.currentDeckIndex = 0;
                }
                deckIds = data.decks[data.currentDeckIndex].cardIds;
            }
        }

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
            foreach (string id in deckIds)
            {
                CardData card = PlayerDataManager.instance.GetCardById(id);
                if (card != null)
                {
                    mainDeck.Add(card);
                }
            }
        }

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

        // ★修正：AbilityManager経由で呼び出す
        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, true);
        
        DecreaseBuildDuration(true);
        AbilityManager.instance.ProcessTurnEndEffects(true);

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
                AbilityManager.instance.ProcessAbilities(selectedInfo, EffectTrigger.ON_SUMMON, mover);

                mover.PlaySummonAnimation();

                Debug.Log("敵召喚: " + selectedInfo.cardName);
                PlaySE(seSummon);
            }
        }
        
        // ★修正：AbilityManager経由で呼び出す
        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, false);
        
        DecreaseBuildDuration(false);
        AbilityManager.instance.ProcessTurnEndEffects(false);

        Invoke("StartPlayerTurn", 1.0f);
    }

    void Update()
    {
        // ターゲット選択中の処理
        if (isTargetingMode && currentCastingCard != null)
        {
            // 右クリックでキャンセル
            if (Input.GetMouseButtonDown(1))
            {
                CancelSpellCast();
                return;
            }

            // 矢印の更新（カード中心からマウスへ）
            UpdateArrow(spellCastCenter.position, Input.mousePosition);

            GameObject targetObj = GetObjectUnderMouse();
            bool isValidTarget = false;

            if (targetObj != null)
            {
                // マウス下のユニット・リーダーを取得
                UnitMover unit = targetObj.GetComponentInParent<UnitMover>();
                Leader leader = targetObj.GetComponentInParent<Leader>();

                // カードのターゲット条件と合致するかチェック
                // （簡易チェック：敵ユニット指定なら敵ユニットか？）
                foreach (var ability in currentCastingCard.cardData.abilities)
                {
                    if (CheckTargetValidity(ability.target, unit, leader))
                    {
                        isValidTarget = true;
                        break;
                    }
                }
            }

            // 色の適用
            SetArrowColor(isValidTarget ? Color.red : Color.white);

            // クリックで決定
            if (Input.GetMouseButtonDown(0))
            {
                // ★修正：isValidTarget が true の場合だけ発動する！
                // （以前は targetObj != null だけで発動していました）
                if (isValidTarget && targetObj != null)
                {
                    TryCastSpellToTarget(targetObj);
                }
                else
                {
                    CancelSpellCast(); 
                }
            }
        }
    }

    bool CheckTargetValidity(EffectTarget targetType, UnitMover unit, Leader leader)
    {
        if (unit != null)
        {
            if (targetType == EffectTarget.SELECT_ENEMY_UNIT || targetType == EffectTarget.SELECT_ANY_ENEMY)
                return !unit.isPlayerUnit; 

            // ★追加：ダメージを受けていない敵ユニットかチェック
            if (targetType == EffectTarget.SELECT_UNDAMAGED_ENEMY)
            {
                // 敵であり、かつ HPが最大値以上（＝減っていない）ならOK
                return !unit.isPlayerUnit && (unit.health >= unit.maxHealth);
            }
        }
        else if (leader != null)
        {
            bool isEnemyLeader = (leader.transform.parent.name == "EnemyBoard" || leader.name == "EnemyInfo");
            
            if (targetType == EffectTarget.SELECT_ENEMY_LEADER || targetType == EffectTarget.SELECT_ANY_ENEMY)
                return isEnemyLeader;
        }
        return false;
    }

    // レイキャストヘルパー
    GameObject GetObjectUnderMouse()
    {
        PointerEventData pointerData = new PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        pointerData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);
        
        if (results.Count > 0) return results[0].gameObject;
        return null;
    }

    // ★スペル詠唱開始（DraggableやDropPlaceから呼ばれる）
    public void StartSpellCast(CardView card)
    {
        // マナチェック
        if (currentMana < card.cardData.cost)
        {
            Debug.Log("マナが足りません");
            // 元の位置に戻す処理が必要（Draggable側で対応）
            return;
        }

        // カードを中央へ移動
        currentCastingCard = card;
        card.transform.SetParent(spellCastCenter);
        card.transform.localPosition = Vector3.zero;
        
        // ターゲットが必要なスペルか判定
        bool needsTarget = CheckIfSpellNeedsTarget(card.cardData);

        if (needsTarget)
        {
            // ターゲットモード移行
            isTargetingMode = true;
            ShowArrow(spellCastCenter.position);
            SetArrowColor(Color.white); // ターゲット探索色
            
            // 右上に説明ウィンドウを出す（ShowUnitDetailを流用）
            ShowUnitDetail(card.cardData); 
            // ※本来は専用の「効果説明小窓」を出すべきですが、既存UIを使います
        }
        else
        {
            // ターゲット不要なら即発動
            ExecuteSpell(null);
        }
    }

    bool CheckIfSpellNeedsTarget(CardData data)
    {
        foreach(var ab in data.abilities)
        {
            if (ab.target == EffectTarget.SELECT_ENEMY_UNIT || 
                ab.target == EffectTarget.SELECT_ENEMY_LEADER || 
                ab.target == EffectTarget.SELECT_ANY_ENEMY ||
                ab.target == EffectTarget.SELECT_UNDAMAGED_ENEMY)
                return true;
        }
        return false;
    }

    // ターゲット指定発動
    void TryCastSpellToTarget(GameObject targetObj)
    {
        UnitMover unit = targetObj.GetComponentInParent<UnitMover>();
        Leader leader = targetObj.GetComponentInParent<Leader>();

        object target = null;
        if (unit != null) target = unit;
        else if (leader != null) target = leader;

        if (target != null)
        {
            // ここで「有効なターゲットか（敵味方など）」を判定すべきですが、
            // AbilityManager側で弾くか、ここで簡易チェックします
            ExecuteSpell(target);
        }
    }

    void ExecuteSpell(object manualTarget)
    {
        // マナ消費
        TryUseMana(currentCastingCard.cardData.cost); // ここで減らす

        // 発動
        AbilityManager.instance.ProcessAbilities(currentCastingCard.cardData, EffectTrigger.SPELL_USE, null, manualTarget);
        
        // 後始末
        Destroy(currentCastingCard.gameObject);
        CleanupSpellMode();
        PlaySE(seSummon); // 魔法音
    }

    public void CancelSpellCast()
    {
        if (currentCastingCard != null)
        {
            // ★修正：手札エリアに戻す
            currentCastingCard.transform.SetParent(handArea);
            
            // サイズと位置をリセット
            currentCastingCard.transform.localScale = Vector3.one;
            currentCastingCard.transform.localPosition = Vector3.zero; // 必要に応じて調整

            // 再び操作できるようにレイキャストをブロックする
            var group = currentCastingCard.GetComponent<CanvasGroup>();
            if (group != null) group.blocksRaycasts = true;

            // ドラッグスクリプトの状態もリセットしておく
            var drag = currentCastingCard.GetComponent<Draggable>();
            if (drag != null) drag.originalParent = handArea;
        }
        CleanupSpellMode();
    }

    void CleanupSpellMode()
    {
        isTargetingMode = false;
        currentCastingCard = null;
        HideArrow();
        OnClickCloseDetail(); // 小窓を消す
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
        
        if (buildUIManager != null && buildUIManager.gameObject.activeSelf) 
        {
            buildUIManager.OpenMenu(true);
        }
        
        UpdateBuildUI();
    }

    public void OpenBuildMenu()
    {
        if (buildUIManager != null)
        {
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

    public void OpenBuildMenu(bool isPlayer)
    {
        if (buildUIManager != null)
        {
            buildUIManager.OpenMenu(isPlayer);
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

    public void ShowTooltip(string text)
    {
        if (simpleTooltip != null) simpleTooltip.Show(text);
    }

    public void HideTooltip()
    {
        if (simpleTooltip != null) simpleTooltip.Hide();
    }

    public void UpdateManaUI()
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

    public void UpdateEnemyManaUI()
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