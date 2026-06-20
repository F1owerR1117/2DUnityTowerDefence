using System;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 网络服务抽象接口。
    /// UI 层和游戏逻辑层只依赖此接口，不直接使用 Photon API。
    /// 更换网络方案时只需替换实现类。
    /// </summary>
    public interface INetworkService
    {
        // ─── 连接 ───
        void Connect();
        void Disconnect();
        bool IsConnected { get; }

        // ─── 房间 ───
        void CreateRoom(string roomCode, int maxPlayers);
        void JoinRoom(string roomCode);
        void JoinRandomRoom();
        void LeaveRoom();
        bool IsInRoom { get; }
        bool IsMasterClient { get; }
        string CurrentRoomName { get; }
        int CurrentPlayerCount { get; }
        int MaxPlayers { get; }

        // ─── 玩家 ───
        string LocalPlayerName { get; set; }
        string[] GetPlayerNames();
        void SetPlayerReady(bool ready);
        bool AreAllPlayersReady { get; }

        [System.Obsolete("Phase 5 废弃：Fusion 使用 WorldState + Host Simulation，不再支持事件广播")]
        void SendToAll(string key, object value);
        [System.Obsolete("Phase 5 废弃：Fusion 使用 WorldState + Host Simulation，不再支持事件广播")]
        void SendToMaster(string key, object value);
        [System.Obsolete("Phase 5 废弃：Fusion 使用 WorldState + Host Simulation，不再支持事件广播")]
        void SendToPlayer(int actorNumber, string key, object value);

        void SetRoomProperty(string key, object value);
        object GetRoomProperty(string key);

        // ─── 场景同步 ───
        void LoadScene(string sceneName);

        // ─── 玩家标识 ───
        int LocalActorNumber { get; }
        int[] GetPlayerActorNumbers();
        int GetActorNumberAtPosition(int position);

        // ─── 事件 ───
        event Action OnServerConnected;
        event Action OnConnectionLost;
        event Action<string> OnRoomCreateSuccess;
        event Action<string> OnRoomJoinSuccess;
        event Action<string> OnRoomJoinError;
        event Action<string> OnPlayerJoined;
        event Action<string> OnPlayerLeft;
        event Action OnAllPlayersReady;
        event Action OnPlayerReadyChanged;
        event Action<string, object, int> OnCustomEvent;
        event Action OnMasterSwitched;
    }
}
