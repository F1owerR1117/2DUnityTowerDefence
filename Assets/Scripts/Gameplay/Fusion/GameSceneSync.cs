using System.Collections.Generic;
using UnityEngine;
using DoudizhuTower.Gameplay.Systems;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 游戏场景同步组件。
    /// Host 定期广播关键状态，Client 接收后更新本地 UI。
    /// </summary>
    public class GameSceneSync : MonoBehaviour
    {
        [SerializeField] private float syncInterval = 0.5f;

        private FusionGameManager _gm;
        private float _syncTimer;
        private int _mySlot = -1;

        private void Start()
        {
            _gm = FusionGameManager.Instance;
            if (_gm != null)
                _mySlot = _gm.GetLocalSlot();
        }

        private void Update()
        {
            if (_gm == null)
            {
                _gm = FusionGameManager.Instance;
                if (_gm != null)
                    _mySlot = _gm.GetLocalSlot();
                return;
            }

            if (!_gm.HasStateAuthority) return;

            _syncTimer -= Time.deltaTime;
            if (_syncTimer <= 0f)
            {
                _syncTimer = syncInterval;
                BroadcastState();
            }
        }

        private void BroadcastState()
        {
            var world = _gm.World;
            int deckCount = world.Game.DeckCount;

            // 广播牌堆数量
            _gm.RpcSyncDeckCount(deckCount);

            // 广播各玩家手牌
            for (int slot = 0; slot < 3; slot++)
            {
                var player = _gm.GetPlayerState(slot);
                if (player.IsAI == 1) continue;

                int handCount = player.HandCount;
                byte[] handCards = _gm.GetHandCardsArray(slot);
                if (handCards != null && handCards.Length > 0)
                {
                    _gm.RpcSyncHandCards(slot, handCards);
                }

                _gm.RpcSyncGold(slot, player.Gold);
            }
        }
    }
}
