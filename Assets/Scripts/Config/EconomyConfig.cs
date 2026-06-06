using UnityEngine;

namespace DoudizhuTower.Config
{
    /// <summary>
    /// 经济系统 ScriptableObject 配置表（§2.3/§2.3a）。
    /// 所有可调数值集中在此，避免硬编码。
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "DoudizhuTower/EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("初始经济")]
        [Tooltip("开局初始金币")]
        public float initialGold = 50f;

        [Tooltip("农民基础回金速度（金币/秒）")]
        public float farmerBaseIncome = 5f;

        [Tooltip("地主额外回金加成（+2/秒，§2.2a）")]
        public float landlordBonusIncome = 2f;

        [Header("经济成长（§2.3a）")]
        [Tooltip("每分钟回金速度增长值")]
        public float incomeStepPerMinute = 1f;

        [Tooltip("骤死期金币倍率")]
        public float suddenDeathMultiplier = 2f;

        [Header("费用公式（§2.3）")]
        [Tooltip("点数3的基础市值 C_3")]
        public float baseCostC3 = 10f;

        [Tooltip("Cost 增长系数（1.17）")]
        public float costGrowthRate = 1.17f;
    }
}
