using System;
using System.Collections;
using System.Collections.Generic;
using DoudizhuTower.Config;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Bidding
{
    /// <summary>
    /// 联机叫分控制器。
    /// 3 个真人玩家轮流叫分，通过网络同步。
    /// Master 客户端负责轮次管理和结果判定。
    /// </summary>
    public class NetworkBiddingManager : MonoBehaviour
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

        [Header("玩家面板 — 左边")]
        [SerializeField] private TextMeshProUGUI playerLabelLeft;
        [SerializeField] private TextMeshProUGUI bidDisplayLeft;
        [SerializeField] private Image roleIconLeft;

        [Header("玩家面板 — 右边")]
        [SerializeField] private TextMeshProUGUI playerLabelRight;
        [SerializeField] private TextMeshProUGUI bidDisplayRight;
        [SerializeField] private Image roleIconRight;

        [Header("叫分按钮")]
        [SerializeField] private Button bid1Button;
        [SerializeField] private Button bid2Button;
        [SerializeField] private Button bid3Button;
        [SerializeField] private Button passButton;

        [Header("配置")]
        [SerializeField] private BiddingConfig biddingConfig;

        private INetworkService _net;
        private int _mySlot;
        private int[] _actorNumbers;
        private int _currentTurnSlot;
        private int[] _bids = new int[3];
        private int _highestBidder = -1;
        private int _highestBid;
        private bool _biddingEnded;
        private float _timer;
        private bool _initialized;

        // 左右两侧对应的网络槽位（-1 = 未分配）
        private int _leftSlot = -1;
        private int _rightSlot = -1;

        // AI 槽位
        private HashSet<int> _aiSlots = new HashSet<int>();
        private float _aiDelay;
        private const float AI_DELAY_SECONDS = 1.2f;

        private void Start()
        {
            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[NetworkBidding] NetworkManager 不存在");
                return;
            }

            _net = NetworkManager.Instance.Service;
            if (_net == null)
            {
                Debug.LogError("[NetworkBidding] 网络服务不可用");
                return;
            }

            // 加载原始 AI 槽位（延迟转换，等 PlayerList 同步后在 InitializeSlotWhenReady 中执行）
            _aiSlots = new HashSet<int>(GameSession.RawAISlots);

            // 等待 Photon PlayerList 同步完成后再计算槽位
            StartCoroutine(InitializeSlotWhenReady());

            float duration = biddingConfig != null ? biddingConfig.biddingDuration : 30f;
            _timer = duration;
            _currentTurnSlot = 0;

            if (resultPanel != null) resultPanel.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            if (bid1Button != null) bid1Button.onClick.AddListener(() => OnBid(1));
            if (bid2Button != null) bid2Button.onClick.AddListener(() => OnBid(2));
            if (bid3Button != null) bid3Button.onClick.AddListener(() => OnBid(3));
            if (passButton != null) passButton.onClick.AddListener(() => OnBid(0));

            // 隐藏角色图标
            SetRoleIconsActive(false);

            _net.OnCustomEvent += OnNetworkEvent;
            _net.OnPlayerLeft += OnPlayerLeft;
        }

        private IEnumerator InitializeSlotWhenReady()
        {
            int expectedPlayerCount = 3 - GameSession.RawAISlots.Count;

            // 等待 Photon PlayerList 同步（最多等 3 秒）
            float timeout = 3f;
            while (timeout > 0f)
            {
                var actors = _net.GetPlayerActorNumbers();
                if (actors.Length >= expectedPlayerCount)
                {
                    // PlayerList 已完整，计算槽位
                    _actorNumbers = new int[3];
                    for (int i = 0; i < 3; i++)
                        _actorNumbers[i] = i < actors.Length ? actors[i] : -1;
                    _mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, _actorNumbers);

                    // 在 PlayerList 同步后再转换 AI 槽位（避免 join 顺序导致的竞态）
                    ConvertAISlots();
                    break;
                }
                timeout -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            // 兜底：如果超时仍未同步，强制计算
            if (_mySlot < 0)
            {
                var actors = _net.GetPlayerActorNumbers();
                _actorNumbers = new int[3];
                for (int i = 0; i < 3; i++)
                    _actorNumbers[i] = i < actors.Length ? actors[i] : -1;
                _mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, _actorNumbers);
                ConvertAISlots();
                Debug.LogWarning($"[NetworkBidding] 槽位计算超时，强制 mySlot={_mySlot}");
            }

            Debug.Log($"[NetworkBidding] AI 槽位: [{string.Join(", ", _aiSlots)}], IsMaster={_net.IsMasterClient}, mySlot={_mySlot}, actorNumbers=[{string.Join(", ", _actorNumbers)}]");

            // 槽位确定后执行依赖槽位的初始化
            AssignSideSlots();
            SetPlayerLabels();
            _initialized = true;

            if (_net.IsMasterClient)
                _net.SendToAll(NetworkProtocol.BID_TURN, new object[] { 0 });

            UpdateBidButtons();
            UpdateTurnDisplay();

            Debug.Log($"[NetworkBidding] 初始化完成，本机槽位={_mySlot}, IsMaster={_net.IsMasterClient}");
        }

        /// <summary>
        /// 将大厅原始 AI 槽位（playerSlots[] 索引）转换为 actor-number 排序后的槽位索引。
        /// 必须在 PlayerList 同步完成后调用（_actorNumbers 已就绪）。
        /// </summary>
        private void ConvertAISlots()
        {
            var rawSlots = GameSession.RawAISlots;
            string[] names = _net.GetPlayerNames();

            // 找出所有真人玩家的 actor-number 排序槽位
            var realPlayerSlots = new HashSet<int>();
            int realIdx = 0;
            for (int pos = 0; pos < 3; pos++)
            {
                if (rawSlots.Contains(pos)) continue; // AI 位置，跳过
                if (realIdx < names.Length)
                {
                    int actorNum = _actorNumbers[realIdx];
                    int sortedIdx = NetworkProtocol.GetPlayerSlot(actorNum, _actorNumbers);
                    if (sortedIdx >= 0) realPlayerSlots.Add(sortedIdx);
                    realIdx++;
                }
            }

            // AI 槽位 = 全集 {0,1,2} - 真人玩家槽位
            _aiSlots = new HashSet<int>();
            for (int i = 0; i < 3; i++)
            {
                if (!realPlayerSlots.Contains(i))
                    _aiSlots.Add(i);
            }
            GameSession.AISlots = new HashSet<int>(_aiSlots);

            Debug.Log($"[NetworkBidding] AI 槽位转换: 原始=[{string.Join(", ", rawSlots)}] → 转换后=[{string.Join(", ", _aiSlots)}], 真人槽位=[{string.Join(", ", realPlayerSlots)}]");
        }

        private void OnDestroy()
        {
            if (_net != null)
            {
                _net.OnCustomEvent -= OnNetworkEvent;
                _net.OnPlayerLeft -= OnPlayerLeft;
            }
        }

        private void Update()
        {
            if (!_initialized || _biddingEnded) return;

            _timer -= Time.deltaTime;
            if (timerText != null)
            {
                int sec = Mathf.CeilToInt(Mathf.Max(0f, _timer));
                timerText.text = $"{sec}s";
            }

            if (_timer <= 0f && _net.IsMasterClient)
            {
                MasterEndBidding();
                return;
            }

            // AI 叫分（仅 Master 执行，带延迟）
            if (_net.IsMasterClient && _aiSlots.Contains(_currentTurnSlot))
            {
                _aiDelay -= Time.deltaTime;
                if (_aiDelay <= 0f)
                {
                    int bid = AIDecideBid();
                    Debug.Log($"[NetworkBidding] AI 槽位 {_currentTurnSlot} 叫分: {bid}");
                    var aiBidData = new object[] { _currentTurnSlot, bid };
                    _net.SendToAll(NetworkProtocol.BID_ACTION, aiBidData);

                    // Master 不会收到自己的 RaiseEvent，需直接处理
                    ProcessBid(_currentTurnSlot, bid);
                }
            }
        }

        // ─── 玩家叫分 ───

        private void OnBid(int bid)
        {
            if (_biddingEnded || _currentTurnSlot != _mySlot) return;
            _net.SendToMaster(NetworkProtocol.BID_ACTION, new object[] { bid });
            SetButtonsInteractable(false);
        }

        // ─── 网络事件处理 ───

        private void OnNetworkEvent(string key, object value, int senderActor)
        {
            switch (key)
            {
                case NetworkProtocol.BID_TURN:
                    HandleBidTurn((object[])value);
                    break;
                case NetworkProtocol.BID_ACTION:
                    if (_net.IsMasterClient)
                        HandleBidActionOnMaster((object[])value, senderActor);
                    else
                        HandleBidActionBroadcast((object[])value);
                    break;
                case NetworkProtocol.BID_RESULT:
                    HandleBidResult((object[])value);
                    break;
            }
        }

        private void HandleBidTurn(object[] data)
        {
            _currentTurnSlot = (int)data[0];
            _timer = biddingConfig != null ? biddingConfig.biddingDuration : 30f;
            _aiDelay = AI_DELAY_SECONDS;
            UpdateBidButtons();
            UpdateTurnDisplay();
        }

        private void HandleBidActionOnMaster(object[] data, int senderActor)
        {
            int senderSlot = NetworkProtocol.GetPlayerSlot(senderActor, _actorNumbers);
            if (senderSlot != _currentTurnSlot) return;

            int bid = (int)data[0];
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;
            if (bid < 0 || bid > maxBid) return;
            if (bid > 0 && bid <= _highestBid) return;

            _net.SendToAll(NetworkProtocol.BID_ACTION, new object[] { senderSlot, bid });

            // Master 不会收到自己的 RaiseEvent，需直接处理
            ProcessBid(senderSlot, bid);
        }

        private void HandleBidActionBroadcast(object[] data)
        {
            int slot = (int)data[0];
            int bid = (int)data[1];
            ProcessBid(slot, bid);
        }

        private void ProcessBid(int slot, int bid)
        {
            _bids[slot] = bid;

            var bidText = GetBidDisplayForSlot(slot);
            if (bidText != null)
                bidText.text = bid > 0 ? $"{bid} 分" : "不叫";

            if (bid > _highestBid)
            {
                _highestBid = bid;
                _highestBidder = slot;
            }

            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (bid == maxBid && _net.IsMasterClient)
            {
                MasterEndBidding();
                return;
            }

            if (_net.IsMasterClient)
            {
                int nextTurn = _currentTurnSlot + 1;
                if (nextTurn >= 3)
                {
                    if (_highestBidder >= 0)
                    {
                        MasterEndBidding();
                        return;
                    }
                    nextTurn = 0;
                }
                Debug.Log($"[NetworkBidding] 轮次推进: {_currentTurnSlot} → {nextTurn}, AI槽位=[{string.Join(", ", _aiSlots)}], isAI={_aiSlots.Contains(nextTurn)}");
                var turnData = new object[] { nextTurn };
                _net.SendToAll(NetworkProtocol.BID_TURN, turnData);

                // Master 不会收到自己的 RaiseEvent，需直接处理
                HandleBidTurn(turnData);
            }
        }

        // ─── 结束叫分（仅 Master）───

        private void MasterEndBidding()
        {
            Debug.Log($"[NetworkBidding] MasterEndBidding called, _highestBidder={_highestBidder}, _highestBid={_highestBid}");
            int landlordSlot;
            float multiplier;

            if (_highestBidder >= 0)
            {
                landlordSlot = _highestBidder;
                multiplier = _highestBid;
            }
            else
            {
                landlordSlot = UnityEngine.Random.Range(0, 3);
                multiplier = 1f;
            }

            // 基地映射约定：baseBuildings[0]=地主基地, [1]=农民A, [2]=农民B
            // baseMapping[playerSlot] = 该玩家操控的基地索引
            int[] baseMapping = new int[3];
            baseMapping[landlordSlot] = 0; // 地主玩家 → 地主基地(索引0)
            int farmerBaseIdx = 1;
            for (int i = 0; i < 3; i++)
            {
                if (i == landlordSlot) continue;
                baseMapping[i] = farmerBaseIdx++; // 农民玩家 → 农民基地(索引1,2)
            }

            int seed = Environment.TickCount;

            var resultData = new object[] { landlordSlot, baseMapping, multiplier, seed };
            _net.SendToAll(NetworkProtocol.BID_RESULT, resultData);

            // Master 不会收到自己的 RaiseEvent，需直接调用
            HandleBidResult(resultData);
        }

        // ─── 处理叫分结果（所有客户端）───

        private void HandleBidResult(object[] data)
        {
            Debug.Log($"[NetworkBidding] HandleBidResult called, _biddingEnded={_biddingEnded}");
            if (_biddingEnded) return;
            _biddingEnded = true;

            int landlordSlot = (int)data[0];
            int[] baseMapping = (int[])data[1];
            float multiplier = Convert.ToSingle(data[2]);
            int seed = (int)data[3];

            bool localIsLandlord = (landlordSlot == _mySlot);

            GameSession.IsNetworkMode = true;
            GameSession.NetworkSeed = seed;
            GameSession.SetResultNetwork(_mySlot, baseMapping, multiplier);
            GameSession.SetLocalPlayerIsLandlord(localIsLandlord);

            SetButtonsInteractable(false);
            ShowResult(localIsLandlord, multiplier);
            ShowRoleIcons(landlordSlot);

            // 激活确认按钮（场景中可能默认隐藏）
            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            }

            Debug.Log($"[NetworkBidding] 结果: 地主槽位={landlordSlot}, 本机={_mySlot}, " +
                      $"本机是地主={localIsLandlord}, 倍数={multiplier}, seed={seed}");
        }

        private void OnConfirm()
        {
            Debug.Log($"[NetworkBidding] OnConfirm: IsNetworkMode={GameSession.IsNetworkMode}, " +
                      $"NetworkSeed={GameSession.NetworkSeed}, MySlot={_mySlot}, " +
                      $"IsMaster={_net.IsMasterClient}");
            // 所有客户端都可触发场景切换（LoadScene 内部仅 Master 执行 LoadLevel，
            // AutomaticallySyncScene 会自动同步到其他客户端）
            _net.LoadScene(SceneLoader.GAME_SCENE);
        }

        private void OnPlayerLeft(string playerName)
        {
            if (_biddingEnded) return;

            int realPlayerCount = _net.CurrentPlayerCount;
            int totalSlots = realPlayerCount + _aiSlots.Count;

            if (totalSlots < 3)
            {
                _biddingEnded = true;
                SetButtonsInteractable(false);
                if (resultText != null)
                    resultText.text = "有玩家断线，叫分取消";
                if (resultPanel != null) resultPanel.SetActive(true);
                Debug.Log("[NetworkBidding] 玩家断线，叫分取消");
            }
        }

        // ─── 槽位分配 ───

        private void AssignSideSlots()
        {
            int side = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == _mySlot) continue;
                if (side == 0) _leftSlot = i;
                else _rightSlot = i;
                side++;
            }
        }

        // ─── UI 更新 ───

        private TextMeshProUGUI GetLabelForSlot(int slot)
        {
            if (slot == _mySlot) return playerLabelMe;
            if (slot == _leftSlot) return playerLabelLeft;
            if (slot == _rightSlot) return playerLabelRight;
            return null;
        }

        private TextMeshProUGUI GetBidDisplayForSlot(int slot)
        {
            if (slot == _mySlot) return bidDisplayMe;
            if (slot == _leftSlot) return bidDisplayLeft;
            if (slot == _rightSlot) return bidDisplayRight;
            return null;
        }

        private Image GetRoleIconForSlot(int slot)
        {
            if (slot == _mySlot) return roleIconMe;
            if (slot == _leftSlot) return roleIconLeft;
            if (slot == _rightSlot) return roleIconRight;
            return null;
        }

        private void SetPlayerLabels()
        {
            if (_net == null) return;
            string[] names = _net.GetPlayerNames();

            if (playerLabelMe != null)
            {
                if (_aiSlots.Contains(_mySlot))
                    playerLabelMe.text = "AI（你）";
                else
                    playerLabelMe.text = _mySlot < names.Length ? $"{names[_mySlot]}（你）" : "你";
            }

            if (playerLabelLeft != null && _leftSlot >= 0)
            {
                if (_aiSlots.Contains(_leftSlot))
                    playerLabelLeft.text = $"<color=#FFD700>AI-{_leftSlot + 1}</color>";
                else
                    playerLabelLeft.text = _leftSlot < names.Length ? names[_leftSlot] : "等待中...";
            }

            if (playerLabelRight != null && _rightSlot >= 0)
            {
                if (_aiSlots.Contains(_rightSlot))
                    playerLabelRight.text = $"<color=#FFD700>AI-{_rightSlot + 1}</color>";
                else
                    playerLabelRight.text = _rightSlot < names.Length ? names[_rightSlot] : "等待中...";
            }
        }

        private void UpdateBidButtons()
        {
            if (_biddingEnded)
            {
                SetButtonsInteractable(false);
                return;
            }

            bool isMyTurn = _currentTurnSlot == _mySlot && !_aiSlots.Contains(_currentTurnSlot);
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (bid1Button != null) bid1Button.interactable = isMyTurn && _highestBid < 1 && maxBid >= 1;
            if (bid2Button != null) bid2Button.interactable = isMyTurn && _highestBid < 2 && maxBid >= 2;
            if (bid3Button != null) bid3Button.interactable = isMyTurn && _highestBid < 3 && maxBid >= 3;
            if (passButton != null) passButton.interactable = isMyTurn;
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
            var labels = new[] {
                GetLabelForSlot(0),
                GetLabelForSlot(1),
                GetLabelForSlot(2)
            };

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null)
                    labels[i].fontStyle = i == _currentTurnSlot
                        ? FontStyles.Bold
                        : FontStyles.Normal;
            }
        }

        private void SetRoleIconsActive(bool active)
        {
            if (roleIconMe != null) roleIconMe.gameObject.SetActive(active);
            if (roleIconLeft != null) roleIconLeft.gameObject.SetActive(active);
            if (roleIconRight != null) roleIconRight.gameObject.SetActive(active);
        }

        private void ShowRoleIcons(int landlordSlot)
        {
            var icons = new[] {
                GetRoleIconForSlot(0),
                GetRoleIconForSlot(1),
                GetRoleIconForSlot(2)
            };

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                icons[i].gameObject.SetActive(true);
                icons[i].color = (i == landlordSlot) ? Color.red : Color.green;
            }
        }

        private void ShowResult(bool localIsLandlord, float multiplier)
        {
            Debug.Log($"[NetworkBidding] ShowResult called: localIsLandlord={localIsLandlord}, multiplier={multiplier}, resultPanel={resultPanel}, resultText={resultText}");
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                Debug.Log($"[NetworkBidding] resultPanel activated: {resultPanel.name}, active={resultPanel.activeSelf}");
            }
            else
            {
                Debug.LogError("[NetworkBidding] resultPanel is NULL! 请在 Inspector 中赋值");
            }
            if (resultText != null)
            {
                string role = localIsLandlord ? "地主" : "农民";
                resultText.text = $"你是 {role}\n叫分倍数: x{multiplier:F0}";
            }
        }

        // ─── AI 叫分逻辑 ───

        private int AIDecideBid()
        {
            int minBid = _highestBid > 0 ? _highestBid + 1 : 1;
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            if (minBid > maxBid) return 0;

            float passChance = biddingConfig != null ? biddingConfig.aiPassChance : 0.6f;
            if (UnityEngine.Random.value < passChance) return 0;

            float w1 = biddingConfig != null ? biddingConfig.aiBid1Weight : 0.5f;
            float w2 = biddingConfig != null ? biddingConfig.aiBid2Weight : 0.3f;
            float w3 = biddingConfig != null ? biddingConfig.aiBid3Weight : 0.2f;

            float total = 0f;
            if (minBid <= 1) total += w1;
            if (minBid <= 2) total += w2;
            if (minBid <= 3) total += w3;

            if (total <= 0f) return 0;

            float roll = UnityEngine.Random.value * total;
            if (minBid <= 1) { roll -= w1; if (roll <= 0) return 1; }
            if (minBid <= 2) { roll -= w2; if (roll <= 0) return 2; }
            return maxBid;
        }
    }
}
