using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// エディタ専用のウィンドウ作成
public class CardCreatorWindow : EditorWindow
{
    // --- 入力用の一時変数 ---
    string cardId = "";
    string cardName = "New Card";
    CardType cardType = CardType.UNIT;
    JobType cardJob = JobType.NEUTRAL;
    Rarity rarity = Rarity.COMMON;

    int cost = 1;
    int attack = 1;
    int health = 1;
    int maxInDeck = 3;
    int duration = 3; // ビルド用

    string description = "";
    Sprite cardIcon;

    // 能力リスト
    List<CardAbility> abilities = new List<CardAbility>();
    Vector2 scrollPos;

    [MenuItem("Hakoniwa/Open Card Creator")]
    public static void ShowWindow()
    {
        GetWindow<CardCreatorWindow>("Card Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("新規カード作成ツール", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // --- 基本情報 ---
        EditorGUILayout.Space();
        GUILayout.Label("基本ステータス", EditorStyles.label);
        
        cardName = EditorGUILayout.TextField("Card Name", cardName);
        
        // ID自動生成エリア
        EditorGUILayout.BeginHorizontal();
        cardId = EditorGUILayout.TextField("ID (File Name)", cardId);
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("Auto ID", GUILayout.Width(80)))
        {
            GenerateAutoId();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        cardType = (CardType)EditorGUILayout.EnumPopup("Type", cardType);
        cardJob = (JobType)EditorGUILayout.EnumPopup("Job", cardJob);
        rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", rarity);

        // --- パラメータ ---
        EditorGUILayout.Space();
        GUILayout.Label("数値パラメータ", EditorStyles.label);
        
        EditorGUILayout.BeginHorizontal();
        cost = EditorGUILayout.IntField("Cost", cost);
        maxInDeck = EditorGUILayout.IntField("Max In Deck", maxInDeck);
        EditorGUILayout.EndHorizontal();

        if (cardType == CardType.UNIT)
        {
            EditorGUILayout.BeginHorizontal();
            attack = EditorGUILayout.IntField("Attack", attack);
            health = EditorGUILayout.IntField("Health", health);
            EditorGUILayout.EndHorizontal();
        }
        else if (cardType == CardType.BUILD)
        {
            duration = EditorGUILayout.IntField("Duration (Turn)", duration);
        }

        // --- 詳細・画像 ---
        EditorGUILayout.Space();
        GUILayout.Label("詳細・画像", EditorStyles.label);
        description = EditorGUILayout.TextArea(description, GUILayout.Height(50));
        cardIcon = (Sprite)EditorGUILayout.ObjectField("Icon", cardIcon, typeof(Sprite), false);

        // --- アビリティリスト ---
        EditorGUILayout.Space();
        GUILayout.Label($"能力リスト ({abilities.Count})", EditorStyles.boldLabel);
        
        for (int i = 0; i < abilities.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Ability {i + 1}");
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                abilities.RemoveAt(i);
                break; 
            }
            EditorGUILayout.EndHorizontal();

            abilities[i].trigger = (EffectTrigger)EditorGUILayout.EnumPopup("Trigger", abilities[i].trigger);
            abilities[i].target = (EffectTarget)EditorGUILayout.EnumPopup("Target", abilities[i].target);
            abilities[i].effect = (EffectType)EditorGUILayout.EnumPopup("Effect", abilities[i].effect);
            abilities[i].value = EditorGUILayout.IntField("Value", abilities[i].value);
            
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ 能力を追加"))
        {
            abilities.Add(new CardAbility());
        }

        EditorGUILayout.Space(20);

        // --- 作成ボタン ---
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("カードを作成・保存", GUILayout.Height(40)))
        {
            CreateCardAsset();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    // ★追加：ID自動生成ロジック
    void GenerateAutoId()
    {
        if (string.IsNullOrEmpty(cardName))
        {
            Debug.LogWarning("カード名を入力してください（IDの末尾に使われます）");
            return;
        }

        // 1. ジョブの頭文字を取得 (例: KNIGHT -> "K")
        string prefix = cardJob.ToString().Substring(0, 1).ToUpper();

        // 2. 既存のファイルを検索して最大番号を探す
        string folderPath = "Assets/Resources/CardsData";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string[] files = Directory.GetFiles(folderPath, "*.asset");
        int maxNum = 0;

        foreach (string file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            
            // ファイル名が指定のPrefixで始まっているか確認
            if (fileName.StartsWith(prefix))
            {
                // アンダースコアを探す (例: K001_Name)
                int underscoreIndex = fileName.IndexOf('_');
                if (underscoreIndex > 1) // K_... ではなく K1_... 以上を想定
                {
                    // Prefix(1文字)とアンダースコアの間の文字列を取得
                    string numPart = fileName.Substring(1, underscoreIndex - 1);
                    
                    // 数字に変換できるか試す
                    if (int.TryParse(numPart, out int num))
                    {
                        if (num > maxNum) maxNum = num;
                    }
                }
            }
        }

        // 3. 次の番号を決定 (最大値 + 1)
        int nextNum = maxNum + 1;
        
        // 4. ID生成 (例: K002_CardName) ※D3で3桁埋め
        cardId = $"{prefix}{nextNum:D3}_{cardName}";
        
        // キーボードフォーカスを外してGUIを更新
        GUI.FocusControl(null); 
        Debug.Log($"自動生成ID: {cardId}");
    }

    void CreateCardAsset()
    {
        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogError("IDを入力してください！");
            return;
        }

        string folderPath = "Assets/Resources/CardsData";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        CardData newCard = ScriptableObject.CreateInstance<CardData>();
        
        newCard.id = cardId;
        newCard.cardName = cardName;
        newCard.type = cardType;
        newCard.job = cardJob;
        newCard.rarity = rarity;
        newCard.cost = cost;
        newCard.attack = attack;
        newCard.health = health;
        newCard.maxInDeck = maxInDeck;
        newCard.duration = duration;
        newCard.description = description;
        newCard.cardIcon = cardIcon;
        newCard.abilities = new List<CardAbility>(abilities);

        string path = $"{folderPath}/{cardId}.asset";
        
        if (AssetDatabase.LoadAssetAtPath<CardData>(path) != null)
        {
            if (!EditorUtility.DisplayDialog("上書き確認", $"'{cardId}' は既に存在します。上書きしますか？", "上書き", "キャンセル"))
            {
                return;
            }
        }

        AssetDatabase.CreateAsset(newCard, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"カードを作成しました: {path}");
        Selection.activeObject = newCard;
    }
}