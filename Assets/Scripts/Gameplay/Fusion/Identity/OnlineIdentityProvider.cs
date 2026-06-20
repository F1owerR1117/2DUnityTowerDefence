using Fusion;
using UnityEngine;
using DoudizhuTower.Gameplay.Network;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 联机模式身份提供者。
    /// 通过 LobbyIdentityService 查询 slot。
    /// </summary>
    public class OnlineIdentityProvider : IIdentityProvider
    {
        public int GetLocalSlot()
        {
            if (LobbyIdentityService.Instance == null) return -1;
            var service = UnityEngine.Object.FindAnyObjectByType<FusionService>();
            if (service == null || service.Runner == null || service.Runner.LocalPlayer.IsNone) return -1;
            return LobbyIdentityService.Instance.GetSlot(service.Runner.LocalPlayer);
        }

        public int GetSlot(PlayerRef player)
        {
            return LobbyIdentityService.Instance != null
                ? LobbyIdentityService.Instance.GetSlot(player)
                : -1;
        }

        public PlayerRef GetPlayer(int slot)
        {
            return LobbyIdentityService.Instance != null
                ? LobbyIdentityService.Instance.GetPlayer(slot)
                : default;
        }

        public bool IsReady()
        {
            return LobbyIdentityService.Instance != null;
        }
    }
}
