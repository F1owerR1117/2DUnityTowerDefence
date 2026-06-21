using UnityEngine;
using UnityEditor;
using DoudizhuTower.Gameplay.Network;
using DoudizhuTower.Gameplay.Entities;
using DoudizhuTower.Gameplay.Battle;
using DoudizhuTower.Gameplay.Systems;
using DoudizhuTower.UI.HUD;
using DoudizhuTower.Core.Cards;
using DoudizhuTower.Core.Economy;
using System.Collections.Generic;

namespace DoudizhuTower.Editor
{
    /// <summary>
    /// 本地联机测试启动器。
    /// Tools → 本地联机测试 → 设置玩家数 → 启动。
    /// 单进程模拟多玩家，零网络延迟，可断点调试。
    /// </summary>
    public class LocalTestLauncher : EditorWindow
    {
        private int _playerCount = 3;
        private int _seed = 42;

        [MenuItem("Tools/本地联机测试")]
        public static void ShowWindow()
        {
            GetWindow<LocalTestLauncher>("本地联机测试");
        }

        private void OnGUI()
        {
            GUILayout.Label("本地联机测试", EditorStyles.boldLabel);
            GUILayout.Space(10);

            _playerCount = EditorGUILayout.IntSlider("玩家数量", _playerCount, 2, 4);
            _seed = EditorGUILayout.IntField("随机种子", _seed);

            GUILayout.Space(10);

            if (GUILayout.Button("启动测试", GUILayout.Height(30)))
                LaunchTest();

            if (GUILayout.Button("停止测试", GUILayout.Height(25)))
                StopTest();

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "启动后会在场景中创建 LocalTestRunner 对象，\n" +
                "为每个玩家创建独立的 LocalNetworkService。\n" +
                "用于测试网络层消息路由，不包含游戏逻辑模拟。",
                MessageType.Info);
        }

        private void LaunchTest()
        {
            StopTest();

            var runnerObj = new GameObject("[LocalTestRunner]");
            var runner = runnerObj.AddComponent<LocalTestRunner>();
            runner.PlayerCount = _playerCount;
            runner.Seed = _seed;
            runner.AutoStart = true;

            EditorApplication.isPlaying = true;
        }

        private void StopTest()
        {
            var existing = GameObject.Find("[LocalTestRunner]");
            if (existing != null) DestroyImmediate(existing);
            LocalNetworkHub.Clear();
        }
    }

    /// <summary>
    /// 本地联机测试运行器。在 Play Mode 中创建多个逻辑玩家。
    /// </summary>
    public class LocalTestRunner : MonoBehaviour
    {
        public int PlayerCount = 3;
        public int Seed = 42;
        public bool AutoStart = true;

        private readonly List<LocalNetworkService> _services = new();
        private bool _started;

        private void Start()
        {
            if (AutoStart) StartCoroutine(DelayedStart());
        }

        private System.Collections.IEnumerator DelayedStart()
        {
            // 等一帧，确保 GameBootstrapper.Start() 已完成（BattleManager 已初始化）
            yield return null;
            yield return null;
            StartTest();
        }

        public void StartTest()
        {
            if (_started) return;
            _started = true;

            LocalNetworkHub.Clear();

            // 查找场景中已有的组件
            var battleManagers = FindObjectsByType<BattleManager>(FindObjectsSortMode.InstanceID);
            var baseBuildings = FindObjectsByType<CardUnit>(FindObjectsSortMode.None);
            var buildingList = new List<Component>();
            foreach (var cu in baseBuildings)
                if (cu._isBuilding) buildingList.Add(cu);

            Debug.Log($"[LocalTest] 启动 {PlayerCount} 个玩家, seed={Seed}, 建筑={buildingList.Count}");

            for (int i = 0; i < PlayerCount; i++)
            {
                // 创建网络服务
                var service = new LocalNetworkService($"Player{i}");
                service.Initialize();
                _services.Add(service);

                // 创建独立牌堆和手牌
                var deck = new CardDeck(Seed);
                int handCap = (i == 0) ? 20 : 17; // slot 0 = 地主
                var hand = new CardHand(handCap);
                deck.Deal(7, hand);

                // 创建经济系统
                float incomeRate = 5f;
                if (i == 0) incomeRate += 2f; // 地主加成
                var economy = new EconomySystem(50f, incomeRate);

                Debug.Log($"[LocalTest] Player {i}: ActorNumber={service.LocalActorNumber}, " +
                          $"IsMaster={service.IsMasterClient}, Hand={hand.Count}, Gold={economy.CurrentGold}");
            }

            Debug.Log($"[LocalTest] 全部就绪。LocalNetworkService 已创建 {PlayerCount} 个玩家");
        }

        private void OnDestroy()
        {
            _services.Clear();
            LocalNetworkHub.Clear();
        }
    }
}
