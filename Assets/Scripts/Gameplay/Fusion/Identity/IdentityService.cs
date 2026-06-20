using Fusion;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 身份服务（唯一入口 Facade）。
    /// 所有系统通过此查询 slot，不再直接计算。
    /// Phase 5 Final：slot 从"数据"彻底变成"查询结果"。
    /// </summary>
    public class IdentityService : MonoBehaviour
    {
        public static IdentityService Instance { get; private set; }

        private IIdentityProvider _provider;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 初始化身份提供者（由 GameBootstrap 调用）。
        /// </summary>
        public void Initialize(IIdentityProvider provider)
        {
            _provider = provider;
            Debug.Log($"[IdentityService] Initialized: {provider.GetType().Name}");
        }

        public int GetLocalSlot()
        {
            return _provider != null ? _provider.GetLocalSlot() : -1;
        }

        public int GetSlot(PlayerRef player)
        {
            return _provider != null ? _provider.GetSlot(player) : -1;
        }

        public PlayerRef GetPlayer(int slot)
        {
            return _provider != null ? _provider.GetPlayer(slot) : default;
        }

        public bool IsReady()
        {
            return _provider != null && _provider.IsReady();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
