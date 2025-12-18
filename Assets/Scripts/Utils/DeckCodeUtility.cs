using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;

public static class DeckCodeUtility
{
    // フォーマット: [JobType(int)]|[CardID,CardID,...]|[BuildID,BuildID,...]
    // これをBase64化する簡易実装。
    // 本格的に圧縮するならBit操作が必要だが、今回は可読性と実装速度優先。

    public static string Encode(DeckData deck)
    {
        if (deck == null) return "";

        StringBuilder sb = new StringBuilder();
        sb.Append((int)deck.deckJob);
        sb.Append("|");
        
        // カードIDリスト
        sb.Append(string.Join(",", deck.cardIds));
        sb.Append("|");

        // ビルドIDリスト
        sb.Append(string.Join(",", deck.buildIds));

        // UTF8バイト列に変換してからBase64化
        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToBase64String(bytes);
    }

    public static DeckData Decode(string code, string newDeckName = "Imported Deck")
    {
        if (string.IsNullOrEmpty(code)) return null;

        try
        {
            byte[] bytes = Convert.FromBase64String(code);
            string decodedString = Encoding.UTF8.GetString(bytes);
            
            string[] parts = decodedString.Split('|');
            if (parts.Length < 3) return null;

            DeckData deck = new DeckData();
            deck.deckName = newDeckName;
            
            // Job
            if (int.TryParse(parts[0], out int jobIndex))
            {
                deck.deckJob = (JobType)jobIndex;
            }

            // Cards
            string[] cardIds = parts[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            deck.cardIds = new List<string>(cardIds);

            // Builds
            string[] buildIds = parts[2].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            deck.buildIds = new List<string>(buildIds);

            // 有効性チェック（IDが存在するか等）はPlayerDataManagerで読み込み時に弾かれるか、
            // もしくはGetCardByIdでnullが返るかで対応される想定。
            
            return deck;
        }
        catch (Exception e)
        {
            Debug.LogError($"Deck Code Decode Error: {e.Message}");
            return null;
        }
    }
}
