using System;
using DoudizhuTower.Core.Economy;
using DoudizhuTower.Config;
using TMPro;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 经济系统焊接层（MonoBehaviour）。
    /// 将 Core.EconomySystem 的事件绑定到 UI 组件。
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI incomeText;

        [Header("配置")]
        [SerializeField] private EconomyConfig config;

        private EconomySystem _coreEconomy;
        private TimerQueue _timerQueue;
        private GameStateMachine _stateMachine;
        private float _baseIncomeRate;

        /// <summary>当前金币（委托给 Core）</summary>
        public float CurrentGold => _coreEconomy?.CurrentGold ?? 0f;

        /// <summary>每次消耗时触发，参数为消耗金额</summary>
        public event Action<float> OnGoldSpent;

        /// <summary>每次获得金币时触发，参数为获得金额</summary>
        public event Action<float> OnGoldEarned;

        /// <summary>
        /// 由 GameBootstrapper 调用
        /// </summary>
        public void Initialize(EconomySystem economyLogic, TimerQueue timerQueue, GameStateMachine stateMachine = null)
        {
            _coreEconomy = economyLogic;
            _timerQueue = timerQueue;
            _stateMachine = stateMachine;
            _baseIncomeRate = economyLogic.IncomeRate;

            // 焊接事件
            _coreEconomy.OnGoldChanged += UpdateGoldUI;
            _coreEconomy.OnIncomeChanged += UpdateIncomeUI;

            // 订阅骤死期切换
            if (_stateMachine != null)
                _stateMachine.OnPhaseChanged += OnPhaseChanged;

            // 初始更新
            UpdateGoldUI(_coreEconomy.CurrentGold);
            UpdateIncomeUI(_coreEconomy.IncomeRate);

            // 启动经济成长定时器（每分钟 +1 回金速度）
            StartIncomeGrowth();
        }

        /// <summary>
        /// 尝试消耗金币
        /// </summary>
        public bool TrySpendGold(float amount)
        {
            if (_coreEconomy == null) return false;

            float goldBefore = _coreEconomy.CurrentGold;
            bool success = _coreEconomy.TrySpend(amount);
            float goldAfter = _coreEconomy.CurrentGold;

            if (success && goldAfter > goldBefore)
            {
                UnityEngine.Debug.LogError($"<color=red>[经济系统恶性Bug严重警告]</color> 触发了扣费，但金币不减反增！扣费前: {goldBefore}, 扣费后: {goldAfter}, 试图扣除: {amount}。游戏已强行暂停，请检查调用栈！");
                UnityEngine.Debug.Break();
            }

            if (success)
                OnGoldSpent?.Invoke(amount);

            return success;
        }

        /// <summary>
        /// 增加金币（击杀奖励等）
        /// </summary>
        public void AddGold(float amount)
        {
            _coreEconomy?.AddGold(amount);
            if (amount > 0f) OnGoldEarned?.Invoke(amount);
        }

        /// <summary>
        /// 强制设置金币（联机同步用）
        /// </summary>
        public void SetGold(float amount)
        {
            _coreEconomy?.SetGold(amount);
        }

        /// <summary>
        /// 设置回金速度（暴君超频等）
        /// </summary>
        public void SetIncomeRate(float rate)
        {
            _coreEconomy?.SetIncomeRate(rate);
        }

        /// <summary>
        /// 临时提升回金速度（双王炸等 buff）
        /// </summary>
        public void BoostIncomeRate(float multiplier, float duration)
        {
            if (_coreEconomy == null) return;
            float originalRate = _coreEconomy.IncomeRate;
            _coreEconomy.SetIncomeRate(originalRate * multiplier);

            _timerQueue?.Schedule(duration, () =>
            {
                if (_coreEconomy != null)
                    _coreEconomy.SetIncomeRate(originalRate);
            });
        }

        private void StartIncomeGrowth()
        {
            if (_timerQueue == null) return;
            _timerQueue.ScheduleLoop(60f, () =>
            {
                if (_coreEconomy != null)
                    _coreEconomy.SetIncomeRate(_coreEconomy.IncomeRate + config.incomeStepPerMinute);
            });
        }

        private Color _goldTextOriginalColor;
        private float _goldFlashTimer;

        /// <summary>金币文字闪烁红色（外部调用，如金币不足时）</summary>
        public void FlashGoldText()
        {
            if (goldText == null) return;
            _goldTextOriginalColor = goldText.color;
            _goldFlashTimer = 0.5f;
        }

        private void UpdateGoldFlash()
        {
            if (_goldFlashTimer <= 0f) return;
            _goldFlashTimer -= Time.deltaTime;
            if (_goldFlashTimer <= 0f)
            {
                if (goldText != null)
                    goldText.color = _goldTextOriginalColor;
                return;
            }
            if (goldText != null)
                goldText.color = (Mathf.FloorToInt(_goldFlashTimer * 20f) % 2 == 0)
                    ? Color.red : _goldTextOriginalColor;
        }

        private void UpdateGoldUI(float currentGold)
        {
            if (goldText != null)
                goldText.SetText("{0}", Mathf.FloorToInt(currentGold));
        }

        private void UpdateIncomeUI(float incomeRate)
        {
            if (incomeText != null)
                incomeText.text = $"每秒金币:{Mathf.FloorToInt(incomeRate)}";
        }

        private void Update()
        {
            _coreEconomy?.UpdateEconomy(Time.deltaTime);
            UpdateGoldFlash();
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (_coreEconomy == null || config == null) return;

            if (phase == GamePhase.SuddenDeath)
            {
                // 骤死期：回金速度乘以倍率
                _baseIncomeRate = _coreEconomy.IncomeRate;
                _coreEconomy.SetIncomeRate(_baseIncomeRate * config.suddenDeathMultiplier);
            }
            else if (phase == GamePhase.GameOver)
            {
                // 游戏结束：恢复基础回金速度（防止重开时残留倍率）
                _coreEconomy.SetIncomeRate(_baseIncomeRate);
            }
        }

        private void OnDestroy()
        {
            if (_coreEconomy != null)
            {
                _coreEconomy.OnGoldChanged -= UpdateGoldUI;
                _coreEconomy.OnIncomeChanged -= UpdateIncomeUI;
            }
            if (_stateMachine != null)
                _stateMachine.OnPhaseChanged -= OnPhaseChanged;
        }
    }
}
