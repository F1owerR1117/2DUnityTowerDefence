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

            int count = buffer.Count;
            RpcSyncUnitCount(count);

            for (int i = 0; i < count; i++)
            {
                var unit = buffer.Get(i);
                RpcSyncUnitState(unit.UnitId, unit.Owner, unit.PosX, unit.PosY,
                    unit.HP, unit.MaxHP, unit.ATK, unit.AttackSpeed,
                    unit.AttackTimer, unit.AttackRange,
                    unit.TargetId, unit.State, unit.MoveSpeed, unit.IsLandlord);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcSyncUnitCount(int count)
        {
            if (HasStateAuthority) return;

            var buffer = gameManager.GetUnitBuffer();
            while (buffer.Count < count)
            {
                buffer.Add(default);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcSyncUnitState(int unitId, int owner, float posX, float posY,
            int hp, int maxHP, int atk, float attackSpeed,
            float attackTimer, float attackRange,
            int targetId, byte state, float moveSpeed, byte isLandlord)
        {
            if (HasStateAuthority) return;

            var buffer = gameManager.GetUnitBuffer();
            int index = buffer.FindIndex(unitId);

            if (index == -1)
            {
                buffer.Add(new UnitState
                {
                    UnitId = unitId,
                    Owner = owner,
                    PosX = posX,
                    PosY = posY,
                    HP = hp,
                    MaxHP = maxHP,
                    ATK = atk,
                    AttackSpeed = attackSpeed,
                    AttackTimer = attackTimer,
                    AttackRange = attackRange,
                    TargetId = targetId,
                    State = state,
                    MoveSpeed = moveSpeed,
                    IsLandlord = isLandlord
                });
            }
            else
            {
                var unit = buffer.Get(index);
                unit.PosX = posX;
                unit.PosY = posY;
                unit.HP = hp;
                unit.ATK = atk;
                unit.AttackSpeed = attackSpeed;
                unit.TargetId = targetId;
                unit.State = state;
                unit.AttackTimer = attackTimer;
                buffer.Set(index, unit);
            }
        }
    }
}
