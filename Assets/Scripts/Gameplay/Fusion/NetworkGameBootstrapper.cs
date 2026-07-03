using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.UI.Battlefield;
using DoudizhuTower.UI.Hand;
using DoudizhuTower.UI.HUD;
using DoudizhuTower.UI.Panels;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 联机模式游戏场景初始化器。
    /// 从 GameBootstrapper 分离所有 Fusion 相关逻辑，
    /// 由 BiddingSceneBootstrap 在联机模式下激活。
    /// </summary>
    public class NetworkGameBootstrapper : MonoBehaviour
    {
        [Header("手牌")]
        [SerializeField] private HandArea handArea;
        [SerializeField] private CardCounterUI cardCounter;

        [Header("战斗")]
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private GameStateMachine gameStateMachine;
        [SerializeField] private TimerQueue timerQueue;

        [Header("UI")]
        [SerializeField] private LaunchTubeUI launchTubeUI;
        [SerializeField] private TempSlotUI tempSlotUI;
        [SerializeField] private TempSlotUI teammateTempSlotUI;
        [SerializeField] private UnityEngine.UI.Button drawButton;
        [SerializeField] private DomainSystem domainSystem;
        [SerializeField] private DomainUIController domainUIController;
        [SerializeField] private UnityEngine.UI.Button domainButton;
        [SerializeField] private PauseMenu pauseMenu;
        [SerializeField] private VictoryPanel victoryPanel;

        [Header("基地")]
        [SerializeField] private Component[] baseBuildings;

        private FusionGameManager _gm;
        private CardDeck _mainDeck;
        private CardHand _playerHand;
        private bool _playerIsLandlord;
        private int _mySlot;

        public CardDeck MainDeck => _mainDeck;
        public CardHand PlayerHand => _playerHand;
        public bool IsLandlord => _playerIsLandlord;

        private void Awake()
        {
            if (GameSession.HasResult)
            {
                _playerIsLandlord = GameSession.PlayerIsLandlord;
            }
        }

        private IEnumerator Start()
        {
            yield return UIManager.WaitForReady();

            _gm = FusionGameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[NetworkGameBootstrapper] FusionGameManager.Instance 为空");
                yield break;
            }

            _mySlot = _gm.GetLocalSlot();
            if (_mySlot < 0) _mySlot = 0;

            _playerIsLandlord = GameSession.PlayerIsLandlord;
            CardUnit.PlayerIsLandlord = _playerIsLandlord;

            // 发牌
            int seed = GameSession.NetworkSeed;
            _mainDeck = new CardDeck(seed);
            int handCapacity = _playerIsLandlord ? 20 : 17;
            _playerHand = new CardHand(handCapacity);

            bool gotCards = false;
            var handCards = _gm.GetHandCards(_mySlot);
            if (handCards != null && handCards.Count > 0)
            {
                foreach (var deckIndex in handCards)
                {
                    var card = _mainDeck.GetCardByIndex(deckIndex);
                    if (card.DeckIndex >= 0) _playerHand.Add(card);
                }
                gotCards = true;
            }

            if (!gotCards)
            {
                var freshDeck = new CardDeck(seed);
                freshDeck.Deal(_mySlot * 7, new CardHand(handCapacity));
                freshDeck.Deal(7, _playerHand);
            }

            // 手牌 UI
            int maxPlay = _playerIsLandlord ? 6 : 5;
            var playerBase = GetPlayerBase();
            var routeGroup = playerBase?.GetComponent<RouteGroup>();
            handArea.Initialize(_playerHand, maxPlay, routeGroup);
            handArea.SetDeck(_mainDeck);
            if (cardCounter != null) { cardCounter.SetDeck(_mainDeck); cardCounter.Refresh(); }

            // 摸牌 RPC
            SetupDrawButton();

            // 领域按钮
            SetupDomainButton();

            // 暂停菜单
            SetupPauseMenu();

            // 胜利面板
            SetupVictoryPanel();

            // GameSceneSync 已由 GameBootstrapper 添加

            Debug.Log($"[NetworkGameBootstrapper] 初始化完成: slot={_mySlot}, landlord={_playerIsLandlord}, handCount={_playerHand.Count}");
        }

        private void SetupDrawButton()
        {
            if (drawButton == null) return;
            float drawInterval = _playerIsLandlord ? 5f : 6f;

            // 自动摸牌
            timerQueue?.ScheduleLoop(drawInterval, () =>
            {
                if (_playerHand == null || _playerHand.IsFull || _mainDeck == null) return;
                if (_gm != null) _gm.RpcDrawCard(_mySlot);
            });

            // 手动摸牌按钮
            drawButton.onClick.AddListener(() =>
            {
                if (_playerHand.IsFull) { handArea?.ShowHandFullFeedback(); return; }
                if (_gm != null) _gm.RpcDrawCard(_mySlot);
            });
        }

        private void SetupDomainButton()
        {
            if (domainButton == null) return;
            domainButton.gameObject.SetActive(_playerIsLandlord);
            domainButton.onClick.AddListener(() =>
            {
                if (_gm != null)
                {
                    _gm.ActivateDomain(_mySlot, 1);
                    Debug.Log($"[DomainButton] Fusion 领域激活: slot={_mySlot}");
                }
            });

            if (domainUIController == null)
                domainUIController = FindFirstObjectByType<DomainUIController>();
            if (domainUIController != null)
            {
                domainUIController.Initialize(domainSystem, _playerIsLandlord);
            }
        }

        private void SetupPauseMenu()
        {
            if (pauseMenu == null) pauseMenu = UIManager.Instance?.PauseMenu;
            if (pauseMenu != null)
            {
                pauseMenu.SetMultiplayerMode(true);
                pauseMenu.OnQuitRequested += OnQuitToLobby;
            }
        }

        private void SetupVictoryPanel()
        {
            if (victoryPanel == null) victoryPanel = UIManager.Instance?.VictoryPanel;
            victoryPanel?.ResetForNewGame();
            PauseMenu.IsGameOver = false;

            if (victoryPanel != null)
            {
                victoryPanel.OnReturnToMenuRequested += SceneLoader.LoadMainMenu;
            }
        }

        private Component GetPlayerBase()
        {
            if (baseBuildings != null && _mySlot >= 0 && _mySlot < baseBuildings.Length)
                return baseBuildings[_mySlot];
            return null;
        }

        private void OnQuitToLobby()
        {
            NetworkFacade.LeaveRoom();
            SceneLoader.LoadOnlineLobby();
        }

        private void OnDestroy()
        {
            if (pauseMenu != null)
                pauseMenu.OnQuitRequested -= OnQuitToLobby;
        }
    }
}
