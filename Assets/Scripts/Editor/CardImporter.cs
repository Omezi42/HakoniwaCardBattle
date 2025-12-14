using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class CardImporter : MonoBehaviour
{
    [MenuItem("Hakoniwa/Import Cards from JSON (v2)")]
    public static void ImportJsonData()
    {
        string path = Application.dataPath + "/Resources/cards.json";
        if (!File.Exists(path))
        {
            Debug.LogError("JSONファイルが見つかりません: " + path);
            return;
        }
        string jsonContent = File.ReadAllText(path);

        // 配列をラップして読み込む
        CardList dataList = JsonUtility.FromJson<CardList>(jsonContent);

        if (dataList == null || dataList.cards == null)
        {
            Debug.LogError("JSONの解析に失敗しました。フォーマットを確認してください。");
            return;
        }

        // 保存先フォルダ確認
        string folderPath = "Assets/Resources/CardsData";
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        foreach (var raw in dataList.cards)
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();

            // 基本データ
            card.id = raw.id;
            card.cardName = raw.name;
            
            // Enum変換（失敗時はデフォルト値）
            System.Enum.TryParse(raw.type, out card.type);
            System.Enum.TryParse(raw.job, out card.job);
            System.Enum.TryParse(raw.rarity, out card.rarity);

            card.cost = raw.cost;
            card.attack = raw.attack;
            card.health = raw.health;
            card.maxInDeck = raw.maxInDeck;
            card.duration = raw.duration; // ★追加
            card.description = raw.description;
            card.scriptKey = raw.scriptKey;

            // ★追加：アビリティリストの変換
            card.abilities = new List<CardAbility>();
            if (raw.abilities != null)
            {
                foreach (var rawAbi in raw.abilities)
                {
                    CardAbility abi = new CardAbility();
                    System.Enum.TryParse(rawAbi.trigger, out abi.trigger);
                    System.Enum.TryParse(rawAbi.target, out abi.target);
                    System.Enum.TryParse(rawAbi.effect, out abi.effect);
                    abi.value = rawAbi.value;
                    card.abilities.Add(abi);
                }
            }

            // 保存
            string savePath = $"{folderPath}/{card.id}.asset";
            AssetDatabase.CreateAsset(card, savePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"完了！ {dataList.cards.Length} 枚のカードデータをインポートしました。");
    }

    // --- JSON受け皿用クラス ---
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
        public string type;   // "UNIT", "SPELL", "BUILD"
        public string job;    // "NEUTRAL", "KNIGHT"...
        public string rarity; // "COMMON"...
        public int cost;
        public int attack;
        public int health;
        public int duration;  // ★追加
        public int maxInDeck;
        public string description;
        public string scriptKey;
        public RawAbility[] abilities; // ★追加
    }

    [System.Serializable]
    private class RawAbility
    {
        public string trigger; // "ON_SUMMON"...
        public string target;  // "FRONT_ENEMY"...
        public string effect;  // "DAMAGE"...
        public int value;
    }
}