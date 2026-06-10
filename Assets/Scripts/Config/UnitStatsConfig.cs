using System.Collections.Generic;
using DoudizhuTower.Gameplay.Entities;
using UnityEngine;

namespace DoudizhuTower.Config
{
    /// <summary>
    /// 兵种数值汇总 ScriptableObject。
    /// 作为 CSV 和预制体之间的中间层，集中管理所有兵种的基础属性。
    /// </summary>
    [CreateAssetMenu(fileName = "UnitStatsConfig", menuName = "DoudizhuTower/UnitStatsConfig")]
    public class UnitStatsConfig : ScriptableObject
    {
        [Tooltip("所有兵种的数值条目")]
        public List<UnitStatsEntry> units = new();
    }

    [System.Serializable]
    public class UnitStatsEntry
    {
        [Tooltip("预制体上的 CardUnit 组件引用")]
        public CardUnit prefab;

        [Tooltip("显示名（纯标注，不影响逻辑）")]
        public string displayName;

        [Tooltip("生命值")]
        public float hp = 100f;

        [Tooltip("攻击力")]
        public float atk = 10f;

        [Tooltip("攻击间隔（秒）")]
        public float attackInterval = 1.2f;

        [Tooltip("移动速度")]
        public float moveSpeed = 2.8f;

        [Tooltip("攻击范围")]
        public float range = 1.8f;

        [Tooltip("攻击命中次数")]
        public int hitCount = 1;

        [Tooltip("是否远程")]
        public bool isRanged;

        [Tooltip("高度标签")]
        public UnitHeight unitHeight = UnitHeight.Ground;

        [Tooltip("索敌范围（0=使用攻击范围）")]
        public float detectionRange;

        [Tooltip("是否建筑")]
        public bool isBuilding;

        [Tooltip("建筑回血速度")]
        public float regenPerSecond;
    }
}
