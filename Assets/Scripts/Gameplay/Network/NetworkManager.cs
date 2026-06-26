using UnityEngine;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 网络管理器单例。
    /// 持有 INetworkService 引用，供全局访问。
    /// 挂载到 OnlineLobby 场景的 GameObject 上，DontDestroyOnLoad 跨场景。
    ///
    /// 更换网络方案时：只需将实现 INetworkService 的 MonoBehaviour 拖入 serviceProvider 字段，
    /// 或在同一 GameObject 上挂载新实现组件即可，无需修改任何业务代码。
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Tooltip("网络服务提供者（拖入 FusionService，或留空自动查找）")]
        [SerializeField] private MonoBehaviour serviceProvider;

        /// <summary>当前使用的网络服务</summary>
        public INetworkService Service { get; private set; }

        /// <summary>是否已连接到服务器</summary>
        public bool IsConnected => Service != null && Service.IsConnected;

        /// <summary>是否在房间中</summary>
        public bool IsInRoom => Service != null && Service.IsInRoom;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 检查 serviceProvider 是否有效
            if (serviceProvider != null && serviceProvider is not INetworkService)
            {
                Debug.LogWarning("[NetworkManager] serviceProvider 不是 INetworkService，尝试自动查找 FusionService");
                serviceProvider = null;
            }

            // 自动查找 FusionService
            if (serviceProvider == null)
            {
                foreach (var mb in GetComponentsInChildren<MonoBehaviour>())
                {
                    if (mb is FusionService)
                    {
                        serviceProvider = mb;
                        Debug.Log("[NetworkManager] 自动找到 FusionService");
                        break;
                    }
                }
            }

            // 如果还没找到，创建 FusionService
            if (serviceProvider == null)
            {
                Debug.Log("[NetworkManager] 未找到 FusionService，自动创建");
                var go = new GameObject("FusionService");
                var fusionService = go.AddComponent<FusionService>();
                fusionService.Connect();
                serviceProvider = fusionService;
            }

            if (serviceProvider is INetworkService service)
            {
                Service = service;
                Debug.Log($"[NetworkManager] Service = {Service.GetType().Name}");

                // Phase 5 Final：初始化 IdentityService
                InitIdentityService(service);
            }
            else
            {
                // 尝试自动创建 FusionService
                Debug.LogWarning("[NetworkManager] 未找到 INetworkService，尝试创建 FusionService");
                var fusionGO = new GameObject("FusionService");
                var fusionService = fusionGO.AddComponent<FusionService>();
                fusionService.Connect();
                Service = fusionService;
                InitIdentityService(fusionService);
            }
        }

        private void InitIdentityService(INetworkService service)
        {
            var identityService = FindAnyObjectByType<DoudizhuTower.Gameplay.Fusion.IdentityService>();
            if (identityService == null)
            {
                // 自动创建 IdentityService
                Debug.Log("[NetworkManager] 自动创建 IdentityService");
                var go = new GameObject("IdentityService");
                identityService = go.AddComponent<DoudizhuTower.Gameplay.Fusion.IdentityService>();
            }

            if (service is DoudizhuTower.Gameplay.Network.FusionService)
            {
                var lobbyId = FindAnyObjectByType<DoudizhuTower.Gameplay.Fusion.LobbyIdentityService>();
                if (lobbyId == null)
                {
                    // 自动创建 LobbyIdentityService
                    Debug.Log("[NetworkManager] 自动创建 LobbyIdentityService");
                    var go = new GameObject("LobbyIdentityService");
                    lobbyId = go.AddComponent<DoudizhuTower.Gameplay.Fusion.LobbyIdentityService>();
                }
                identityService.Initialize(new DoudizhuTower.Gameplay.Fusion.OnlineIdentityProvider());
            }
            else
            {
                identityService.Initialize(new DoudizhuTower.Gameplay.Fusion.OfflineIdentityProvider());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>连接到服务器</summary>
        public void Connect()
        {
            Service?.Connect();
        }

        /// <summary>断开连接</summary>
        public void Disconnect()
        {
            Service?.Disconnect();
        }
    }
}
