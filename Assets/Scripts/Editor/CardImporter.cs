using UnityEngine;
using UnityEditor; // エディタ拡張機能を使う
using System.IO;   // ファイル読み書き用

// このスクリプトはゲーム中には動かず、エディタ上でのみ動きます
public class CardImporter : MonoBehaviour
{
    // メニューバーにボタンを追加する魔法の呪文
    [MenuItem("Hakoniwa/Import Cards from JSON")]
    public static void ImportJsonData()
    {
        // 1. JSONファイルを読み込む
        // Resourcesフォルダ内の cards.json を探す
        string path = Application.dataPath + "/Resources/cards.json";
        if (!File.Exists(path))
        {
            Debug.LogError("ファイルが見つかりません: " + path);
            return;
        }
        string jsonContent = File.ReadAllText(path);

        // 2. JSONを解析する
        CardList dataList = JsonUtility.FromJson<CardList>(jsonContent);

        // 3. データを1つずつScriptableObjectに変換して保存
        foreach (var raw in dataList.cards)
        {
            // 新しいカードデータを作成
            CardData card = ScriptableObject.CreateInstance<CardData>();

            // 中身をコピー
            card.id = raw.id;
            card.cardName = raw.name; // JSONの"name"をC#の"cardName"へ

            // 文字列(String)をEnumに変換
            System.Enum.TryParse(raw.type, out card.type);
            System.Enum.TryParse(raw.job, out card.job);
            System.Enum.TryParse(raw.rarity, out card.rarity);

            card.cost = raw.cost;
            card.attack = raw.attack;
            card.health = raw.health;
            card.description = raw.description;
            card.scriptKey = raw.scriptKey;
            card.maxInDeck = raw.maxInDeck;

            // ファイルとして保存 (CardsDataフォルダへ)
            string savePath = "Assets/CardsData/" + card.id + "_" + card.cardName + ".asset";
            AssetDatabase.CreateAsset(card, savePath);
        }

        AssetDatabase.SaveAssets(); // 保存を確定
        AssetDatabase.Refresh();    // エディタを更新
        Debug.Log("完了！カードデータの作成に成功しました！");
    }

    // JSONを受け取るための入れ物クラス
    [System.Serializable]
    private class CardList
    {
        public RawData[] cards;
    }

    [System.Serializable]
    private class RawData
    {
        public string id;
        public string name;
        public string type;
        public string job;
        public string rarity;
        public int cost;
        public int attack;
        public int health;
        public string description;
        public string scriptKey;
        public int maxInDeck;
    }
}