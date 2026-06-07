using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Bidding
{
    /// <summary>
    /// 叫分期控制器。
    /// 管理叫分流程：倒计时、AI 叫分、玩家叫分、结果确定、场景跳转。
    /// 挂载到叫分场景 Canvas 上的 GameObject。
    /// </summary>
    public class BiddingManager : MonoBehaviour
    {
        [Header("计时与结果")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button confirmButton;

        [Header("玩家面板 — 自己（中间）")]
        [SerializeField] private TextMeshProUGUI playerLabelMe;
        [SerializeField] private TextMeshProUGUI bidDisplayMe;
        [SerializeField] private Image roleIconMe;

        [Header("玩家面板 — AI-1（左边）")]
        [SerializeField] private TextMeshProUGUI playerLabelAI1;
        [SerializeField] private TextMeshProUGUI bidDisplayAI1;
        [SerializeField] private Image roleIconAI1;

        [Header("玩家面板 — AI-2（右边）")]
        [SerializeField] private TextMeshProUGUI playerLabelAI2;
        [SerializeField] private TextMeshProUGUI bidDisplayAI2;
        [SerializeField] private Image roleIconAI2;

        [Header("叫分按钮")]
        [SerializeField] private Button bid1Button;
        [SerializeField] private Button bid2Button;
        [SerializeField] private Button bid3Button;
        [SerializeField] private Button passButton;

        [Header("配置")]
        [SerializeField] private BiddingConfig biddingConfig;

        // 叫分状态
        private int[] _bids = new int[3];
        private int _currentTurn;
        private int _highestBidder = -1;
        private int _highestBid;
        private bool _biddingEnded;
        private float _timer;

        // AI 延迟
        private float _aiDelay;
        private const float AI_DELAY_SECONDS = 1.2f;

        // 玩家标签和叫分显示的快捷访问（按逻辑索引：0=Me, 1=AI1, 2=AI2）
        private TextMeshProUGUI[] _labels;
        private TextMeshProUGUI[] _bidTexts;
        private Image[] _roleIcons;

        private void Start()
        {
            // 联机模式下由 BiddingSceneBootstrap 禁用本组件，跳过初始化
            if (!gameObject.activeInHierarchy) return;

            // 构建快捷数组
            _labels = new[] { playerLabelMe, playerLabelAI1, playerLabelAI2 };
            _bidTexts = new[] { bidDisplayMe, bidDisplayAI1, bidDisplayAI2 };
            _roleIcons = new[] { roleIconMe, roleIconAI1, roleIconAI2 };

            float duration = biddingConfig != null ? biddingConfig.biddingDuration : 30f;

            GameSession.Reset();
            _timer = duration;
            _currentTurn = 0;

            if (resultPanel != null) resultPanel.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            // 隐藏角色图标
            SetRoleIconsActive(false);

            SetPlayerLabels();
            UpdateBidButtons();
            UpdateTurnDisplay();
        }

        private void Update()
        {
            if (_biddingEnded) return;

            _timer -= Time.deltaTime;
            if (timerText != null)
            {
                int sec = Mathf.CeilToInt(Mathf.Max(0f, _timer));
                timerText.text = $"{sec}s";
            }

            if (_timer <= 0f)
            {
                EndBidding();
                return;
            }

            if (_currentTurn > 0)
            {
                _aiDelay -= Time.deltaTime;
                if (_aiDelay <= 0f)
                    ProcessAITurn();
            }
        }

        #region 玩家叫分

        public void OnBid1() => PlayerBid(1);
        public void OnBid2() => PlayerBid(2);
        public void OnBid3() => PlayerBid(3);
        public void OnPass() => PlayerBid(0);

        private void PlayerBid(int bid)
        {
            if (_biddingEnded || _currentTurn != 0) return;
            ProcessBid(0, bid);
        }

        #endregion

        #region AI 叫分

        private void ProcessAITurn()
        {
            if (_biddingEnded) return;
            int bid = AIDecideBid();
            ProcessBid(_currentTurn, bid);
        }

        private int AIDecideBid()
        {
            int minBid = _highestBid > 0 ? _highestBid + 1 : 1;
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (minBid > maxBid) return 0;

            float passChance = biddingConfig != null ? biddingConfig.aiPassChance : 0.6f;
            if (Random.value < passChance) return 0;

            float w1 = biddingConfig != null ? biddingConfig.aiBid1Weight : 0.5f;
            float w2 = biddingConfig != null ? biddingConfig.aiBid2Weight : 0.3f;
            float w3 = biddingConfig != null ? biddingConfig.aiBid3Weight : 0.2f;

            float total = 0f;
            if (minBid <= 1) total += w1;
            if (minBid <= 2) total += w2;
            if (minBid <= 3) total += w3;

            if (total <= 0f) return 0;

            float roll = Random.value * total;
            if (minBid <= 1) { roll -= w1; if (roll <= 0) return 1; }
            if (minBid <= 2) { roll -= w2; if (roll <= 0) return 2; }
            return maxBid;
        }

        #endregion

        #region 叫分处理

        private void ProcessBid(int playerIndex, int bid)
        {
            if (_bidTexts == null) return;
            _bids[playerIndex] = bid;

            if (_bidTexts[playerIndex] != null)
                _bidTexts[playerIndex].text = bid > 0 ? $"{bid} 分" : "不叫";

            if (bid > _highestBid)
            {
                _highestBid = bid;
                _highestBidder = playerIndex;
            }

            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;
            if (bid == maxBid)
            {
                EndBidding();
                return;
            }

            _currentTurn++;
            _aiDelay = AI_DELAY_SECONDS;
            if (_currentTurn >= 3)
            {
                if (_highestBidder >= 0)
                    EndBidding();
                else
                    _currentTurn = 0;
            }

            UpdateBidButtons();
            UpdateTurnDisplay();
        }

        #endregion

        #region 结束与跳转

        private void EndBidding()
        {
            if (_biddingEnded) return;
            _biddingEnded = true;

            SetButtonsInteractable(false);

            bool playerIsLandlord;
            float multiplier;

            if (_highestBidder >= 0)
            {
                playerIsLandlord = (_highestBidder == 0);
                multiplier = _highestBid;
            }
            else
            {
                bool randomAssign = biddingConfig != null ? biddingConfig.randomAssignOnTimeout : true;
                if (randomAssign)
                    _highestBidder = Random.Range(0, 3);
                else
                    _highestBidder = 0;

                playerIsLandlord = (_highestBidder == 0);
                multiplier = 1f;
            }

            int landlordIndex = 0;
            int[] farmerIndices = { 1, 2 };
            GameSession.SetResult(playerIsLandlord, multiplier, landlordIndex, farmerIndices);

            ShowResult(playerIsLandlord, multiplier);
            ShowRoleIcons();
        }

        private void ShowResult(bool playerIsLandlord, float multiplier)
        {
            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultText != null)
            {
                string role = playerIsLandlord ? "地主" : "农民";
                resultText.text = $"你是 {role}\n叫分倍数: x{multiplier:F0}";
            }
        }

        public void OnConfirm()
        {
            SceneLoader.LoadGame();
        }

        #endregion

        #region UI 更新

        private void SetPlayerLabels()
        {
            string[] names = { "你", "AI-1", "AI-2" };
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] != null)
                    _labels[i].text = names[i];
            }
        }

        private void UpdateBidButtons()
        {
            if (_biddingEnded || _currentTurn != 0)
            {
                SetButtonsInteractable(false);
                return;
            }

            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;
            if (bid1Button != null) bid1Button.interactable = _highestBid < 1 && maxBid >= 1;
            if (bid2Button != null) bid2Button.interactable = _highestBid < 2 && maxBid >= 2;
            if (bid3Button != null) bid3Button.interactable = _highestBid < 3 && maxBid >= 3;
            if (passButton != null) passButton.interactable = true;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (bid1Button != null) bid1Button.interactable = interactable;
            if (bid2Button != null) bid2Button.interactable = interactable;
            if (bid3Button != null) bid3Button.interactable = interactable;
            if (passButton != null) passButton.interactable = interactable;
        }

        private void UpdateTurnDisplay()
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] != null)
                    _labels[i].fontStyle = i == _currentTurn
                        ? FontStyles.Bold
                        : FontStyles.Normal;
            }
        }

        private void SetRoleIconsActive(bool active)
        {
            for (int i = 0; i < _roleIcons.Length; i++)
            {
                if (_roleIcons[i] != null)
                    _roleIcons[i].gameObject.SetActive(active);
            }
        }

        private void ShowRoleIcons()
        {
            for (int i = 0; i < _roleIcons.Length; i++)
            {
                if (_roleIcons[i] == null) continue;
                _roleIcons[i].gameObject.SetActive(true);
                // 地主用红色，农民用绿色（颜色可自行调整）
                bool isLandlord = (i == _highestBidder);
                _roleIcons[i].color = isLandlord ? Color.red : Color.green;
            }
        }

        #endregion
    }
}
