using System.Collections.Generic;

// 保存するデータ全体のクラス
[System.Serializable]
public class PlayerData
{
    public List<string> ownedCardIds = new List<string>();
    public List<DeckData> decks = new List<DeckData>();
    public int currentDeckIndex = 0;
}

// デッキ単体のクラス
[System.Serializable]
public class DeckData
{
    public string deckName = "New Deck";
    
    // ★追加：このデッキがどのジョブ用か
    public JobType deckJob = JobType.NEUTRAL; 
    
    public List<string> cardIds = new List<string>();
}