using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;

namespace DoudizhuTower.UI.HUD
{
    /// <summary>
    /// 对局计时 UI。
    /// 显示游戏已运行时间（正计时），骤死期变色提示。
    /// 由 GameBootstrapper 自动焊接，或自动查找 GameStateMachine。
    /// </summary>
    public class GameTimerUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("颜色配置")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color suddenDeathColor = new Color(1f, 0.3f, 0.3f);

        private GameStateMachine _stateMachine;
        private bool _subscribed;

        /// <summary>
        /// 由 GameBootstrapper 调用，注入状态机引用。
        /// </summary>
        public void Initialize(GameStateMachine stateMachine)
        {
            Unsubscribe();
            _stateMachine = stateMachine;
            Subscribe();
        }

        private void OnEnable()
        {
            // 自动查找（兜底，防止 Bootstrapper 未调用 Initialize）
            if (_stateMachine == null)
                _stateMachine = FindFirstObjectByType<GameStateMachine>();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (timerText == null) return;

            // 兜底查找
            if (_stateMachine == null)
            {
                _stateMachine = FindFirstObjectByType<GameStateMachine>();
                if (_stateMachine == null) return;
                Subscribe();
            }

            float elapsed = _stateMachine.ElapsedTime;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";

            // 骤死期变色
            if (_stateMachine.CurrentPhase == GamePhase.SuddenDeath)
            {
                timerText.color = suddenDeathColor;
            }
            else if (_stateMachine.CurrentPhase != GamePhase.GameOver)
            {
                timerText.color = normalColor;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _stateMachine == null) return;
            _stateMachine.OnPhaseChanged += OnPhaseChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _stateMachine == null) return;
            _stateMachine.OnPhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (timerText == null) return;

            if (phase == GamePhase.SuddenDeath)
            {
                timerText.color = suddenDeathColor;
            }
            else if (phase == GamePhase.GameOver)
            {
                timerText.color = normalColor;
            }
        }
    }
}
