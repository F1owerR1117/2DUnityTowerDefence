using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 基于 Photon PUN 2 的网络服务实现。
    /// 实现 INetworkService 接口，封装所有 Photon API 调用。
    /// </summary>
    public class PhotonService : MonoBehaviourPunCallbacks, INetworkService
    {
        private const string READY_KEY = "ready";
        private const byte CUSTOM_EVENT_CODE = 1;

        private static PhotonService _instance;

        private void Awake()
        {
            // 防止重复实例
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─── 连接 ───

        public bool IsConnected => PhotonNetwork.IsConnected;
        public bool IsInRoom => PhotonNetwork.InRoom;
        public bool IsMasterClient => PhotonNetwork.IsMasterClient;

        public string CurrentRoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int CurrentPlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;

        public string LocalPlayerName
        {
            get => PhotonNetwork.NickName;
            set => PhotonNetwork.NickName = value;
        }

        public void Connect()
        {
            EnsureCallbackRegistered();
            if (!PhotonNetwork.IsConnected)
            {
                // 增大断线超时，防止应用失焦时被服务端踢出
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 30000;

                // 固定区域：确保所有客户端连接到同一区域，避免房间不可见
                if (string.IsNullOrEmpty(PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion))
                    PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "jp";

                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public void Disconnect()
        {
            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();
        }

        // ─── 应用焦点/暂停恢复自动重连 ───

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !PhotonNetwork.IsConnected)
            {
                TryReconnect();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && !PhotonNetwork.IsConnected)
            {
                TryReconnect();
            }
        }

        private void TryReconnect()
        {
            if (_shouldRejoinRoom)
            {
                Debug.Log("[Photon] 应用恢复，尝试重连并重新加入房间...");
                PhotonNetwork.ReconnectAndRejoin();
            }
            else
            {
                Debug.Log("[Photon] 应用恢复，尝试重连服务器...");
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        // ─── 房间 ───

        public void CreateRoom(string roomCode, int maxPlayers)
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.LogWarning("[Photon] CreateRoom 失败：客户端未就绪");
                return;
            }
            var options = new RoomOptions
            {
                MaxPlayers = (byte)maxPlayers,
                IsVisible = true,
                CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { { "host", PhotonNetwork.NickName } }
            };
            PhotonNetwork.CreateRoom(roomCode, options);
        }

        public void JoinRoom(string roomCode)
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.LogWarning("[Photon] JoinRoom 失败：客户端未就绪");
                return;
            }
            PhotonNetwork.JoinRoom(roomCode);
        }

        public void JoinRandomRoom()
        {
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                Debug.LogWarning("[Photon] JoinRandomRoom 失败：客户端未就绪");
                return;
            }
            PhotonNetwork.JoinRandomRoom();
        }

        public void LeaveRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                _shouldRejoinRoom = false;
                PhotonNetwork.LeaveRoom();
            }
        }

        // ─── 玩家 ───

        public string[] GetPlayerNames()
        {
            if (!PhotonNetwork.InRoom) return Array.Empty<string>();
            var names = new List<string>();
            foreach (var p in PhotonNetwork.PlayerList)
                names.Add(p.NickName);
            return names.ToArray();
        }

        public void SetPlayerReady(bool ready)
        {
            var props = new ExitGames.Client.Photon.Hashtable { { READY_KEY, ready } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public bool AreAllPlayersReady
        {
            get
            {
                if (!PhotonNetwork.InRoom) return false;
                foreach (var p in PhotonNetwork.PlayerList)
                {
                    object val;
                    if (!p.CustomProperties.TryGetValue(READY_KEY, out val) || (bool)val != true)
                        return false;
                }
                return PhotonNetwork.PlayerList.Length >= 3;
            }
        }

        // ─── 消息同步 ───

        public void SendToAll(string key, object value)
        {
            var data = new object[] { key, value };
            PhotonNetwork.RaiseEvent(CUSTOM_EVENT_CODE, data, RaiseEventOptions.Default, SendOptions.SendReliable);
        }

        public void SendToMaster(string key, object value)
        {
            int masterActor = PhotonNetwork.MasterClient.ActorNumber;
            var data = new object[] { key, value };
            var options = new RaiseEventOptions { TargetActors = new int[] { masterActor } };
            PhotonNetwork.RaiseEvent(CUSTOM_EVENT_CODE, data, options, SendOptions.SendReliable);
        }

        public void SendToPlayer(int actorNumber, string key, object value)
        {
            var data = new object[] { key, value };
            var options = new RaiseEventOptions { TargetActors = new int[] { actorNumber } };
            PhotonNetwork.RaiseEvent(CUSTOM_EVENT_CODE, data, options, SendOptions.SendReliable);
        }

        public void SetRoomProperty(string key, object value)
        {
            if (!PhotonNetwork.InRoom) return;
            var props = new ExitGames.Client.Photon.Hashtable { { key, value } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        public object GetRoomProperty(string key)
        {
            if (!PhotonNetwork.InRoom) return null;
            object val;
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out val);
            return val;
        }

        // ─── 场景同步 ───

        public void LoadScene(string sceneName)
        {
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.LoadLevel(sceneName);
        }

        // ─── 玩家标识 ───

        public int LocalActorNumber => PhotonNetwork.LocalPlayer.ActorNumber;

        public int[] GetPlayerActorNumbers()
        {
            if (!PhotonNetwork.InRoom) return Array.Empty<int>();
            var actors = new int[PhotonNetwork.PlayerList.Length];
            for (int i = 0; i < actors.Length; i++)
                actors[i] = PhotonNetwork.PlayerList[i].ActorNumber;
            Array.Sort(actors);
            return actors;
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
        public event Action<string, object, int> OnCustomEvent;

        // ─── Photon 回调 ───

        public override void OnConnectedToMaster()
        {
            Debug.Log($"[Photon] 已连接到服务器，区域: {PhotonNetwork.CloudRegion}");
            OnServerConnected?.Invoke();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("[Photon] 已进入大厅");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[Photon] 断开连接: {cause}");
            OnConnectionLost?.Invoke();

            // 超时断线（通常因应用失焦）自动尝试重连
            if (cause == DisconnectCause.ServerTimeout
                || cause == DisconnectCause.ClientTimeout
                || cause == DisconnectCause.Exception)
            {
                Debug.Log("[Photon] 超时断线，3 秒后尝试自动重连...");
                StartCoroutine(ReconnectAfterDelay(3f));
            }
        }

        private System.Collections.IEnumerator ReconnectAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            TryReconnect();
        }

        public override void OnCreatedRoom()
        {
            Debug.Log($"[Photon] 房间已创建: {PhotonNetwork.CurrentRoom.Name}");
            OnRoomCreateSuccess?.Invoke(PhotonNetwork.CurrentRoom.Name);
        }

        public override void OnJoinedRoom()
        {
            _shouldRejoinRoom = true;
            // 启用场景同步：Master 调用 LoadLevel 时自动同步到所有客户端
            PhotonNetwork.AutomaticallySyncScene = true;
            Debug.Log($"[Photon] 已加入房间: {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
            OnRoomJoinSuccess?.Invoke(PhotonNetwork.CurrentRoom.Name);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[Photon] 加入房间失败: {message}");
            OnRoomJoinError?.Invoke(message);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[Photon] 随机匹配失败: {message}");
            OnRoomJoinError?.Invoke(message);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[Photon] 玩家加入: {newPlayer.NickName}");
            OnPlayerJoined?.Invoke(newPlayer.NickName);
            CheckAllReady();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[Photon] 玩家离开: {otherPlayer.NickName}");
            OnPlayerLeft?.Invoke(otherPlayer.NickName);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if (changedProps.ContainsKey(READY_KEY))
                CheckAllReady();
        }

        private bool _callbackRegistered;
        private bool _shouldRejoinRoom;

        private void OnEnable()
        {
            if (PhotonNetwork.NetworkingClient != null)
            {
                base.OnEnable();
                PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
                _callbackRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (_callbackRegistered && PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.NetworkingClient.EventReceived -= OnEventReceived;
                base.OnDisable();
                _callbackRegistered = false;
            }
        }

        /// <summary>确保回调已注册（OnEnable 时 NetworkingClient 可能尚未初始化）</summary>
        private void EnsureCallbackRegistered()
        {
            if (!_callbackRegistered && PhotonNetwork.NetworkingClient != null)
            {
                base.OnEnable();
                PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
                _callbackRegistered = true;
            }
        }

        private void OnEventReceived(EventData eventData)
        {
            if (eventData.Code == CUSTOM_EVENT_CODE)
            {
                var data = (object[])eventData.CustomData;
                string key = (string)data[0];
                object value = data[1];
                int senderActor = eventData.Sender;
                OnCustomEvent?.Invoke(key, value, senderActor);
            }
        }

        private void CheckAllReady()
        {
            if (AreAllPlayersReady)
                OnAllPlayersReady?.Invoke();
        }
    }
}
