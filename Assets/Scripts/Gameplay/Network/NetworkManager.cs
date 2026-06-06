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

        [Tooltip("网络服务提供者（拖入实现了 INetworkService 的 MonoBehaviour，如 PhotonService）")]
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

            // 优先使用 Inspector 指定的服务提供者
            if (serviceProvider == null)
            {
                // 自动查找任意实现了 INetworkService 的组件
                foreach (var mb in GetComponentsInChildren<MonoBehaviour>())
                {
                    if (mb is INetworkService)
                    {
                        serviceProvider = mb;
                        break;
                    }
                }
            }

            if (serviceProvider is INetworkService service)
            {
                Service = service;
            }
            else
            {
                Debug.LogError("[NetworkManager] 未找到实现 INetworkService 的组件！" +
                               "请在 Inspector 中拖入，或在同一 GameObject 上挂载实现类。");
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
