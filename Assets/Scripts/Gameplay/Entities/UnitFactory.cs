using System.Collections.Generic;
using DoudizhuTower.Core.Battle;
using DoudizhuTower.Core.Cards;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 兵种工厂（对象池模式）。
    /// 按预制体引用分池，支持任意预制体的懒创建和回收。
    /// </summary>
    public class UnitFactory : MonoBehaviour
    {
        // 按预制体引用分池（同一预制体跨 Rank 复用）
        private readonly Dictionary<CardUnit, Queue<CardUnit>> _pools = new();
        private int _nextUnitId;

        /// <summary>
        /// 生成兵种
        /// </summary>
        /// <param name="prefab">要生成的预制体</param>
        /// <param name="rank">牌值（用于 Stats.Rank）</param>
        /// <param name="lane">路线</param>
        /// <param name="position">生成位置</param>
        /// <param name="isLandlord">是否地主阵营</param>
        public CardUnit Spawn(CardUnit prefab, CardRank rank, Lane lane, Vector2 position, bool isLandlord)
        {
            if (prefab == null)
            {
                Debug.LogError("[UnitFactory] 预制体为空");
                return null;
            }

            // 懒创建对象池
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<CardUnit>();
                _pools[prefab] = pool;
            }

            // 从池中取出或新建
            CardUnit unit;
            if (pool.Count > 0)
            {
                unit = pool.Dequeue();
                if (unit == null)
                {
                    // 临时禁用防止 Start() 在 Initialize() 之前执行（用错误的 Inspector _isLandlord 值）
                    prefab.gameObject.SetActive(false);
                    unit = Instantiate(prefab, position, Quaternion.identity, transform);
                    prefab.gameObject.SetActive(true);
                }
                else
                {
                    unit.transform.position = position;
                    unit.OnPoolSpawn();
                }
            }
            else
            {
                // 临时禁用防止 Start() 在 Initialize() 之前执行
                prefab.gameObject.SetActive(false);
                unit = Instantiate(prefab, position, Quaternion.identity, transform);
                prefab.gameObject.SetActive(true);
            }

            // 记录来源预制体（回收时查池用）
            unit.SourcePrefab = prefab;

            // 初始化（必须在 gameObject.SetActive(true) 之前，确保 Start() 看到 _initialized=true）
            int id = _nextUnitId++;
            unit.Initialize(id, rank, lane, isLandlord);

            // 激活单位（Instantiate 时 prefab 被临时禁用，所以 copy 也是 inactive 的）
            unit.gameObject.SetActive(true);

            // 初始化血条
            var healthBar = unit.GetComponentInChildren<UnitHealthBar>(true);
            if (healthBar != null)
            {
                healthBar.Initialize(unit);
                healthBar.gameObject.SetActive(true);
            }

            return unit;
        }

        /// <summary>
        /// 回收兵种到对象池
        /// </summary>
        public void Despawn(CardUnit unit)
        {
            if (unit == null) return;

            var prefab = unit.SourcePrefab;
            if (prefab != null && _pools.TryGetValue(prefab, out var pool))
            {
                unit.OnPoolDespawn();
                pool.Enqueue(unit);
            }
            else
            {
                Destroy(unit.gameObject);
            }
        }
    }
}
