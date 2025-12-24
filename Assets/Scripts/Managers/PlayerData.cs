using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    public List<string> ownedCardIds = new List<string>();
    public List<DeckData> decks = new List<DeckData>();
    public int currentDeckIndex = 0;
}

[System.Serializable]
public class DeckData
{
    public string deckName = "New Deck";
    public JobType deckJob = JobType.NEUTRAL; 
    
    public List<string> cardIds = new List<string>();
    public List<string> buildIds = new List<string>(); // ★追加：ビルド用
    
    // 発行済みコード（PlayFab Shared Group IDの一部）
    public string publishedCode = "";
}