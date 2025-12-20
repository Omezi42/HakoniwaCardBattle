using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using System.Collections.Generic;

public class PlayFabManager : MonoBehaviour
{
    public static PlayFabManager instance;
    
    public string MyPlayFabId { get; private set; }
    public bool IsLoggedIn { get; private set; } = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            PlayFab.PlayFabSettings.TitleId = "184DCC";
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Login();
    }

    public void Login()
    {
        if (IsLoggedIn) return;

        string customId = SystemInfo.deviceUniqueIdentifier;
#if UNITY_EDITOR
        // Ensure unique ID for ParrelSync / Editor testing
        // ParrelSync uses "clone" in path or arguments, but simple random suffix is safest for uniqueness
        customId += "_" + Random.Range(1000, 9999).ToString();
#endif

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        Debug.Log($"Logging in to PlayFab with CustomID: {customId}");
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("PlayFab Login Successful. ID: " + result.PlayFabId);
        MyPlayFabId = result.PlayFabId;
        IsLoggedIn = true;
    }

    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogError("PlayFab Login Failed: " + error.GenerateErrorReport());
    }

    // デッキをShared Groupとして登録
    public void RegisterDeck(string deckJson, System.Action<string, string> callback)
    {
        if (!IsLoggedIn)
        {
            var request = new LoginWithCustomIDRequest 
            { 
                CustomId = SystemInfo.deviceUniqueIdentifier, 
                CreateAccount = true 
            };
            PlayFabClientAPI.LoginWithCustomID(request, (res) => {
                OnLoginSuccess(res);
                RegisterDeck(deckJson, callback);
            }, (err) => {
                callback?.Invoke(null, "Login Failed: " + err.ErrorMessage);
            });
            return;
        }

        CreateSharedGroupWithRetry(deckJson, callback);
    }

    private void CreateSharedGroupWithRetry(string deckJson, System.Action<string, string> callback, int retryCount = 0)
    {
        if (retryCount > 5)
        {
            callback?.Invoke(null, "Failed to generate unique code after retries.");
            return;
        }

        string randomCode = Random.Range(1000000, 10000000).ToString();
        string sharedGroupId = "Deck_" + randomCode;

        PlayFabClientAPI.CreateSharedGroup(new CreateSharedGroupRequest
        {
            SharedGroupId = sharedGroupId
        }, (result) =>
        {
            // 作成成功 -> データを保存
            SaveDeckToSharedGroup(sharedGroupId, randomCode, deckJson, callback);
        }, (error) =>
        {
            // ID重複エラーならリトライ
            // (PlayFabのエラーコードを確認するのがベストだが、作成失敗時は基本的に重複か権限か)
            Debug.Log($"Create Shared Group Failed ({error.Error}), retrying...");
            CreateSharedGroupWithRetry(deckJson, callback, retryCount + 1);
        });
    }

    private void SaveDeckToSharedGroup(string groupId, string shortCode, string json, System.Action<string, string> callback)
    {
        var request = new UpdateSharedGroupDataRequest
        {
            SharedGroupId = groupId,
            Data = new Dictionary<string, string>
            {
                { "DeckData", json }
            },
            Permission = UserDataPermission.Public
        };

        PlayFabClientAPI.UpdateSharedGroupData(request, (result) =>
        {
            Debug.Log($"Deck saved to Shared Group: {groupId}");
            callback?.Invoke(shortCode, null);
        }, (error) =>
        {
            Debug.LogError("UpdateSharedGroupData Failed: " + error.GenerateErrorReport());
            callback?.Invoke(null, error.ErrorMessage);
        });
    }

    // デッキを取得 (Shared Groupから)
    public void GetDeck(string code, System.Action<string, string> callback)
    {
        if (string.IsNullOrEmpty(code))
        {
            callback?.Invoke(null, "Invalid Code");
            return;
        }

        if (!IsLoggedIn)
        {
            var req = new LoginWithCustomIDRequest { CustomId = SystemInfo.deviceUniqueIdentifier, CreateAccount = true };
            PlayFabClientAPI.LoginWithCustomID(req, (res) => {
                OnLoginSuccess(res);
                GetDeck(code, callback);
            }, (err) => callback?.Invoke(null, "Login Required"));
            return;
        }

        string sharedGroupId = "Deck_" + code;

        PlayFabClientAPI.GetSharedGroupData(new GetSharedGroupDataRequest
        {
            SharedGroupId = sharedGroupId,
            Keys = new List<string> { "DeckData" },
            GetMembers = false
        }, (result) =>
        {
            if (result.Data != null && result.Data.ContainsKey("DeckData"))
            {
                string json = result.Data["DeckData"].Value;
                callback?.Invoke(json, null);
            }
            else
            {
                callback?.Invoke(null, "Deck Not Found");
            }
        }, (error) =>
        {
            Debug.LogError("GetDeck Failed: " + error.GenerateErrorReport());
            callback?.Invoke(null, "Error: " + error.ErrorMessage);
        });
    }
}
