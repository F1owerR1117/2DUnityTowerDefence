using Fusion;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 单机模式身份提供者。
    /// 本地玩家固定 slot=0，不依赖任何网络系统。
    /// </summary>
    public class OfflineIdentityProvider : IIdentityProvider
    {
        public int GetLocalSlot() => 0;

        public int GetSlot(PlayerRef player) => 0;

        public PlayerRef GetPlayer(int slot) => default;

        public bool IsReady() => true;
    }
}
