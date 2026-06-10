using System;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Panels
{
    public class VictoryPanel : MonoBehaviour
    {
        [Header("面板引用")]
        [SerializeField] private GameObject panelRoot;

        [Header("结果展示")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("单人模式")]
        [SerializeField] private GameObject singlePlayerArea;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private GameObject firstWinBadge;

        [Header("联机模式")]
        [SerializeField] private GameObject multiplayerArea;
        [SerializeField] private TextMeshProUGUI settlementText;

        [Header("按钮 — 单人")]
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button returnToMenuButton;

        [Header("按钮 — 联机")]
        [SerializeField] private Button returnToRoomButton;
        [SerializeField] private Button returnToMenuButtonMp;

        public event Action OnNextLevelRequested;
        public event Action OnRestartRequested;
        public event Action OnReturnToMenuRequested;
        public event Action OnReturnToRoomRequested;

        private CanvasGroup _canvasGroup;
        private bool _hasShown;

        private void Awake()
        {
            _canvasGroup = EnsureCanvasGroup(panelRoot);
            HidePanel(_canvasGroup);
        }

        /// <summary>
        /// 每局游戏开始时调用，重置状态以便可以再次显示。
        /// 由 GameBootstrapper 在初始化时调用。
        /// </summary>
        public void ResetForNewGame()
        {
            _hasShown = false;
            if (_canvasGroup != null) HidePanel(_canvasGroup);
        }

        private void Start()
        {
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(RequestNextLevel);
            if (restartButton != null) restartButton.onClick.AddListener(RequestRestart);
            if (returnToMenuButton != null) returnToMenuButton.onClick.AddListener(RequestReturnToMenu);
            if (returnToRoomButton != null) returnToRoomButton.onClick.AddListener(RequestReturnToRoom);
            if (returnToMenuButtonMp != null) returnToMenuButtonMp.onClick.AddListener(RequestReturnToMenu);
        }

        public void Show(bool playerWon, bool isMultiplayer, VictoryStats stats)
        {
            if (_hasShown) return;
            _hasShown = true;

            // 设置标题
            if (titleText != null)
                titleText.text = playerWon ? "胜利!" : "失败!";

            // 设置副标题（对局时长）
            if (subtitleText != null)
            {
                int minutes = Mathf.FloorToInt(stats.gameDuration / 60f);
                int seconds = Mathf.FloorToInt(stats.gameDuration % 60f);
                subtitleText.text = $"对局时长: {minutes:00}:{seconds:00}";
            }

            // 切换单人/联机区域
            if (singlePlayerArea != null) singlePlayerArea.SetActive(!isMultiplayer);
            if (multiplayerArea != null) multiplayerArea.SetActive(isMultiplayer);

            // 切换按钮组
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(!isMultiplayer && playerWon && SceneLoader.HasNextLevel);
            if (restartButton != null) restartButton.gameObject.SetActive(!isMultiplayer);
            if (returnToMenuButton != null) returnToMenuButton.gameObject.SetActive(!isMultiplayer);
            if (returnToRoomButton != null) returnToRoomButton.gameObject.SetActive(isMultiplayer);
            if (returnToMenuButtonMp != null) returnToMenuButtonMp.gameObject.SetActive(isMultiplayer);

            if (!isMultiplayer)
                ShowSinglePlayerContent(playerWon, stats);
            else
                ShowMultiplayerContent(playerWon, stats);

            ShowPanel(_canvasGroup);
            Time.timeScale = 0f;
        }

        private void ShowSinglePlayerContent(bool playerWon, VictoryStats stats)
        {
            if (rewardText != null)
            {
                if (playerWon)
                {
                    int reward = CalculateSinglePlayerReward(stats);
                    rewardText.text = $"获得货币: +{reward}";
                }
                else
                {
                    rewardText.text = "再接再厉!";
                }
            }

            if (firstWinBadge != null)
                firstWinBadge.SetActive(playerWon && IsFirstWin());
        }

        private void ShowMultiplayerContent(bool playerWon, VictoryStats stats)
        {
            if (settlementText != null)
            {
                string identity = stats.identityBaseScore >= 100 ? "地主" : "农民";
                float settlement = stats.SettlementAmount;
                string resultTag = playerWon ? "胜" : "负";

                settlementText.text =
                    $"身份: {identity}  结果: {resultTag}\n" +
                    $"基础分: {stats.identityBaseScore}\n" +
                    $"叫分乘数: x{stats.bidMultiplier:F1}\n" +
                    $"局态系数: x{stats.gameStateCoefficient:F1}\n" +
                    $"最终结算: {settlement:F0}";
            }
        }

        private static int CalculateSinglePlayerReward(VictoryStats stats)
        {
            // 基础奖励 + 对局时长加成（预留商店系统）
            int baseReward = 100;
            int durationBonus = Mathf.FloorToInt(stats.gameDuration / 60f) * 10;
            return baseReward + durationBonus;
        }

        private static bool IsFirstWin()
        {
            return !SaveSystem.Data.HasFirstWin;
        }

        private void RequestNextLevel()
        {
            OnNextLevelRequested?.Invoke();
        }

        private void RequestRestart()
        {
            GameSession.Reset();
            OnRestartRequested?.Invoke();
        }

        private void RequestReturnToMenu()
        {
            GameSession.Reset();
            OnReturnToMenuRequested?.Invoke();
        }

        private void RequestReturnToRoom()
        {
            GameSession.Reset();
            OnReturnToRoomRequested?.Invoke();
        }

        // ── 辅助方法 ──

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (go == null) return null;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static void ShowPanel(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        private static void HidePanel(CanvasGroup cg)
        {
            if (cg == null) return;
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }
}
