using UnityEngine;

namespace DoudizhuTower.Config
{
    /// <summary>
    /// 叫分阶段配置表（ScriptableObject）。
    /// 通过菜单 DoudizhuTower/BiddingConfig 创建资产。
    /// </summary>
    [CreateAssetMenu(fileName = "BiddingConfig", menuName = "DoudizhuTower/BiddingConfig")]
    public class BiddingConfig : ScriptableObject
    {
        [Header("叫分规则")]
        [Tooltip("叫分总时长（秒）")]
        public float biddingDuration = 30f;

        [Tooltip("最高叫分（1/2/3）")]
        public int maxBid = 3;

        [Header("AI 策略")]
        [Tooltip("AI 不叫的概率（0~1）")]
        [Range(0f, 1f)]
        public float aiPassChance = 0.6f;

        [Tooltip("AI 叫 1 分的概率（在决定叫分后）")]
        [Range(0f, 1f)]
        public float aiBid1Weight = 0.5f;

        [Tooltip("AI 叫 2 分的概率（在决定叫分后）")]
        [Range(0f, 1f)]
        public float aiBid2Weight = 0.3f;

        [Tooltip("AI 叫 3 分的概率（在决定叫分后）")]
        [Range(0f, 1f)]
        public float aiBid3Weight = 0.2f;

        [Header("超时处理")]
        [Tooltip("超时无人叫分时是否随机分配地主")]
        public bool randomAssignOnTimeout = true;
    }
}
