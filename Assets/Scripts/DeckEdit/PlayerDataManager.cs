using UnityEngine;
using System.Collections.Generic;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager instance;

    public PlayerData playerData = new PlayerData();
    private Dictionary<string, CardData> cardDatabase = new Dictionary<string, CardData>();

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
        CardData[] allCards = Resources.LoadAll<CardData>("CardsData");
        foreach (var card in allCards)
        {
            if (!cardDatabase.ContainsKey(card.id))
            {
                cardDatabase.Add(card.id, card);
            }
        }

        Load();

        // 1. カードが1枚もなければ、全カードを付与（初回プレイ用）
        if (playerData.ownedCardIds.Count == 0)
        {
             foreach (var card in allCards)
             {
                 playerData.ownedCardIds.Add(card.id);
                 playerData.ownedCardIds.Add(card.id); 
             }
             // カードが増えたのでセーブ
             Save();
        }

        // 2. ★追加：デッキが1つもなければ、初期デッキを作成
        if (playerData.decks.Count == 0)
        {
             CreateStarterDeck();
        }
    }

    // ★追加：初期デッキ作成メソッド（外部からも呼べるようにpublicにしておく）
    public void CreateStarterDeck()
    {
        DeckData starterDeck = new DeckData();
        starterDeck.deckName = "Starter Deck";
        
        // ※とりあえず最初のジョブ（KNIGHT等）にする
        starterDeck.deckJob = JobType.KNIGHT; 

        // 所持カードから適当に30枚詰め込む
        int count = 0;
        foreach (string id in playerData.ownedCardIds)
        {
            if (count >= 30) break;
            // 簡易的にジョブチェックせず入れる（本来はチェック推奨）
            starterDeck.cardIds.Add(id);
            count++;
        }

        playerData.decks.Add(starterDeck);
        playerData.currentDeckIndex = 0;
        
        Save();
        Debug.Log("初期デッキを自動作成しました。");
    }

    // ... (GetCardById, Save, Load はそのまま) ...
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
            // JSON読み込み
            playerData = JsonUtility.FromJson<PlayerData>(json);

            // ★追加：もし読み込みに失敗して null になっていたら、新しく作り直す
            if (playerData == null)
            {
                Debug.LogWarning("セーブデータの読み込みに失敗しました。データを初期化します。");
                playerData = new PlayerData();
            }
        }
        else
        {
            playerData = new PlayerData();
        }
    }
}