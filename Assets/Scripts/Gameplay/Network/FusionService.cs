using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// Fusion 2 网络服务实现。
    /// 最小闭环：Runner 启动 → 连接 → 分配 PlayerRef → Tick 运行。
    /// 不包含任何游戏逻辑。
    /// </summary>
    public class FusionService : MonoBehaviour, INetworkService, INetworkRunnerCallbacks
    {
        public static FusionService Instance { get; private set; }

        private NetworkRunner _runner;
        private bool _isConnected;
        private bool _isInRoom;
        private bool _isMaster;
        private string _playerName = "Player";
        private string _currentRoomName;

        [Header("Fusion Spawn")]
        [Tooltip("加入房间后自动 Spawn 的 NetworkObject Prefab（如 FusionTestObject）")]
        [SerializeField] private NetworkObject _spawnPrefab;

        /// <summary>Fusion NetworkRunner（外部可访问）</summary>
        public NetworkRunner Runner => _runner;

        /// <summary>本机 PlayerRef（Fusion 身份）</summary>
        public PlayerRef LocalPlayer { get; private set; }

        /// <summary>FusionGameManager 预制体（在 Inspector 中设置）</summary>
        [SerializeField] private NetworkObject _fusionGameManagerPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Build 中 TLS 证书验证修复：Photon 云 HTTPS 连接需要此回调
            ServicePointManager.ServerCertificateValidationCallback = CertCallback;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static bool CertCallback(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // 允许 Photon 云的自签名证书（Build 环境）
            Debug.LogWarning($"[Fusion] TLS 证书警告: {sslPolicyErrors}，已放行");
            return true;
        }

        // ─── INetworkService 实现 ───

        public void Connect()
        {
            if (this == null || gameObject == null) return;

            // 检查 GameObject 上是否已有 NetworkRunner 组件
            var existingRunner = gameObject.GetComponent<NetworkRunner>();
            if (existingRunner != null)
            {
                if (_runner == null)
                {
                    // 复用已存在的 Runner
                    _runner = existingRunner;
                    Debug.Log("[Fusion] 复用已存在的 Runner");
                }
                else
                {
                    Debug.Log("[Fusion] Runner 已存在且运行中，跳过创建");
                    return;
                }
            }
            else
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
                Debug.Log("[Fusion] 创建新 Runner");
            }

            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);
            _isConnected = true;
            Debug.Log($"[Fusion] Runner 就绪: ProvideInput={_runner.ProvideInput}");
            OnServerConnected?.Invoke();
        }

        /// <summary>
        /// 通过 NetworkRunner Spawn FusionGameManager。
        /// 只有 Host/Master 可以 Spawn。
        /// </summary>
        public void SpawnFusionGameManager()
        {
            Debug.Log($"[Fusion] SpawnFusionGameManager 被调用: Runner={_runner != null}, IsRunning={_runner?.IsRunning}");

            if (_runner == null || !_runner.IsRunning)
            {
                Debug.LogError($"[Fusion] NetworkRunner 未运行！Runner={_runner}, IsRunning={_runner?.IsRunning}");
                return;
            }

            if (DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance != null)
            {
                Debug.Log("[Fusion] FusionGameManager 已存在，跳过 Spawn");
                return;
            }

            Debug.Log($"[Fusion] _fusionGameManagerPrefab={_fusionGameManagerPrefab != null}");

            // 方式 1：使用预制体（推荐）
            if (_fusionGameManagerPrefab != null)
            {
                Debug.Log("[Fusion] 尝试通过 Runner.Spawn 预制体...");
                var obj = _runner.Spawn(_fusionGameManagerPrefab);
                Debug.Log($"[Fusion] Runner.Spawn 结果: {obj != null}");
                return;
            }

            // 方式 2：运行时创建（备选）
            Debug.LogWarning("[Fusion] 未配置 FusionGameManager 预制体，尝试运行时创建");
            var go = new GameObject("FusionGameManager_Runtime");
            go.AddComponent<global::Fusion.NetworkObject>();
            go.AddComponent<DoudizhuTower.Gameplay.Fusion.FusionGameManager>();
        }

        public void Disconnect()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
                _runner.Shutdown();
                Destroy(_runner);
                _runner = null;
            }
            _isConnected = false;
            _isInRoom = false;
            _isMaster = false;
            _currentRoomName = null;
            LocalPlayer = default;
        }

        public bool IsConnected => _isConnected;

        public async void CreateRoom(string roomCode, int maxPlayers)
        {
            if (this == null || gameObject == null) return;  // 对象已销毁
            Debug.Log($"[Fusion] CreateRoom 开始: roomCode={roomCode}, runner={_runner != null}");
            if (_runner == null) Connect();
            if (this == null || gameObject == null) return;  // Connect 后再次检查

            try
            {
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = roomCode,
                    PlayerCount = maxPlayers,
                    SceneManager = GetOrCreateSceneManager()
                };
                Debug.Log($"[Fusion] StartGame 调用中... GameMode={args.GameMode}, Session={args.SessionName}");

                var result = await _runner.StartGame(args);

                Debug.Log($"[Fusion] StartGame 结果: Ok={result.Ok}, ErrorMessage={result.ErrorMessage}");

                if (result.Ok)
                {
                    _isInRoom = true;
                    _isMaster = true;
                    _currentRoomName = roomCode;
                    LocalPlayer = _runner.LocalPlayer;
                    Debug.Log($"[Fusion] Host 已创建房间: {roomCode}, PlayerRef={LocalPlayer}, RunnerState={_runner.State}");
                    SpawnTestObject();
                    OnRoomCreateSuccess?.Invoke(roomCode);
                }
                else
                {
                    Debug.LogError($"[Fusion] 创建房间失败: Ok={result.Ok}, Error={result.ErrorMessage}");
                    OnRoomJoinError?.Invoke(result.ErrorMessage);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Fusion] CreateRoom 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async void JoinRoom(string roomCode)
        {
            if (this == null || gameObject == null) return;  // 对象已销毁
            Debug.Log($"[Fusion] JoinRoom 开始: roomCode={roomCode}, runner={_runner != null}");
            if (_runner == null) Connect();
            if (this == null || gameObject == null) return;  // Connect 后再次检查

            try
            {
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SessionName = roomCode,
                    SceneManager = GetOrCreateSceneManager()
                };
                Debug.Log($"[Fusion] StartGame 调用中... GameMode={args.GameMode}, Session={args.SessionName}");

                var result = await _runner.StartGame(args);

                Debug.Log($"[Fusion] StartGame 结果: Ok={result.Ok}, ErrorMessage={result.ErrorMessage}");

                if (result.Ok)
                {
                    _isInRoom = true;
                    _isMaster = false;
                    _currentRoomName = roomCode;
                    LocalPlayer = _runner.LocalPlayer;
                    Debug.Log($"[Fusion] Client 已加入房间: {roomCode}, PlayerRef={LocalPlayer}, RunnerState={_runner.State}");
                    OnRoomJoinSuccess?.Invoke(roomCode);
                }
                else
                {
                    Debug.LogError($"[Fusion] 加入房间失败: Ok={result.Ok}, Error={result.ErrorMessage}");
                    OnRoomJoinError?.Invoke(result.ErrorMessage);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Fusion] JoinRoom 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async void JoinRandomRoom()
        {
            if (this == null || gameObject == null) return;
            if (_runner == null) Connect();
            if (this == null || gameObject == null) return;

            try
            {
                var result = await _runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SceneManager = GetOrCreateSceneManager()
                });

                if (this == null || gameObject == null) return;

                if (result.Ok)
                {
                    _isInRoom = true;
                    _isMaster = false;
                    _currentRoomName = _runner.SessionInfo.Name;
                    LocalPlayer = _runner.LocalPlayer;
                    Debug.Log($"[Fusion] 已随机加入房间: {_currentRoomName}, PlayerRef={LocalPlayer}");
                    OnRoomJoinSuccess?.Invoke(_currentRoomName);
                }
                else
                {
                    Debug.LogError($"[Fusion] 随机加入失败: {result}");
                    OnRoomJoinError?.Invoke(result.ToString());
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Fusion] JoinRandomRoom 异常: {ex.Message}\n{ex.StackTrace}");
                if (_runner != null)
                {
                    _runner.Shutdown();
                    _runner = null;
                }
            }
        }

        /// <summary>获取或创建 NetworkSceneManagerDefault</summary>
        private NetworkSceneManagerDefault GetOrCreateSceneManager()
        {
            var sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
                Debug.Log("[Fusion] 创建 NetworkSceneManagerDefault");
            }
            return sceneManager;
        }

        /// <summary>Spawn 测试对象（仅 Server/Host 执行）</summary>
        private void SpawnTestObject()
        {
            if (_runner == null || !_runner.IsServer) return;
            if (_spawnPrefab == null)
            {
                Debug.LogWarning("[Fusion] _spawnPrefab 未设置，跳过 Spawn");
                return;
            }
            _runner.Spawn(_spawnPrefab, Vector3.zero, Quaternion.identity);
            Debug.Log("[Fusion] 测试对象已 Spawn");
        }

        public void LeaveRoom()
        {
            if (_runner != null)
            {
                _runner.RemoveCallbacks(this);
                _runner.Shutdown();
                Destroy(_runner);
                _runner = null;
            }
            _isInRoom = false;
            _isMaster = false;
            _currentRoomName = null;
            LocalPlayer = default;
        }

        public bool IsInRoom => _isInRoom;
        public bool IsMasterClient => _isMaster;
        public string CurrentRoomName => _currentRoomName ?? "";
        public int CurrentPlayerCount => _runner != null ? System.Linq.Enumerable.Count(_runner.ActivePlayers) : 0;
        public int MaxPlayers => 3;

        public string LocalPlayerName
        {
            get => _playerName;
            set => _playerName = value;
        }

        public string[] GetPlayerNames()
        {
            if (_runner == null) return Array.Empty<string>();
            var list = new System.Collections.Generic.List<string>();
            foreach (var p in _runner.ActivePlayers)
            {
                list.Add($"Player_{p.RawEncoded}");
            }
            return list.ToArray();
        }

        public void SetPlayerReady(bool ready) { }
        public bool AreAllPlayersReady => true;

        [System.Obsolete] public void SendToAll(string key, object value) { }
        [System.Obsolete] public void SendToMaster(string key, object value) { }
        [System.Obsolete] public void SendToPlayer(int actorNumber, string key, object value) { }

        public void SetRoomProperty(string key, object value) { }
        public object GetRoomProperty(string key) => null;

        public void LoadScene(string sceneName)
        {
            if (_runner != null && _runner.IsServer)
                _runner.LoadScene(sceneName);
        }

        public int LocalActorNumber => LocalPlayer.RawEncoded;
        public int[] GetPlayerActorNumbers()
        {
            if (_runner == null) return Array.Empty<int>();
            var list = new System.Collections.Generic.List<int>();
            foreach (var p in _runner.ActivePlayers)
            {
                list.Add(p.RawEncoded);
            }
            return list.ToArray();
        }

        public int GetActorNumberAtPosition(int position)
        {
            var actors = GetPlayerActorNumbers();
            if (position < 0 || position >= actors.Length) return -1;
            return actors[position];
        }

        // ─── 事件 ───

        public event Action OnServerConnected;
        public event Action OnConnectionLost;
        public event Action<string> OnRoomCreateSuccess;
        public event Action<string> OnRoomJoinSuccess;
        public event Action<string> OnRoomJoinError;
        public event Action<string> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action OnAllPlayersReady;
        public event Action OnPlayerReadyChanged;
        public event Action<string, object, int> OnCustomEvent;
        public event Action OnMasterSwitched;

        // ─── INetworkRunnerCallbacks ───

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Fusion] 玩家加入: PlayerRef={player}");
            _isMaster = runner.IsServer;
            OnPlayerJoined?.Invoke($"Player_{player.RawEncoded}");

            // 转发到 FusionGameManager（slot 分配）
            var gm = FindAnyObjectByType<DoudizhuTower.Gameplay.Fusion.FusionGameManager>();
            if (gm != null) gm.OnPlayerJoinedSlot(player);
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Fusion] 玩家离开: PlayerRef={player}");
            OnPlayerLeft?.Invoke($"Player_{player.RawEncoded}");

            // 转发到 FusionGameManager（slot 清理）
            var gm = FindAnyObjectByType<DoudizhuTower.Gameplay.Fusion.FusionGameManager>();
            if (gm != null) gm.OnPlayerLeftSlot(player);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("[Fusion] 已连接到服务器");
            _isConnected = true;
            OnServerConnected?.Invoke();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[Fusion] 断开连接: {reason}");
            _isInRoom = false;
            OnConnectionLost?.Invoke();
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[Fusion] Runner 关闭: {shutdownReason}");
            _isInRoom = false;
            _isMaster = false;
            LocalPlayer = default;
        }

        // ─── 未实现的回调（占位）───
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason)
        {
            Debug.LogError($"[Fusion] 连接失败: Address={address}, Reason={reason}");
            _isConnected = false;
            OnConnectionLost?.Invoke();
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var gm = DoudizhuTower.Gameplay.Fusion.FusionGameManager.Instance;
            if (gm != null && gm.TryGetLocalInput(out var localInput))
            {
                input.Set(localInput);
                Debug.Log($"[FusionService] OnInput forwarded: Action={localInput.Action}");
            }
        }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnReadyToStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj) { }
        public void OnObjectDespawned(NetworkRunner runner, NetworkObject obj) { }
        public void OnFailedToConnectPeerToServer(NetworkRunner runner, PlayerRef player, NetDisconnectReason reason) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
