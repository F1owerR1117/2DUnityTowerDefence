using System;
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
        [Header("UI 引用（与 BiddingManager 共用同一套 UI）")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI[] playerLabels;
        [SerializeField] private TextMeshProUGUI[] bidDisplays;
        [SerializeField] private Button bid1Button;
        [SerializeField] private Button bid2Button;
        [SerializeField] private Button bid3Button;
        [SerializeField] private Button passButton;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button confirmButton;

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

            _actorNumbers = _net.GetPlayerActorNumbers();
            _mySlot = NetworkProtocol.GetPlayerSlot(_net.LocalActorNumber, _actorNumbers);

            float duration = biddingConfig != null ? biddingConfig.biddingDuration : 30f;
            _timer = duration;
            _currentTurnSlot = 0;

            if (resultPanel != null) resultPanel.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            // 按钮绑定
            if (bid1Button != null) bid1Button.onClick.AddListener(() => OnBid(1));
            if (bid2Button != null) bid2Button.onClick.AddListener(() => OnBid(2));
            if (bid3Button != null) bid3Button.onClick.AddListener(() => OnBid(3));
            if (passButton != null) passButton.onClick.AddListener(() => OnBid(0));

            // 显示玩家名
            SetPlayerLabels();

            // 订阅网络事件
            _net.OnCustomEvent += OnNetworkEvent;
            _net.OnPlayerLeft += OnPlayerLeft;

            _initialized = true;

            // Master 初始化第一轮
            if (_net.IsMasterClient)
            {
                _net.SendToAll(NetworkProtocol.BID_TURN, new object[] { 0 });
            }

            UpdateBidButtons();
            UpdateTurnDisplay();

            Debug.Log($"[NetworkBidding] 初始化完成，本机槽位={_mySlot}, IsMaster={_net.IsMasterClient}");
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

            // Master 负责超时判定
            if (_timer <= 0f && _net.IsMasterClient)
            {
                MasterEndBidding();
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
            UpdateBidButtons();
            UpdateTurnDisplay();
        }

        // Master 收到叫分请求，校验后广播
        private void HandleBidActionOnMaster(object[] data, int senderActor)
        {
            int senderSlot = NetworkProtocol.GetPlayerSlot(senderActor, _actorNumbers);
            if (senderSlot != _currentTurnSlot) return;

            int bid = (int)data[0];

            // 校验叫分合法性
            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;
            if (bid < 0 || bid > maxBid) return;
            if (bid > 0 && bid <= _highestBid) return;

            // 广播给所有客户端
            _net.SendToAll(NetworkProtocol.BID_ACTION, new object[] { senderSlot, bid });
        }

        // 所有客户端处理叫分广播
        private void HandleBidActionBroadcast(object[] data)
        {
            int slot = (int)data[0];
            int bid = (int)data[1];
            ProcessBid(slot, bid);
        }

        // Master 处理叫分并推进轮次
        private void ProcessBid(int slot, int bid)
        {
            _bids[slot] = bid;

            if (bidDisplays != null && slot < bidDisplays.Length && bidDisplays[slot] != null)
                bidDisplays[slot].text = bid > 0 ? $"{bid} 分" : "不叫";

            if (bid > _highestBid)
            {
                _highestBid = bid;
                _highestBidder = slot;
            }

            int maxBid = biddingConfig != null ? biddingConfig.maxBid : 3;

            // 叫了最高分直接结束
            if (bid == maxBid && _net.IsMasterClient)
            {
                MasterEndBidding();
                return;
            }

            // Master 推进轮次
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
                _net.SendToAll(NetworkProtocol.BID_TURN, new object[] { nextTurn });
            }
        }

        // ─── 结束叫分（仅 Master）───

        private void MasterEndBidding()
        {
            if (_biddingEnded) return;
            _biddingEnded = true;

            int landlordSlot;
            float multiplier;

            if (_highestBidder >= 0)
            {
                landlordSlot = _highestBidder;
                multiplier = _highestBid;
            }
            else
            {
                // 无人叫分，随机指定地主
                landlordSlot = UnityEngine.Random.Range(0, 3);
                multiplier = 1f;
            }

            // 构建基地映射：地主→基地索引2(LandLord)，农民→基地索引0,1(FarmerA/FarmerB)
            int[] baseMapping = new int[3];
            baseMapping[landlordSlot] = 2; // LandLord base index
            int farmerIdx = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == landlordSlot) continue;
                baseMapping[i] = farmerIdx++;
            }

            int seed = Environment.TickCount;

            _net.SendToAll(NetworkProtocol.BID_RESULT, new object[] {
                landlordSlot, baseMapping, multiplier, seed
            });
        }

        // ─── 处理叫分结果（所有客户端）───

        private void HandleBidResult(object[] data)
        {
            if (_biddingEnded && _highestBidder >= 0) return; // 已经处理过
            _biddingEnded = true;

            int landlordSlot = (int)data[0];
            int[] baseMapping = (int[])data[1];
            float multiplier = Convert.ToSingle(data[2]);
            int seed = (int)data[3];

            bool localIsLandlord = (landlordSlot == _mySlot);

            // 设置 GameSession 联机数据
            GameSession.IsNetworkMode = true;
            GameSession.NetworkSeed = seed;
            GameSession.SetResultNetwork(_mySlot, baseMapping, multiplier);
            GameSession.SetLocalPlayerIsLandlord(localIsLandlord);

            SetButtonsInteractable(false);
            ShowResult(localIsLandlord, multiplier);

            Debug.Log($"[NetworkBidding] 结果: 地主槽位={landlordSlot}, 本机={_mySlot}, " +
                      $"本机是地主={localIsLandlord}, 倍数={multiplier}, seed={seed}");
        }

        // ─── 确认后跳转 ───

        private void OnConfirm()
        {
            if (_net.IsMasterClient)
                _net.LoadScene(SceneLoader.GAME_SCENE);
        }

        // ─── 玩家断线 ───

        private void OnPlayerLeft(string playerName)
        {
            if (_biddingEnded) return;

            // 人数不足，取消叫分
            if (_net.CurrentPlayerCount < 3)
            {
                _biddingEnded = true;
                SetButtonsInteractable(false);
                if (resultText != null)
                    resultText.text = "有玩家断线，叫分取消";
                if (resultPanel != null) resultPanel.SetActive(true);
                Debug.Log("[NetworkBidding] 玩家断线，叫分取消");
            }
        }

        // ─── UI 更新 ───

        private void SetPlayerLabels()
        {
            if (playerLabels == null || _net == null) return;
            string[] names = _net.GetPlayerNames();
            for (int i = 0; i < playerLabels.Length; i++)
            {
                if (playerLabels[i] == null) continue;
                if (i < names.Length)
                    playerLabels[i].text = i == _mySlot ? $"{names[i]}（你）" : names[i];
                else
                    playerLabels[i].text = "等待中...";
            }
        }

        private void UpdateBidButtons()
        {
            if (_biddingEnded)
            {
                SetButtonsInteractable(false);
                return;
            }

            bool isMyTurn = _currentTurnSlot == _mySlot;
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
            if (playerLabels == null) return;
            for (int i = 0; i < playerLabels.Length; i++)
            {
                if (playerLabels[i] == null) continue;
                playerLabels[i].fontStyle = i == _currentTurnSlot
                    ? FontStyles.Bold
                    : FontStyles.Normal;
            }
        }

        private void ShowResult(bool localIsLandlord, float multiplier)
        {
            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultText != null)
            {
                string role = localIsLandlord ? "地主" : "农民";
                resultText.text = $"你是 {role}\n叫分倍数: x{multiplier:F0}";
            }
        }
    }
}
