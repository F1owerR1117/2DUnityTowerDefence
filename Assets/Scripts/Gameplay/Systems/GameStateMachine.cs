using System;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 游戏阶段状态机。
    /// 控制整个对局的阶段流转：叫分 → 对局 → 骤死期 → 结算（§2.4a）。
    /// 阶段一直接从 Playing 开始（跳过 Bidding）。
    /// </summary>
    public enum GamePhase
    {
        /// <summary>叫分期（30秒，§1.1）— 阶段三实现</summary>
        Bidding,

        /// <summary>对局中（5分钟）</summary>
        Playing,

        /// <summary>骤死期（1分钟双倍金币）</summary>
        SuddenDeath,

        /// <summary>游戏结束</summary>
        GameOver
    }

    public class GameStateMachine : MonoBehaviour
    {
        [Header("时间配置")]
        [Tooltip("对局阶段时长（秒）")]
        [SerializeField] private float playingDuration = 300f;
        [Tooltip("骤死期时长（秒）")]
        [SerializeField] private float suddenDeathDuration = 60f;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Playing;

        /// <summary>对局开始时间（Time.time）</summary>
        public float GameStartTime { get; private set; }

        /// <summary>当前阶段开始时间</summary>
        private float _phaseStartTime;

        /// <summary>是否已被外部强制停止计时</summary>
        private bool _timerStopped;

        // ─── 公共属性 ───

        /// <summary>获取自对局开始经过的时间</summary>
        public float ElapsedTime => _timerStopped ? _elapsedWhenStopped : Time.time - GameStartTime;

        /// <summary>当前阶段剩余时间（秒），GameOver 时返回 0</summary>
        public float RemainingTime
        {
            get
            {
                if (CurrentPhase == GamePhase.GameOver) return 0f;
                float phaseDuration = CurrentPhase == GamePhase.SuddenDeath
                    ? suddenDeathDuration : playingDuration;
                float elapsed = Time.time - _phaseStartTime;
                return Mathf.Max(0f, phaseDuration - elapsed);
            }
        }

        /// <summary>当前阶段总时长</summary>
        public float CurrentPhaseDuration =>
            CurrentPhase == GamePhase.SuddenDeath ? suddenDeathDuration : playingDuration;

        /// <summary>对局阶段总时长（供 UI 读取）</summary>
        public float PlayingDuration => playingDuration;

        /// <summary>骤死期总时长（供 UI 读取）</summary>
        public float SuddenDeathDuration => suddenDeathDuration;

        private float _elapsedWhenStopped;

        // ─── 事件 ───

        /// <summary>阶段切换事件</summary>
        public event Action<GamePhase> OnPhaseChanged;

        /// <summary>时间耗尽事件（骤死期结束后触发）</summary>
        public event Action OnTimeUp;

        // ─── 生命周期 ───

        private void Start()
        {
            GameStartTime = Time.time;
            _phaseStartTime = Time.time;
        }

        private void Update()
        {
            if (_timerStopped) return;
            if (CurrentPhase == GamePhase.GameOver) return;

            float phaseElapsed = Time.time - _phaseStartTime;
            float phaseDuration = CurrentPhase == GamePhase.SuddenDeath
                ? suddenDeathDuration : playingDuration;

            if (phaseElapsed >= phaseDuration)
            {
                if (CurrentPhase == GamePhase.Playing)
                {
                    TransitionTo(GamePhase.SuddenDeath);
                }
                else if (CurrentPhase == GamePhase.SuddenDeath)
                {
                    TransitionTo(GamePhase.GameOver);
                    OnTimeUp?.Invoke();
                }
            }
        }

        // ─── 公共方法 ───

        /// <summary>
        /// 联机模式下同步游戏开始时间（由 NetworkGameManager 调用）。
        /// 直接设置本地时间轴上的游戏开始时间，NetworkGameManager 已完成网络时间到本地时间的映射。
        /// </summary>
        /// <param name="localStartTime">本地 Time.time 坐标系下的游戏开始时间</param>
        public void SyncGameStartTime(float localStartTime)
        {
            GameStartTime = localStartTime;
            _phaseStartTime = localStartTime;
            Debug.Log($"[GameStateMachine] 时间同步: localStartTime={localStartTime:F2}");
        }

        /// <summary>
        /// 切换到目标阶段
        /// </summary>
        public void TransitionTo(GamePhase newPhase)
        {
            if (CurrentPhase == newPhase) return;

            CurrentPhase = newPhase;
            _phaseStartTime = Time.time;
            OnPhaseChanged?.Invoke(newPhase);
        }

        /// <summary>
        /// 停止计时（游戏结束时调用，冻结 ElapsedTime）
        /// </summary>
        public void StopTimer()
        {
            if (_timerStopped) return;
            _timerStopped = true;
            _elapsedWhenStopped = Time.time - GameStartTime;
        }
    }
}
