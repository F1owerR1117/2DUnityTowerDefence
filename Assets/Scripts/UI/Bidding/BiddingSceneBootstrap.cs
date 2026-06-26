using DoudizhuTower.Gameplay.Fusion;
using DoudizhuTower.Gameplay.Network;
using UnityEngine;

namespace DoudizhuTower.UI.Bidding
{
    /// <summary>
    /// 叫分场景启动引导。
    /// 检测当前是否处于联机房间，决定激活 BiddingManager（单机）还是 NetworkBiddingManager（联机）。
    /// 联机模式下确保 FusionGameManager 已 Spawn。
    /// </summary>
    public class BiddingSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private BiddingManager singlePlayerManager;
        [SerializeField] private NetworkBiddingManager networkManager;

        private void Awake()
        {
            bool isConnected = NetworkFacade.IsConnected;
            bool isInRoom = NetworkFacade.IsInRoom;
            bool isNetwork = isConnected && isInRoom;

            Debug.Log($"[BiddingBootstrap] IsConnected={isConnected}, IsInRoom={isInRoom}, 模式={(isNetwork ? "联机" : "单机")}");

            if (singlePlayerManager != null)
                singlePlayerManager.gameObject.SetActive(!isNetwork);

            if (networkManager != null)
                networkManager.gameObject.SetActive(isNetwork);

            // 联机模式：确保 FusionGameManager 已 Spawn（叫分场景需要 WorldState）
            if (isNetwork)
            {
                EnsureFusionGameManager();
            }
        }

        private void EnsureFusionGameManager()
        {
            if (FusionGameManager.Instance != null)
            {
                Debug.Log("[BiddingBootstrap] FusionGameManager 已存在");
                return;
            }

            var fusionService = FindFirstObjectByType<FusionService>();
            if (fusionService == null || fusionService.Runner == null || !fusionService.Runner.IsRunning)
            {
                Debug.LogWarning("[BiddingBootstrap] FusionService 未就绪");
                return;
            }

            if (!fusionService.IsMasterClient)
            {
                Debug.Log("[BiddingBootstrap] Client 不 Spawn，等待 Host 的 FusionGameManager 网络同步");
                return;
            }

            Debug.Log("[BiddingBootstrap] Host Spawn FusionGameManager");
            fusionService.SpawnFusionGameManager();
        }
    }
}
