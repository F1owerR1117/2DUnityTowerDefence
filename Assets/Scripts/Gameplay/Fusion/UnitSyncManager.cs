using UnityEngine;
using Fusion;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 单位同步管理器。
    /// 负责 Host 和 Client 之间的单位状态同步。
    /// </summary>
    public class UnitSyncManager : NetworkBehaviour
    {
        [Header("引用")]
        [SerializeField] private FusionGameManager gameManager;

        // 同步间隔（每 N 帧同步一次）
        private const int SYNC_INTERVAL = 2;
        private int _syncTimer;

        public override void FixedUpdateNetwork()
        {
            if (gameManager == null) return;

            _syncTimer++;
            if (_syncTimer < SYNC_INTERVAL) return;
            _syncTimer = 0;

            if (HasStateAuthority)
            {
                // Host: 将 _unitBuffer 数据同步到 RPC
                SyncToClients();
            }
        }

        /// <summary>
        /// Host 同步数据到 Client
        /// </summary>
        private void SyncToClients()
        {
            var buffer = gameManager.GetUnitBuffer();
            if (buffer == null) return;

            // 发送单位数量
            int count = buffer.Count;
            RpcSyncUnitCount(count);

            // 发送每个单位的状态
            for (int i = 0; i < count; i++)
            {
                var unit = buffer.Get(i);
                RpcSyncUnitState(unit.UnitId, unit.Owner, unit.PosX, unit.PosY, 
                    unit.HP, unit.MaxHP, unit.TargetId, unit.State, 
                    unit.AttackTimer, unit.MoveSpeed, unit.AttackRange, 
                    unit.IsLandlord, unit.PassiveFlags);
            }
        }

        /// <summary>
        /// RPC: 同步单位数量
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcSyncUnitCount(int count)
        {
            if (HasStateAuthority) return; // Host 不需要处理

            // Client: 确保 _unitBuffer 有正确的数量
            var buffer = gameManager.GetUnitBuffer();
            while (buffer.Count < count)
            {
                buffer.Add(default);
            }
        }

        /// <summary>
        /// RPC: 同步单个单位状态
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcSyncUnitState(int unitId, int owner, float posX, float posY,
            int hp, int maxHP, int targetId, byte state, float attackTimer,
            float moveSpeed, float attackRange, byte isLandlord, byte passiveFlags)
        {
            if (HasStateAuthority) return; // Host 不需要处理

            var buffer = gameManager.GetUnitBuffer();
            int index = buffer.FindIndex(unitId);

            if (index == -1)
            {
                // 单位不存在，创建新的
                buffer.Add(new UnitState
                {
                    UnitId = unitId,
                    Owner = owner,
                    PosX = posX,
                    PosY = posY,
                    HP = hp,
                    MaxHP = maxHP,
                    TargetId = targetId,
                    State = state,
                    AttackTimer = attackTimer,
                    MoveSpeed = moveSpeed,
                    AttackRange = attackRange,
                    IsLandlord = isLandlord,
                    PassiveFlags = passiveFlags
                });
            }
            else
            {
                // 更新现有单位
                var unit = buffer.Get(index);
                unit.PosX = posX;
                unit.PosY = posY;
                unit.HP = hp;
                unit.TargetId = targetId;
                unit.State = state;
                unit.AttackTimer = attackTimer;
                buffer.Set(index, unit);
            }
        }
    }
}