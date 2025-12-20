using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

public class NetworkConnectionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkConnectionManager instance;
    private NetworkRunner _runner;
    public NetworkRunner Runner => _runner; // [NEW] 外部公開プロパティ

    // 戦闘シーン名（実際の名前に変更）
    [SerializeField] private string gameSceneName = "SampleScene"; 

    private Action<List<SessionInfo>> onSessionListUpdated;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 重要: エディタ上で手動アタッチされたNetworkRunnerがあれば破壊する
            // (RunnerHolder方式と競合してSingle-Peer違反になるのを防ぐ)
            var attachedRunner = GetComponent<NetworkRunner>();
            if (attachedRunner != null)
            {
                Debug.LogWarning("Destined NetworkRunner attached to Manager. Destroying it to use RunnerHolder pattern.");
                Destroy(attachedRunner);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ■ ルームマッチ (ID指定: 参加/作成 共通)
    // OnlineMenuManagerから呼ばれる
    public async Task StartSharedSession(string sessionName, string sceneName)
    {
        await StartGame(sessionName, GameMode.Shared, sceneName);
    }
    
    // ■ ルームマッチ (旧: ID指定参加)
    public async void StartRoomMatch(string roomID)
    {
        if (string.IsNullOrEmpty(roomID)) 
        {
            Debug.LogError("Room ID is empty");
            return;
        }
        await StartGame(roomID, GameMode.Shared, "SampleScene"); // Default to SampleScene if legacy call
    }

    // ■ ルームマッチ (旧: ID生成作成)
    public async void CreatePrivateRoom()
    {
        string roomID = UnityEngine.Random.Range(1000, 9999).ToString();
        Debug.Log($"Creating Private Room with ID: {roomID}");
        await StartGame(roomID, GameMode.Shared, "RoomScene");
    }

    private GameObject _runnerInstance;

    // ■ ランダムマッチ
    // ■ ランダムマッチ
    // ■ ランダムマッチ
    public async Task StartRandomMatch()
    {
        await CreateRunner(); // ロビー用のRunner作成

        Debug.Log("Joining Lobby for Random Match...");
        var result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);

        if (!result.Ok)
        {
            Debug.LogError($"Failed to join lobby: {result.ShutdownReason}");
            return;
        }
   
        onSessionListUpdated = (sessions) => {
            onSessionListUpdated = null; 
            JoinFirstAvailableOrHost(sessions);
        };
    }

    private async void JoinFirstAvailableOrHost(List<SessionInfo> sessions)
    {
        foreach (var session in sessions)
        {
            if (session.IsOpen && session.PlayerCount < session.MaxPlayers)
            {
                Debug.Log($"Found open session: {session.Name}, Joining...");
                await StartGame(session.Name, GameMode.Shared);
                return;
            }
        }

        string randomRoomName = "Random_" + Guid.NewGuid().ToString().Substring(0, 8);
        Debug.Log($"No session found. Creating: {randomRoomName}");
        await StartGame(randomRoomName, GameMode.Shared);
    }

    // Runnerを作成（作り直し）するヘルパー
    private async Task CreateRunner()
    {
        if (_runnerInstance != null)
        {
            if (_runner != null && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }
            Destroy(_runnerInstance);
            _runnerInstance = null;
            _runner = null;

            // 念のため長めに待つ (内部スレッドの停止待ち)
            await Task.Delay(500);
        }

        // 新しいオブジェクトをRootに作る (親を持たせない)
        _runnerInstance = new GameObject("RunnerHolder");
        DontDestroyOnLoad(_runnerInstance); // シーン遷移で消えないように
        
        _runner = _runnerInstance.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        
        // 明示的にObjectProviderを追加（Fusion2で推奨される場合がある）
        _runnerInstance.AddComponent<NetworkObjectProviderDefault>();
        
        _runner.AddCallbacks(this);
    }

    // 共通接続処理
    async Task StartGame(string sessionName, GameMode mode = GameMode.Shared, string specificSceneName = null)
    {
        // 念のためRunnerの状態をチェック
        if (_runner != null && _runner.IsRunning)
        {
             Debug.LogWarning($"Runner is still active. Shutting down before StartGame.");
             await _runner.Shutdown();
             // Shutdown待機はCreateRunnerに任せるため、ここでは一旦nullにして再作成フローへ
             _runner = null;
        }

        // 毎回Runnerを作り直す
        await CreateRunner();

        // シーンパスの確認
        string targetScene = !string.IsNullOrEmpty(specificSceneName) ? specificSceneName : gameSceneName;
        string scenePath = $"Assets/Scenes/{targetScene}.unity";
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);

        if (sceneIndex == -1)
        {
            Debug.LogError($"Scene '{gameSceneName}' not found in Build Settings at path: {scenePath}");
            Debug.LogError("Please add the scene to File > Build Settings.");
            return;
        }
        
        Debug.Log($"Starting Game with Scene: {targetScene} (Index: {sceneIndex}) in Session: {sessionName} Mode: {mode}");

        // SceneManagerは明示的に追加し、引数で渡す (Fusion 2の推奨パターン)
        var sceneManager = _runnerInstance.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = _runnerInstance.AddComponent<NetworkSceneManagerDefault>();
        }
        
        // Explicit Object Provider
        var objProvider = _runnerInstance.GetComponent<NetworkObjectProviderDefault>();

        // AppSettingsでRegionを固定 (ParrelSyncなどでのリージョン不一致を防ぐ)
        // 既存の設定をベースにするため、PhotonAppSettings.Global.AppSettingsからIDを取得
        var globalSettings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings;
        
        var customAppSettings = new Fusion.Photon.Realtime.FusionAppSettings
        {
            AppIdFusion = globalSettings.AppIdFusion,
            FixedRegion = "jp",
            UseNameServer = true
        };

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode, 
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(sceneIndex), 
            PlayerCount = 2, 
            SceneManager = sceneManager,
            ObjectProvider = objProvider,
            CustomPhotonAppSettings = customAppSettings
        });

        if (result.Ok)
        {
            Debug.Log($"Connected to Session: {sessionName}");
            // Spawn logic moved to OnSceneLoadDone to prevent premature spawn and destruction in RoomScene
        }
        else
        {
            Debug.LogError($"Failed to Start: {result.ShutdownReason}");
        }
    }

    private async void CheckAndSpawnGameState()
    {
        await Task.Delay(1000); // 接続・同期待ち時間を少し延長
        if (_runner == null || !_runner.IsRunning) return;

        // Ensure we are in the Game Scene before spawning GameState
        if (SceneManager.GetActiveScene().name != "SampleScene") return;

        // すでに存在するかチェック
        var existingState = FindObjectOfType<GameStateController>();
        if (existingState != null) return;
        
        // SharedModeでの重複生成防止: Shared Mode Master Clientのみが生成
        if (_runner.IsSharedModeMasterClient)
        {
             Debug.Log($"I am the Shared Mode Master ({_runner.LocalPlayer}). Spawning GameStateController.");
             if (gameStatePrefab != null)
             {
                 _runner.Spawn(gameStatePrefab, Vector3.zero, Quaternion.identity);
             }
             else
             {
                 Debug.LogError("GameStatePrefab is not assigned in NetworkConnectionManager!");
             }
        }
    }

    [SerializeField] private NetworkObject gameStatePrefab; // [NEW]

    // --- INetworkRunnerCallbacks ---

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        // 頻繁にログが出るとうるさいのでコメントアウト推奨等だが、デバッグ中は残す
        Debug.Log($"Session List Updated: {sessionList.Count} sessions");
        onSessionListUpdated?.Invoke(sessionList);
    }
    
    public void OnSceneLoadDone(NetworkRunner runner) 
    { 
        // When scene finishes loading, check if we need to spawn GameState
        Debug.Log($"[NetworkConnectionManager] Scene Loaded: {SceneManager.GetActiveScene().name}");
        CheckAndSpawnGameState();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { Debug.Log($"Player Joined: {player}"); }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { Debug.Log($"Player Left: {player}"); }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { Debug.Log($"Shutdown: {shutdownReason}"); }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { } 
}
