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
    public Transform playerDeckIsland;

    [Header("リザルト・詳細表示")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;

    [Header("★新：カード拡大表示")]
    public GameObject enlargedCardPanel; // 拡大表示の親オブジェクト
    public CardView enlargedCardView;    // 拡大表示に使うCardViewコンポーネント

    [Header("ゲームデータ")]
    public int maxMana = 0;
    public int currentMana = 0;
    public bool isPlayerTurn = true;

    [Header("デッキ・手札")]
    public List<CardData> mainDeck = new List<CardData>();

    [Header("敵のステータス")]
    public Transform enemyBoard;
    public Transform playerLeader;
    public Transform enemyManaText;
    public int enemyMaxMana = 0;
    public int enemyCurrentMana = 0;

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
    // ★変更: BuildData ではなく CardData のリストにします
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

    [Header("敵UI")]
    public Transform enemyHandArea;
    public Transform enemyDeckIsland;
    public GameObject cardBackPrefab;

    [Header("ビルド表示用")]
    public Transform playerBuildArea;
    public Transform enemyBuildArea;
    public GameObject buildIconPrefab;

    [Header("マリガン")]
    public MulliganManager mulliganManager;
    private List<CardData> tempHand = new List<CardData>();

    // ターン数カウンター
    private int turnCount = 0;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        maxMana = 0;
        currentMana = 0;
        enemyMaxMana = 0;
        enemyCurrentMana = 0;

        // ★追加：拡大パネルの初期化（隠しておく）
        if (enlargedCardPanel != null) enlargedCardPanel.SetActive(false);
        if (enlargedCardView != null) 
        {
            enlargedCardView.enableHoverScale = false;  // 拡大表示自体がホバーで拡大しないように
            enlargedCardView.enableHoverDetail = false; // 無限ループ防止
        }

        UpdateManaUI();
        UpdateEnemyManaUI();

        SetupBoard(GameObject.Find("PlayerBoard").transform, false);
        SetupBoard(enemyBoard, true);

        SetupDeck();
        UpdateBuildUI();
        SetupLeaderIcon();

        if (targetArrowPrefab != null)
        {
            Transform canvas = handArea.parent; 
            currentArrow = Instantiate(targetArrowPrefab, canvas);
            currentArrow.gameObject.SetActive(false);
        }

        // マリガン開始
        StartMulliganSequence();
    }

    // --- マリガン処理 ---
    void StartMulliganSequence()
    {
        tempHand.Clear();
        for (int i = 0; i < 3; i++)
        {
            if (mainDeck.Count > 0)
            {
                tempHand.Add(mainDeck[0]);
                mainDeck.RemoveAt(0);
            }
        }

        if (mulliganManager != null)
        {
            mulliganManager.ShowMulligan(tempHand);
        }
        else
        {
            StartGameAfterMulligan();
        }
    }

    public void EndMulligan(List<bool> replaceFlags)
    {
        for (int i = 0; i < replaceFlags.Count; i++)
        {
            if (replaceFlags[i] && tempHand[i] != null)
            {
                mainDeck.Add(tempHand[i]);
                tempHand[i] = null;
            }
        }

        // シャッフル
        for (int i = 0; i < mainDeck.Count; i++)
        {
            CardData temp = mainDeck[i];
            int randomIndex = UnityEngine.Random.Range(i, mainDeck.Count);
            mainDeck[i] = mainDeck[randomIndex];
            mainDeck[randomIndex] = temp;
        }

        for (int i = 0; i < tempHand.Count; i++)
        {
            if (tempHand[i] == null && mainDeck.Count > 0)
            {
                tempHand[i] = mainDeck[0];
                mainDeck.RemoveAt(0);
            }
        }

        StartGameAfterMulligan();
    }

    void StartGameAfterMulligan()
    {
        // アニメーションなしで配置
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
        StartPlayerTurn();
    }

    // --- ターン進行 ---

    void StartPlayerTurn()
    {
        StartCoroutine(PlayerTurnSequence());
    }

    System.Collections.IEnumerator PlayerTurnSequence()
    {
        isPlayerTurn = true;
        
        // 画面切り替え待ち
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
        
        // ドロー
        StartCoroutine(DrawSequence(1, true));

        // ユニットリセット
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

        // ビルドリセット
        if (activeBuilds != null)
        {
            foreach (var build in activeBuilds)
            {
                if (build.isPlayerOwner)
                {
                    if (build.isUnderConstruction)
                    {
                        build.isUnderConstruction = false;
                        // ★修正：build.data.buildName -> build.data.cardName
                        Debug.Log(build.data.cardName + " 建築完了");
                    }
                    build.hasActed = false;
                }
            }
        }
        
        if (buildUIManager != null && buildUIManager.gameObject.activeSelf) 
        {
            buildUIManager.OpenMenu(true);
        }
        UpdateBuildUI();
    }

    public void OnClickEndTurn()
    {
        if (!isPlayerTurn) return;
        StartCoroutine(EndTurnSequence());
    }

    System.Collections.IEnumerator EndTurnSequence()
    {
        // ★修正：先に効果処理を行ってからターンフラグを折る
        // （そうしないとドロー効果などが「相手ターン」とみなされて発動しない）
        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, true);
        DecreaseBuildDuration(true);
        AbilityManager.instance.ProcessTurnEndEffects(true);

        // 効果処理が終わってからターン終了
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

        EnemyDrawCard(1);
        yield return new WaitForSeconds(1.0f);

        EnemySummon();
    }

    public void EnemyDrawCard(int count = 1)
    {
        StartCoroutine(DrawSequence(count, false));
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

                if (BattleLogManager.instance != null)
                     BattleLogManager.instance.AddLog($"敵は {selectedInfo.cardName} を召喚した", false);

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
        
        AbilityManager.instance.ProcessBuildEffects(EffectTrigger.ON_TURN_END, false);
        DecreaseBuildDuration(false);
        AbilityManager.instance.ProcessTurnEndEffects(false);

        Invoke("StartPlayerTurn", 1.0f);
    }

    // --- ドローアニメーション ---
    System.Collections.IEnumerator DrawSequence(int count, bool isPlayer)
    {
        Vector3 startPos;
        if (isPlayer)
            startPos = (playerDeckIsland != null) ? playerDeckIsland.position : new Vector3(800, -400, 0);
        else
            startPos = (enemyDeckIsland != null) ? enemyDeckIsland.position : new Vector3(800, 400, 0);

        Vector3 centerPos = (spellCastCenter != null) ? spellCastCenter.position : Vector3.zero;
        
        // ピボット調整
        float heightOffset = 150f * 3.0f * 0.5f;
        Vector3 adjustCenterPos = centerPos;
        if(isPlayer) adjustCenterPos -= new Vector3(0, heightOffset, 0);
        else adjustCenterPos += new Vector3(0, 100f, 0);

        Transform targetArea = isPlayer ? handArea : enemyHandArea;

        for (int i = 0; i < count; i++)
        {
            CardData cardData = null;
            if (isPlayer)
            {
                if (mainDeck.Count == 0) break;
                cardData = mainDeck[0];
                mainDeck.RemoveAt(0);
            }

            Transform tempParent = effectCanvasLayer != null ? effectCanvasLayer : handArea.root;
            GameObject cardObj = Instantiate(isPlayer ? cardPrefab.gameObject : cardBackPrefab, tempParent);
            
            var cg = cardObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; 

            cardObj.transform.localScale = Vector3.one * 0.5f; 
            cardObj.transform.rotation = Quaternion.identity;
            cardObj.transform.position = startPos;

            CardView view = cardObj.GetComponent<CardView>();
            if (isPlayer && view != null)
            {
                view.SetCard(cardData);
                view.ShowBack(true);
            }

            // 移動
            float moveTime = 0.25f;
            float elapsed = 0f;
            Vector3 initialScale = Vector3.one * 0.8f; 
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

            // めくり
            if (isPlayer && view != null)
            {
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
            else
            {
                yield return new WaitForSeconds(0.1f);
            }

            // 手札へ
            float flyTime = 0.2f;
            elapsed = 0f;
            Vector3 startFlyPos = cardObj.transform.position;
            Vector3 targetFlyPos = targetArea.position; 

            while (elapsed < flyTime)
            {
                float t = elapsed / flyTime;
                t = t * t;
                cardObj.transform.position = Vector3.Lerp(startFlyPos, targetFlyPos, t);
                cardObj.transform.localScale = Vector3.Lerp(centerScale, Vector3.one, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            cardObj.transform.SetParent(targetArea);
            cardObj.transform.localScale = Vector3.one;
            cardObj.transform.localRotation = Quaternion.identity;
            
            if (cg != null) cg.blocksRaycasts = true;

            if (isPlayer)
            {
                 if (view != null) view.ShowBack(false);
                 UpdateHandState();
                 if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog("カードを引いた", true);
            }
            else
            {
                 if (BattleLogManager.instance != null) BattleLogManager.instance.AddLog("相手がカードを引いた", false);
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // --- マナ・盤面設定 ---

    void SetupBoard(Transform board, bool isEnemy)
    {
        if (board == null) return;
        
        int index = 0;
        foreach (Transform slot in board)
        {
            SlotInfo info = slot.gameObject.GetComponent<SlotInfo>();
            if (info == null) info = slot.gameObject.AddComponent<SlotInfo>();

            // インデックス順：0,1 (ペア1), 2,3 (ペア2), 4,5 (ペア3)
            // X座標（レーン）: 0, 1, 2
            int col = index / 2; 
            info.x = col; 

            // Y座標（前後）: 0=前衛, 1=後衛
            if (!isEnemy)
            {
                // 自分: 0,2,4(偶数) = 前(0) / 1,3,5(奇数) = 後(1)
                info.y = index % 2; 
            }
            else
            {
                // 敵: 0,2,4(偶数) = 後(1) / 1,3,5(奇数) = 前(0)
                info.y = 1 - (index % 2); 
            }

            info.isEnemySlot = isEnemy;
            DropPlace dropPlace = slot.GetComponent<DropPlace>();
            if (dropPlace != null) dropPlace.isEnemySlot = isEnemy;
            index++;
        }
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

                // 色リセット
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

    // --- その他メソッド（変更なしだが全量記述） ---
    
    // 自分用ドロー（AbilityManager等から呼ばれる）
    public void DealCards(int count)
    {
        StartCoroutine(DrawSequence(count, true));
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
                // 敵のマナも同様にリセット＆設定
                enemyManaCrystals[i].color = Color.white;
                
                var crystalUI = enemyManaCrystals[i].GetComponent<ManaCrystalUI>();
                if (crystalUI == null) crystalUI = enemyManaCrystals[i].gameObject.AddComponent<ManaCrystalUI>();

                bool isActive = (i < enemyCurrentMana);
                crystalUI.SetState(isActive, manaOnSprite, manaOffSprite);

                enemyManaCrystals[i].gameObject.SetActive(i < enemyMaxMana);
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

        List<string> deckCardIds = new List<string>();
        List<string> deckBuildIds = new List<string>(); 

        // 1. PlayerDataManagerからデータ取得を試みる
        if (PlayerDataManager.instance != null)
        {
            var data = PlayerDataManager.instance.playerData;
            if (data.decks != null && data.decks.Count > 0)
            {
                if (data.currentDeckIndex >= data.decks.Count || data.currentDeckIndex < 0)
                {
                    data.currentDeckIndex = 0;
                }
                var deck = data.decks[data.currentDeckIndex];
                deckCardIds = deck.cardIds;
                deckBuildIds = deck.buildIds; 
            }
        }

        // 2. デッキ構築
        // ★修正：データがない場合は「テスト用ランダムデッキ」を生成する
        if (deckCardIds == null || deckCardIds.Count == 0)
        {
            Debug.LogWarning("デッキデータが見つかりません（または直接シーンを再生しています）。テスト用ランダムデッキを生成します。");
            
            // Resources/CardsData フォルダから全カードを読み込む
            CardData[] allCards = Resources.LoadAll<CardData>("CardsData");
            
            if (allCards.Length > 0)
            {
                // 30枚ランダムに選んでデッキに入れる
                for (int i = 0; i < 30; i++)
                {
                    CardData randomCard = allCards[Random.Range(0, allCards.Length)];
                    
                    // ビルド以外ならメインデッキへ
                    if (randomCard.type != CardType.BUILD)
                    {
                        mainDeck.Add(randomCard);
                    }
                    else
                    {
                        // ビルドならロードアウトへ（最大3つまで）
                        if (playerLoadoutBuilds.Count < 3 && !playerLoadoutBuilds.Contains(randomCard))
                        {
                            playerLoadoutBuilds.Add(randomCard);
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("Resources/CardsData にカードデータがありません！");
            }
        }
        else
        {
            // 通常の読み込み処理（PlayerDataManagerがある場合）
            foreach (string id in deckCardIds)
            {
                CardData card = PlayerDataManager.instance.GetCardById(id);
                if (card != null && card.type != CardType.BUILD)
                {
                    mainDeck.Add(card);
                }
            }
        }

        // 3. ビルドロードアウト構築 (正規データがある場合)
        if (deckBuildIds != null && deckBuildIds.Count > 0)
        {
            foreach (string id in deckBuildIds)
            {
                CardData card = PlayerDataManager.instance.GetCardById(id);
                if (card != null && card.type == CardType.BUILD)
                {
                    playerLoadoutBuilds.Add(card);
                }
            }
        }

        // シャッフル
        int n = mainDeck.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            CardData value = mainDeck[k];
            mainDeck[k] = mainDeck[n];
            mainDeck[n] = value;
        }

        Debug.Log($"デッキ構築完了！ デッキ: {mainDeck.Count}枚, ビルド: {playerLoadoutBuilds.Count}個");
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

    // ターゲット選択など
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
        CardData data = playerLoadoutBuilds[buildIndex];

        if (TryUseMana(data.cost))
        {
            if (activeBuilds == null) activeBuilds = new List<ActiveBuild>();
            activeBuilds.Add(new ActiveBuild(data, true)); // コンストラクタもCardData対応へ
            
            playerBuildCooldown = COOLDOWN_PLAYER;

            Debug.Log(data.cardName + " の建築を開始します..."); // buildName -> cardName
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
                    // ★修正：build.data.buildName -> build.data.cardName
                    Debug.Log(build.data.cardName + " の効果が切れました");
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

    public void ShowBuildDetail(CardData data, int currentLife = -1)
    {
        if (enlargedCardPanel == null || enlargedCardView == null || data == null) return;

        enlargedCardPanel.SetActive(true);

        if (enlargedCardView.nameText != null) enlargedCardView.nameText.text = data.cardName; // buildName -> cardName
        if (enlargedCardView.descText != null) enlargedCardView.descText.text = data.description;
        if (enlargedCardView.costText != null) enlargedCardView.costText.text = data.cost.ToString();
        
        if (enlargedCardView.iconImage != null)
        {
            if (data.cardIcon != null) // icon -> cardIcon
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

    bool ExistsActiveTaunt(Transform board)
    {
        if (board == null) return false;
        foreach(Transform slot in board)
        {
            if (slot.childCount > 0)
            {
                UnitMover unit = slot.GetChild(0).GetComponent<UnitMover>();
                // UnitMoverに追加したプロパティを使う
                if (unit != null && unit.IsTauntActive)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ユニットへの攻撃可否（ガード判定）
    public bool CanAttackUnit(UnitMover attacker, UnitMover target)
    {
        if (target.hasStealth) return false;
        Transform targetBoard = target.transform.parent.parent; // Slot -> Board
        // 2. ★修正：守護チェック（守護発動中の敵がいるなら、守護持ち以外は攻撃不可）
        if (ExistsActiveTaunt(targetBoard))
        {
            // ターゲット自身が守護発動中でなければ攻撃できない
            if (!target.IsTauntActive) return false;
        }

        SlotInfo targetSlot = target.transform.parent.GetComponent<SlotInfo>();
        if (targetSlot == null) return true;

        // 定義：「同じX座標で、0にモンスターがいるとき、1は攻撃されない」
        if (targetSlot.y == 1) // ターゲットが後衛(1)の時
        {
            Transform board = target.transform.parent.parent;
            foreach (Transform slot in board)
            {
                SlotInfo info = slot.GetComponent<SlotInfo>();
                
                // 同じ列(X) かつ 前衛(Y=0) にユニットがいるか
                if (info.x == targetSlot.x && info.y == 0 && slot.childCount > 0)
                {
                    return false; // 前衛がいるのでガードされる
                }
            }
        }
        return true;
    }
    
    // リーダーへの攻撃可否（鉄壁判定）
    // 「全ての列(X)の前衛(Y=0)が埋まっていると攻撃不可」と解釈します
    public bool CanAttackLeader(UnitMover attacker)
    {
        Transform targetBoard = attacker.isPlayerUnit ? enemyBoard : GameObject.Find("PlayerBoard").transform;
        if (targetBoard == null) return true;

        if (ExistsActiveTaunt(targetBoard))
        {
            return false;
        }

        bool[] isLaneBlocked = new bool[3]; // 横3列
        foreach (Transform slot in targetBoard)
        {
            SlotInfo info = slot.GetComponent<SlotInfo>();
            if (info != null && info.y == 0 && slot.childCount > 0) // 最前列のみチェック
            {
                isLaneBlocked[info.x] = true;
            }
        }
        
        // 全列の前衛が埋まっていたら攻撃不可
        if (isLaneBlocked[0] && isLaneBlocked[1] && isLaneBlocked[2]) return false;
        
        return true;
    }

    // ユニット・カードの詳細表示
    public void ShowUnitDetail(CardData data)
    {
        // 拡大パネルがない、またはデータがない場合は無視
        if (enlargedCardPanel == null || enlargedCardView == null || data == null) return;

        enlargedCardPanel.SetActive(true);
        
        // CardViewの機能を使ってデータをセット（これで見た目がカードそのものになる）
        enlargedCardView.SetCard(data);
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

    // 詳細を閉じる
    public void OnClickCloseDetail()
    {
        // 古いパネルを閉じる（念のため）
        // if (detailPanel != null) detailPanel.SetActive(false);

        // 新しい拡大パネルを閉じる
        if (enlargedCardPanel != null) enlargedCardPanel.SetActive(false);
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
    public CardData data; // BuildData -> CardData
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
