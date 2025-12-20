using UnityEngine;
using System.Collections;

// PlayFabManagerへのブリッジとして機能させる
public class DeckCodeDatabaseManager : MonoBehaviour
{
    public static DeckCodeDatabaseManager instance;

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
        }
    }

    public void RegisterDeck(string longCode, System.Action<string, string> callback)
    {
        if (PlayFabManager.instance != null)
        {
            PlayFabManager.instance.RegisterDeck(longCode, callback);
        }
        else
        {
            callback?.Invoke(null, "PlayFabManager instance not found");
        }
    }

    public void GetDeck(string shortCode, System.Action<string, string> callback)
    {
        if (PlayFabManager.instance != null)
        {
            PlayFabManager.instance.GetDeck(shortCode, callback);
        }
        else
        {
            callback?.Invoke(null, "PlayFabManager instance not found");
        }
    }
}
