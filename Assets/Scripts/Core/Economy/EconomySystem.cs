using System;

namespace DoudizhuTower.Core.Economy
{
    /// <summary>
    /// 金币经济系统（纯 C#，无 Unity 依赖）。
    /// 管理金币增减、回金速度、经济成长曲线（§2.3/§2.3a）。
    /// </summary>
    public class EconomySystem
    {
        /// <summary>当前金币（内部精确值，UI 显示 floor）</summary>
        public float CurrentGold { get; private set; }

        /// <summary>当前回金速度（金币/秒）</summary>
        public float IncomeRate { get; private set; }

        /// <summary>累计获得金币总量</summary>
        public float TotalGoldEarned { get; private set; }

        /// <summary>累计消耗金币总量</summary>
        public float TotalGoldSpent { get; private set; }

        public EconomySystem(float initialGold, float baseIncomeRate)
        {
            CurrentGold = initialGold;
            IncomeRate = baseIncomeRate;
            TotalGoldEarned = 0f;
            TotalGoldSpent = 0f;
        }

        /// <summary>
        /// 每帧调用一次，根据回金速度增加金币
        /// </summary>
        /// <param name="deltaTime">帧时间（秒）</param>
        public void UpdateEconomy(float deltaTime)
        {
            float increment = IncomeRate * deltaTime;
            CurrentGold += increment;
            TotalGoldEarned += increment;
            OnGoldChanged?.Invoke(CurrentGold);
        }

        /// <summary>
        /// 尝试消耗金币
        /// </summary>
        /// <returns>金币足够时扣费并返回 true，否则返回 false</returns>
        public bool TrySpend(float amount)
        {
            if (amount < 0f)
                throw new ArgumentException("消耗金额不能为负数", nameof(amount));

            if (CurrentGold < amount)
                return false;

            CurrentGold -= amount;
            TotalGoldSpent += amount;
            OnGoldChanged?.Invoke(CurrentGold);
            return true;
        }

        /// <summary>
        /// 直接增加金币（用于击杀金币、技能收入等）
        /// </summary>
        public void AddGold(float amount)
        {
            if (amount < 0f)
                throw new ArgumentException("增加金额不能为负数", nameof(amount));

            CurrentGold += amount;
            TotalGoldEarned += amount;
            OnGoldChanged?.Invoke(CurrentGold);
        }

        /// <summary>
        /// 强制设置金币（联机同步用，跳过校验）
        /// </summary>
        public void SetGold(float amount)
        {
            CurrentGold = amount;
            OnGoldChanged?.Invoke(CurrentGold);
        }

        /// <summary>
        /// 设置回金速度
        /// </summary>
        public void SetIncomeRate(float rate)
        {
            IncomeRate = rate;
            OnIncomeChanged?.Invoke(IncomeRate);
        }

        /// <summary>
        /// 金币变动事件（参数为当前金币总额）
        /// </summary>
        public event Action<float> OnGoldChanged;

        /// <summary>
        /// 回金速度变动事件
        /// </summary>
        public event Action<float> OnIncomeChanged;
    }
}
