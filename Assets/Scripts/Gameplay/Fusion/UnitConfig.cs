using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 兵种配置（Inspector 可调）。
    /// 存储兵种的基础属性，不包含运行时数据。
    /// </summary>
    [CreateAssetMenu(fileName = "UnitConfig", menuName = "DoudizhuTower/Fusion/UnitConfig")]
    public class UnitConfig : ScriptableObject
    {
        [Header("基本信息")]
        public int unitTypeId;
        public string displayName;

        [Header("属性")]
        public int maxHP = 100;
        public int attackDamage = 10;
        public float attackInterval = 1.2f;
        public float moveSpeed = 2.8f;
        public float attackRange = 1.8f;

        [Header("攻击设置")]
        public int hitCount = 1;
        public int maxTargets = 1;
        public bool isRanged;

        [Header("类型")]
        public bool isBuilding;
        public float regenPerSecond;
    }
}