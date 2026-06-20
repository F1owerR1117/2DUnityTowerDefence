using DoudizhuTower.Gameplay.Network;
using UnityEngine;

namespace DoudizhuTower.UI.Bidding
{
    /// <summary>
    /// 叫分场景启动引导。
    /// 检测当前是否处于联机房间，决定激活 BiddingManager（单机）还是 NetworkBiddingManager（联机）。
    /// 挂载到叫分场景中同时拥有 BiddingManager 和 NetworkBiddingManager 的 GameObject 上。
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
        }
    }
}
