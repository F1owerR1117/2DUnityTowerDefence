using Fusion;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 身份提供者接口（Phase 5：唯一身份抽象层）。
    /// 所有系统通过此接口查询 slot，不再直接计算。
    /// </summary>
    public interface IIdentityProvider
    {
        int GetLocalSlot();
        int GetSlot(PlayerRef player);
        PlayerRef GetPlayer(int slot);
        bool IsReady();
    }
}
