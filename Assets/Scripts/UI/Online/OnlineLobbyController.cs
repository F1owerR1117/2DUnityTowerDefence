using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Online
{
    /// <summary>
    /// 联机大厅控制器。
    /// 通过 INetworkService 接口与网络层交互，不直接依赖 Photon。
    /// </summary>
    public class OnlineLobbyController : MonoBehaviour
    {
        [Header("面板")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject roomPanel;
        [SerializeField] private GameObject matchmakingPanel;

        [Header("大厅面板")]
        [SerializeField] private Button soloQueueButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button backToMenuButton;

        [Header("房间面板")]
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI[] playerSlots;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;
        [SerializeField] private TextMeshProUGUI roomStatusText;

        [Header("匹配面板")]
        [SerializeField] private TextMeshProUGUI matchmakingStatusText;
        [SerializeField] private TextMeshProUGUI matchmakingTimerText;
        [SerializeField] private Button cancelMatchButton;

        private INetworkService _net;
        private bool _isReady;
        private bool _isMatching;
        private float _matchTimeout;
        private const float MATCH_TIMEOUT = 30f;

        private void Start()
        {
            // 获取网络服务
            Debug.Log($"[OnlineLobby] NetworkManager.Instance = {NetworkManager.Instance}");
            if (NetworkManager.Instance != null)
            {
                _net = NetworkManager.Instance.Service;
                Debug.Log($"[OnlineLobby] Service = {_net}, IsConnected = {_net?.IsConnected}");
            }

            ShowLobby();

            // 大厅按钮
            if (soloQueueButton != null) soloQueueButton.onClick.AddListener(OnSoloQueue);
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoom);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoom);
            if (backToMenuButton != null) backToMenuButton.onClick.AddListener(OnBackToMenu);

            // 房间按钮
            if (readyButton != null) readyButton.onClick.AddListener(OnToggleReady);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGame);
            if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoom);

            // 匹配按钮
            if (cancelMatchButton != null) cancelMatchButton.onClick.AddListener(OnCancelMatch);

            // 订阅网络事件
            if (_net != null)
            {
                _net.OnServerConnected += OnServerConnected;
                _net.OnRoomJoinSuccess += OnRoomJoinSuccess;
                _net.OnRoomJoinError += OnRoomJoinError;
                _net.OnPlayerJoined += OnPlayerJoined;
                _net.OnPlayerLeft += OnPlayerLeft;
                _net.OnAllPlayersReady += OnAllPlayersReady;
                _net.OnConnectionLost += OnConnectionLost;

                // 连接服务器
                _net.Connect();
            }
            else
            {
                Debug.LogWarning("[OnlineLobby] NetworkManager 未找到，请确保场景中有 NetworkManager");
            }
        }

        private void OnDestroy()
        {
            if (_net != null)
            {
                _net.OnServerConnected -= OnServerConnected;
                _net.OnRoomJoinSuccess -= OnRoomJoinSuccess;
                _net.OnRoomJoinError -= OnRoomJoinError;
                _net.OnPlayerJoined -= OnPlayerJoined;
                _net.OnPlayerLeft -= OnPlayerLeft;
                _net.OnAllPlayersReady -= OnAllPlayersReady;
                _net.OnConnectionLost -= OnConnectionLost;
            }
        }

        private void Update()
        {
            if (!_isMatching) return;

            _matchTimeout -= Time.deltaTime;
            if (matchmakingTimerText != null)
            {
                int sec = Mathf.CeilToInt(Mathf.Max(0f, _matchTimeout));
                matchmakingTimerText.text = $"{sec}s";
            }

            if (_net != null && matchmakingStatusText != null)
                matchmakingStatusText.text = $"正在匹配... ({_net.CurrentPlayerCount}/3)";

            if (_matchTimeout <= 0f)
            {
                _isMatching = false;
                if (_net != null) _net.LeaveRoom();
                ShowLobby();
                Debug.Log("[OnlineLobby] 匹配超时");
            }
        }

        #region 面板切换

        private void ShowLobby()
        {
            SetPanelActive(lobbyPanel);
            _isMatching = false;
            UpdateLobbyButtons();
        }

        private void ShowRoom()
        {
            SetPanelActive(roomPanel);
            _isReady = false;
            UpdateRoomUI();
        }

        private void ShowMatchmaking()
        {
            SetPanelActive(matchmakingPanel);
            _isMatching = true;
            _matchTimeout = MATCH_TIMEOUT;
            if (matchmakingStatusText != null)
                matchmakingStatusText.text = "正在匹配... (1/3)";
        }

        private void SetPanelActive(GameObject activePanel)
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(activePanel == lobbyPanel);
            if (roomPanel != null) roomPanel.SetActive(activePanel == roomPanel);
            if (matchmakingPanel != null) matchmakingPanel.SetActive(activePanel == matchmakingPanel);
        }

        #endregion

        #region 大厅操作

        private void UpdateLobbyButtons()
        {
            bool connected = _net != null && _net.IsConnected;
            if (soloQueueButton != null) soloQueueButton.interactable = connected;
            if (createRoomButton != null) createRoomButton.interactable = connected;
            if (joinRoomButton != null) joinRoomButton.interactable = connected;
        }

        private void OnSoloQueue()
        {
            if (_net == null) return;
            _net.JoinRandomRoom();
            ShowMatchmaking();
        }

        private void OnCreateRoom()
        {
            if (_net == null) return;
            string code = GenerateRoomCode();
            _net.CreateRoom(code, 3);
        }

        private void OnJoinRoom()
        {
            if (_net == null) return;
            string code = roomCodeInput != null ? roomCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                Debug.Log("[OnlineLobby] 请输入房间号");
                return;
            }
            _net.JoinRoom(code);
        }

        private void OnBackToMenu()
        {
            if (_net != null && _net.IsInRoom)
                _net.LeaveRoom();
            if (_net != null)
                _net.Disconnect();
            SceneLoader.LoadMainMenu();
        }

        #endregion

        #region 房间操作

        private void UpdateRoomUI()
        {
            if (_net == null) return;

            string[] names = _net.GetPlayerNames();

            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] == null) continue;
                if (i < names.Length)
                    playerSlots[i].text = names[i];
                else
                    playerSlots[i].text = "<color=#888888>等待中...</color>";
            }

            if (roomCodeText != null)
                roomCodeText.text = $"房间号: {_net.CurrentRoomName}";

            if (readyButton != null)
            {
                var text = readyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.text = _isReady ? "取消准备" : "准备";
            }

            if (startGameButton != null)
                startGameButton.interactable = _net.IsMasterClient && _net.CurrentPlayerCount >= 3;

            if (roomStatusText != null)
            {
                if (_net.CurrentPlayerCount < 3)
                    roomStatusText.text = $"等待玩家加入... ({_net.CurrentPlayerCount}/3)";
                else
                    roomStatusText.text = "所有人已就绪，可以开始";
            }
        }

        private void OnToggleReady()
        {
            if (_net == null) return;
            _isReady = !_isReady;
            _net.SetPlayerReady(_isReady);
            UpdateRoomUI();
        }

        private void OnStartGame()
        {
            if (_net == null || !_net.IsMasterClient) return;
            if (_net.CurrentPlayerCount < 3) return;

            // 房主同步跳转到叫分场景
            GameSession.Reset();
            _net.LoadScene(SceneLoader.BIDDING_SCENE);
        }

        private void OnLeaveRoom()
        {
            if (_net != null && _net.IsInRoom)
                _net.LeaveRoom();
            _isReady = false;
            ShowLobby();
        }

        #endregion

        #region 匹配操作

        private void OnCancelMatch()
        {
            _isMatching = false;
            if (_net != null && _net.IsInRoom)
                _net.LeaveRoom();
            ShowLobby();
        }

        #endregion

        #region 网络回调

        private void OnServerConnected()
        {
            UpdateLobbyButtons();
            Debug.Log("[OnlineLobby] 已连接到服务器");
        }

        private void OnConnectionLost()
        {
            _isMatching = false;
            ShowLobby();
            Debug.Log("[OnlineLobby] 已断开连接");
        }

        private void OnRoomJoinSuccess(string roomName)
        {
            _isMatching = false;
            if (roomCodeText != null)
                roomCodeText.text = $"房间号: {roomName}";
            ShowRoom();
        }

        private void OnRoomJoinError(string message)
        {
            _isMatching = false;
            ShowLobby();
            Debug.LogWarning($"[OnlineLobby] 加入房间失败: {message}");
        }

        private void OnPlayerJoined(string playerName)
        {
            UpdateRoomUI();
        }

        private void OnPlayerLeft(string playerName)
        {
            UpdateRoomUI();
        }

        private void OnAllPlayersReady()
        {
            UpdateRoomUI();
        }

        #endregion

        private string GenerateRoomCode()
        {
            return Random.Range(100000, 999999).ToString();
        }
    }
}
