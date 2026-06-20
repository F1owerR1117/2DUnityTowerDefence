using UnityEngine;
using Fusion;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 玩家输入处理器。
    /// 替代 SendToMaster RPC，将所有操作转换为 INetworkInput。
    /// 
    /// Fusion 2 输入模型：
    /// - 客户端调用 SubmitNetworkInput() 设置输入
    /// - 服务端在 FixedUpdateNetwork() 中通过 GetInput<PlayerInput>() 读取
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private FusionGameManager gameManager;

        private FusionPlayerInput _pendingInput;
        private bool _hasPendingInput;

        /// <summary>
        /// 设置引用。
        /// </summary>
        public void Setup(FusionGameManager manager)
        {
            gameManager = manager;
        }

        /// <summary>
        /// 出牌操作。
        /// </summary>
        public void PlayCard(byte cardId, byte targetSlot = 0)
        {
            if (gameManager == null) return;

            _pendingInput = new FusionPlayerInput
            {
                Action = 1,
                CardId = cardId,
                Target = targetSlot
            };
            _hasPendingInput = true;
        }

        /// <summary>
        /// 摸牌操作。
        /// </summary>
        public void DrawCard()
        {
            if (gameManager == null) return;

            _pendingInput = new FusionPlayerInput
            {
                Action = 2
            };
            _hasPendingInput = true;
        }

        /// <summary>
        /// 叫分操作。
        /// </summary>
        public void Bid(byte bidValue)
        {
            if (gameManager == null) return;

            _pendingInput = new FusionPlayerInput
            {
                Action = 3,
                CardId = bidValue
            };
            _hasPendingInput = true;
        }

        /// <summary>
        /// 通用操作（用于扩展）。
        /// </summary>
        public void SendAction(byte action, byte param1 = 0, byte param2 = 0)
        {
            if (gameManager == null) return;

            _pendingInput = new FusionPlayerInput
            {
                Action = action,
                CardId = param1,
                Target = param2
            };
            _hasPendingInput = true;
        }

        /// <summary>
        /// 供 FusionGameManager 在 FixedUpdateNetwork 中调用。
        /// </summary>
        public bool TryGetInput(out FusionPlayerInput input)
        {
            if (_hasPendingInput)
            {
                input = _pendingInput;
                _hasPendingInput = false;
                return true;
            }
            input = default;
            return false;
        }
    }
}