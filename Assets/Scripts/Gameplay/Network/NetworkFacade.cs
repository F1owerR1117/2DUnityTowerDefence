using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 网络门面（统一入口）。
    /// UI 层和游戏逻辑层只依赖此接口，不直接使用 Photon 或 Fusion API。
    /// </summary>
    public static class NetworkFacade
    {
        /// <summary>
        /// 获取当前网络服务
        /// </summary>
        public static INetworkService Service
        {
            get
            {
                if (NetworkManager.Instance == null)
                {
                    Debug.LogWarning("[NetworkFacade] NetworkManager.Instance is null");
                    return null;
                }
                return NetworkManager.Instance.Service;
            }
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public static bool IsConnected => Service?.IsConnected ?? false;

        /// <summary>
        /// 是否在房间中
        /// </summary>
        public static bool IsInRoom => Service?.IsInRoom ?? false;

        /// <summary>
        /// 是否是主机
        /// </summary>
        public static bool IsMasterClient => Service?.IsMasterClient ?? false;

        // ─── 连接 ───
        public static void Connect() => Service?.Connect();
        public static void Disconnect() => Service?.Disconnect();

        // ─── 房间 ───
        public static void CreateRoom(string roomCode, int maxPlayers) => Service?.CreateRoom(roomCode, maxPlayers);
        public static void JoinRoom(string roomCode) => Service?.JoinRoom(roomCode);
        public static void JoinRandomRoom() => Service?.JoinRandomRoom();
        public static void LeaveRoom() => Service?.LeaveRoom();

        [System.Obsolete] public static void SendToAll(string key, object value) => Service?.SendToAll(key, value);
        [System.Obsolete] public static void SendToMaster(string key, object value) => Service?.SendToMaster(key, value);
        [System.Obsolete] public static void SendToPlayer(int actorNumber, string key, object value) => Service?.SendToPlayer(actorNumber, key, value);
    }
}