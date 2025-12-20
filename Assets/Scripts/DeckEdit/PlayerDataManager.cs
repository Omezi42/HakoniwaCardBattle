using UnityEngine;
using System.Collections.Generic;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager instance;

    public PlayerData playerData = new PlayerData();
    private Dictionary<string, CardData> cardDatabase = new Dictionary<string, CardData>();
    
    // AI Difficulty (0: Easy, 1: Normal, 2: Hard)
    public int cpuDifficulty = 0;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Initialize()
    {
        // 1. 全カードをデータベースに登録
        CardData[] allCards = Resources.LoadAll<CardData>("CardsData");
        cardDatabase.Clear();
        foreach (var card in allCards)
        {
            if (!cardDatabase.ContainsKey(card.id))
            {
                cardDatabase.Add(card.id, card);
            }
        }

        // 2. セーブデータをロード
        Load();

        // 3. ★追加：データのクリーニング（存在しないIDを除外する）
        CleanUpInvalidData();

        // 4. 初回プレイ処理（カード付与）
        if (playerData.ownedCardIds.Count == 0)
        {
             foreach (var card in allCards)
             {
                 playerData.ownedCardIds.Add(card.id);
                 playerData.ownedCardIds.Add(card.id); 
             }
             Save();
        }

        // 5. 初回プレイ処理（デッキ作成）
        if (playerData.decks.Count == 0)
        {
             CreateStarterDeck();
        }
    }

    // ★追加：無効なカードIDをお掃除するメソッド
    void CleanUpInvalidData()
    {
        bool isDirty = false;

        // 所持カードのチェック
        int removedOwned = playerData.ownedCardIds.RemoveAll(id => !cardDatabase.ContainsKey(id));
        if (removedOwned > 0) isDirty = true;

        // 各デッキのチェック
        foreach (var deck in playerData.decks)
        {
            int removedCards = deck.cardIds.RemoveAll(id => !cardDatabase.ContainsKey(id));
            int removedBuilds = deck.buildIds.RemoveAll(id => !cardDatabase.ContainsKey(id));
            
            if (removedCards > 0 || removedBuilds > 0)
            {
                Debug.LogWarning($"デッキ '{deck.deckName}' から無効なカードを削除しました (Card: {removedCards}, Build: {removedBuilds})");
                isDirty = true;
            }
        }

        if (isDirty)
        {
            Debug.Log("データの整合性を修正し、保存しました。");
            Save();
        }
    }

    public void CreateStarterDeck()
    {
        DeckData starterDeck = new DeckData();
        starterDeck.deckName = "Starter Deck";
        starterDeck.deckJob = JobType.KNIGHT; 

        int count = 0;
        foreach (string id in playerData.ownedCardIds)
        {
            if (count >= 30) break;
            // ビルドは除外してユニット/スペルのみ入れる簡易ロジック
            CardData c = GetCardById(id);
            if (c != null && c.type != CardType.BUILD)
            {
                starterDeck.cardIds.Add(id);
                count++;
            }
        }

        playerData.decks.Add(starterDeck);
        playerData.currentDeckIndex = 0;
        
        Save();
        Debug.Log("初期デッキを自動作成しました。");
    }

    public CardData GetCardById(string id)
    {
        if (cardDatabase.ContainsKey(id)) return cardDatabase[id];
        return null;
    }

    public void Save()
    {
        string json = JsonUtility.ToJson(playerData);
        PlayerPrefs.SetString("SaveData", json);
        PlayerPrefs.Save();
        Debug.Log("セーブしました");
    }

    public void Load()
    {
        if (PlayerPrefs.HasKey("SaveData"))
        {
            string json = PlayerPrefs.GetString("SaveData");
            playerData = JsonUtility.FromJson<PlayerData>(json);

            if (playerData == null)
            {
                Debug.LogWarning("セーブデータの読み込みに失敗しました。データを初期化します。");
                playerData = new PlayerData();
            }
            else
            {
                if (playerData.ownedCardIds == null) playerData.ownedCardIds = new List<string>();
                if (playerData.decks == null) playerData.decks = new List<DeckData>();

                foreach (var deck in playerData.decks)
                {
                    if (deck.cardIds == null) deck.cardIds = new List<string>();
                    if (deck.buildIds == null) deck.buildIds = new List<string>();
                }
            }
        }
        else
        {
            playerData = new PlayerData();
        }
    }
}