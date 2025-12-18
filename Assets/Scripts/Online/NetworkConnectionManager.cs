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

    // ■ ルームマッチ (ID指定: 参加)
    public async void StartRoomMatch(string roomID)
    {
        if (string.IsNullOrEmpty(roomID)) 
        {
            Debug.LogError("Room ID is empty");
            return;
        }
        await StartGame(roomID, GameMode.Shared);
    }

    // ■ ルームマッチ (ID生成: 作成)
    public async void CreatePrivateRoom()
    {
        // 6桁のランダムな数字IDを生成
        string roomID = UnityEngine.Random.Range(100000, 999999).ToString();
        Debug.Log($"Creating Private Room with ID: {roomID}");
        await StartGame(roomID, GameMode.Shared);
    }

    private GameObject _runnerInstance;

    // ■ ランダムマッチ
    // ■ ランダムマッチ
    // ■ ランダムマッチ
    public async void StartRandomMatch()
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
    async Task StartGame(string sessionName, GameMode mode = GameMode.Shared)
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
        string scenePath = $"Assets/Scenes/{gameSceneName}.unity";
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);

        if (sceneIndex == -1)
        {
            Debug.LogError($"Scene '{gameSceneName}' not found in Build Settings at path: {scenePath}");
            Debug.LogError("Please add the scene to File > Build Settings.");
            return;
        }
        
        Debug.Log($"Starting Game with Scene: {gameSceneName} (Index: {sceneIndex}) in Session: {sessionName} Mode: {mode}");

        // SceneManagerは明示的に追加し、引数で渡す (Fusion 2の推奨パターン)
        var sceneManager = _runnerInstance.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = _runnerInstance.AddComponent<NetworkSceneManagerDefault>();
        }
        
        // Explicit Object Provider
        var objProvider = _runnerInstance.GetComponent<NetworkObjectProviderDefault>();

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode, 
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(sceneIndex), 
            PlayerCount = 2, 
            SceneManager = sceneManager,
            ObjectProvider = objProvider
        });

        if (result.Ok)
        {
            Debug.Log($"Connected to Session: {sessionName}");

            // Shared ModeのMaster(最初の作成者)だけがGameStateを生成する
            // 注意: SharedModeでは IsServer は false, IsClient は true。
            // 権限確認には IsSharedModeMasterClient などを本来使うが、単純に
            // Runner.Spawn は SharedMode でも可能(権限を持てる)。
            // 重複生成を防ぐため、少し待って確認するか、PlayerJoinedで制御する手もあるが
            // ここでは簡易的に「自分が一人目なら生成」とする。
            // しかしStartGame直後は PlayerCount が自分だけとは限らない(Join時)。
            
            // 少し非同期にチェック
            CheckAndSpawnGameState();
        }
        else
        {
            Debug.LogError($"Failed to Start: {result.ShutdownReason}");
        }
    }

    private async void CheckAndSpawnGameState()
    {
        await Task.Delay(500); // 接続待ち
        if (_runner == null || !_runner.IsRunning) return;

        // すでに存在するかチェック
        // (注: FindObjectOfTypeはローカルのみ。NetworkObjectの同期遅延で被るリスクはあるが、
        //  SharedModeのプロトタイプとしては「見つからなければ作る」で進める)
        var existingState = FindObjectOfType<GameStateController>();
        if (existingState == null)
        {
             // 自分がセッション作成者(または部屋に誰もいない)と仮定してスポーンを試みる
             // 本来は playerlist を見て判断すべきだが、SharedModeでの権限管理は複雑なので
             // ここでは「生成は誰でもできる（早い者勝ち）」というFusionの性質を利用。
             // NetworkObjectPrefab に GameStateController をアタッチしたPrefabが必要だが、
             // 動的生成も可能。ただしPrefab登録が必要。
             
             // 簡易策: 空のGameObjectにAddComponentしてSpawnはできない(Prefab必須)。
             // なので、Resourcesからロードするか、InspectorでPrefab指定が必要。
             // ここでは「InspectorにPrefabを設定」する方式をとる。
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
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { } 
}
