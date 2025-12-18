using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;

public class DeckCodeDatabaseManager : MonoBehaviour
{
    public static DeckCodeDatabaseManager instance;

    // Google Services JSONから取得したURL
    // 末尾に .json を付けるのがREST APIのルール
    private const string DATABASE_URL = "https://hakoniwacardbattle-default-rtdb.asia-southeast1.firebasedatabase.app/decks";

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // デッキを登録して、7桁のコードを返す
    public void RegisterDeck(string longDeckCode, Action<string, string> onComplete)
    {
        StartCoroutine(RegisterDeckCoroutine(longDeckCode, onComplete));
    }

    private IEnumerator RegisterDeckCoroutine(string longDeckCode, Action<string, string> onComplete)
    {
        // 7桁のランダムな数字コードを生成
        string shortCode = GenerateRandomCode(7);
        
        // REST APIのエンドポイント: URL/shortCode.json
        string url = $"{DATABASE_URL}/{shortCode}.json";

        // PUTリクエストでデータを保存（引用符で囲んでJSON文字列にする）
        // そのままだと文字列として認識されない場合があるので、"値" の形式にする
        string jsonBody = "\"" + longDeckCode + "\"";
        
        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Firebase Error: {request.error}\nResponse: {request.downloadHandler.text}");
                onComplete?.Invoke(null, request.error);
            }
            else
            {
                // 成功
                Debug.Log($"Deck Registered via REST API: {shortCode}");
                onComplete?.Invoke(shortCode, null);
            }
        }
    }

    // 7桁コードから元のデッキコードを取得する
    public void GetDeck(string shortCode, Action<string, string> onComplete)
    {
        StartCoroutine(GetDeckCoroutine(shortCode, onComplete));
    }

    private IEnumerator GetDeckCoroutine(string shortCode, Action<string, string> onComplete)
    {
        string url = $"{DATABASE_URL}/{shortCode}.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Firebase Error: {request.error}");
                onComplete?.Invoke(null, request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"Firebase Response: {responseText}");

                // 該当なしの場合は "null" という文字列が返ってくる
                if (string.IsNullOrEmpty(responseText) || responseText == "null")
                {
                    onComplete?.Invoke(null, "Code not found");
                }
                else
                {
                    // 返ってくる値はダブルクォートで囲まれているため除去する
                    string cleanCode = responseText.Trim('"');
                    // エスケープ文字が含まれている可能性があるので解除（単純なBase64なら不要な場合も多いが念のため）
                    // cleanCode = System.Text.RegularExpressions.Regex.Unescape(cleanCode); 
                    
                    onComplete?.Invoke(cleanCode, null);
                }
            }
        }
    }

    private string GenerateRandomCode(int length)
    {
        const string chars = "0123456789"; 
        char[] stringChars = new char[length];
        var random = new System.Random();

        for (int i = 0; i < length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }

        return new string(stringChars);
    }
}
