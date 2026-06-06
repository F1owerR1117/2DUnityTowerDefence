using System;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Battle
{
    /// <summary>
    /// 可攻击目标接口。所有可被兵种锁定为攻击目标的对象实现此接口。
    /// CardUnit（_isBuilding 的建筑型单位）是唯一实现。BOSS 单位使用 _isBoss 标记，独立于 _isBuilding。
    /// </summary>
    public interface IBuildingTarget
    {
        bool IsDestroyed { get; }
        Transform transform { get; }
        /// <summary>预制体 Awake 时快取的 Collider2D，供高頻邊緣距離計算 O(1) 讀取</summary>
        Collider2D BuildingCollider { get; }
        /// <summary>邏輯幾何中心，回退用</summary>
        Vector2 LogicCenter { get; }
        void TakeDamage(float rawDamage);
        event Action<IBuildingTarget> OnDestroyed;
    }
}
