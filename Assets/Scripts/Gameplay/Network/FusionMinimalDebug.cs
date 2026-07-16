using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// Fusion 最小连接调试工具。
    /// 只保留：Runner → AddCallbacks → StartGame → Debug.Log。
    /// 用于隔离测试基础连接功能。
    /// </summary>
    public class FusionMinimalDebug : MonoBehaviour, INetworkRunnerCallbacks
    {
        private NetworkRunner _runner;

        [Header("调试配置")]
        [SerializeField] private string sessionName = "DebugTest";
        [SerializeField] private int maxPlayers = 2;
        [SerializeField] private bool autoStart = true;

        private async void Start()
        {
            if (!autoStart) return;
            await ConnectAndStart();
        }

        public async Awaitable ConnectAndStart()
        {
            Debug.Log("[FusionDebug] === 开始最小连接测试 ===");

            // 1. 创建 Runner
            Debug.Log("[FusionDebug] 1. 创建 NetworkRunner...");
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            Debug.Log($"[FusionDebug] Runner 创建完成: {_runner != null}");

            // 2. 注册回调
            Debug.Log("[FusionDebug] 2. 注册 INetworkRunnerCallbacks...");
            _runner.AddCallbacks(this);
            Debug.Log("[FusionDebug] 回调注册完成");

            // 3. 启动游戏（连接）
            Debug.Log($"[FusionDebug] 3. 调用 StartGame: Session={sessionName}, MaxPlayers={maxPlayers}");
            try
            {
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = sessionName,
                    PlayerCount = maxPlayers,
                    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
                };

                Debug.Log("[FusionDebug] StartGame 调用中...");
                var result = await _runner.StartGame(args);

                Debug.Log($"[FusionDebug] StartGame 完成: Ok={result.Ok}");

                if (result.Ok)
                {
                    Debug.Log($"[FusionDebug] 连接成功! RunnerState={_runner.State}");
                    Debug.Log($"[FusionDebug] LocalPlayer={_runner.LocalPlayer}");
                    Debug.Log($"[FusionDebug] IsServer={_runner.IsServer}, IsClient={_runner.IsClient}");
                }
                else
                {
                    Debug.LogError($"[FusionDebug] 连接失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FusionDebug] StartGame 异常: {ex.Message}");
                Debug.LogException(ex);
            }

            Debug.Log("[FusionDebug] === 测试结束 ===");
        }

        public void Disconnect()
        {
            if (_runner != null)
            {
                Debug.Log("[FusionDebug] 断开连接...");
                _runner.Shutdown();
                _runner = null;
            }
        }

        // ─── INetworkRunnerCallbacks 实现（全部带日志）───

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[FusionDebug] OnPlayerJoined: Player={player}, IsServer={runner.IsServer}");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[FusionDebug] OnPlayerLeft: Player={player}");
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log($"[FusionDebug] OnConnectedToServer: State={runner.State}");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"[FusionDebug] OnDisconnectedFromServer: Reason={reason}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[FusionDebug] OnShutdown: Reason={shutdownReason}");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            Debug.Log($"[FusionDebug] OnConnectRequest: RemoteAddress={request.RemoteAddress}");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason)
        {
            Debug.LogError($"[FusionDebug] OnConnectFailed: Address={address}, Reason={reason}");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
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