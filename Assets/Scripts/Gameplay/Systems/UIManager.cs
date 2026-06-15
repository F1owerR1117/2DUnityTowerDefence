using System.Collections;
using DoudizhuTower.UI.Panels;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 跨场景 UI 管理器（单例 + DontDestroyOnLoad）。
    /// 管理 UI_Scene 的加载，持有 PauseMenu 和 VictoryPanel 引用。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private PauseMenu _pauseMenu;
        private VictoryPanel _victoryPanel;

        public PauseMenu PauseMenu
        {
            get
            {
                if (_pauseMenu == null) _pauseMenu = FindFirstObjectByType<PauseMenu>();
                return _pauseMenu;
            }
        }

        public VictoryPanel VictoryPanel
        {
            get
            {
                if (_victoryPanel == null) _victoryPanel = FindFirstObjectByType<VictoryPanel>();
                return _victoryPanel;
            }
        }

        private const string UI_SCENE_NAME = "UI_Scene";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CleanupDuplicateEventSystems();
            CleanupSceneDuplicates();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupDuplicateEventSystems();
            CleanupSceneDuplicates();
        }

        /// <summary>
        /// 检查 UI_Scene 是否实际存在于内存中。
        /// </summary>
        private static bool IsUISceneLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == UI_SCENE_NAME)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 确保 UI_Scene 已加载。如果已加载返回 null，否则返回加载操作。
        /// </summary>
        private static AsyncOperation EnsureSceneLoaded()
        {
            if (IsUISceneLoaded()) return null;
            return SceneManager.LoadSceneAsync(UI_SCENE_NAME, LoadSceneMode.Additive);
        }

        /// <summary>
        /// GameBootstrapper 协程调用：等待 UI_Scene 就绪。
        /// 每次都验证场景是否真正在内存中，如果被卸载则重新加载。
        /// </summary>
        public static IEnumerator WaitForReady()
        {
            bool sceneLoaded = IsUISceneLoaded();
            Debug.Log($"[UIManager] WaitForReady: sceneLoaded={sceneLoaded}, Instance={Instance != null}");

            // 场景已加载且 Instance 有效 → 直接返回
            if (sceneLoaded && Instance != null) yield break;

            // 场景被卸载了 → 重新加载
            var op = EnsureSceneLoaded();
            Debug.Log($"[UIManager] WaitForReady: EnsureSceneLoaded returned {(op != null ? "AsyncOp" : "null")}");
            if (op != null) yield return op;

            // 等一帧让 UIManager.Awake / PauseMenu.Awake 执行
            yield return null;

            Debug.Log($"[UIManager] WaitForReady: after wait, sceneLoaded={IsUISceneLoaded()}, Instance={Instance != null}");

            // 超时保护：最多等 10 秒
            float timeout = 10f;
            while (Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (Instance == null)
                Debug.LogError("[UIManager] UI_Scene 加载超时，UIManager.Instance 仍为 null。跳过等待继续初始化。");
        }

        private void CleanupDuplicateEventSystems()
        {
            var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (all.Length <= 1) return;
            for (int i = 1; i < all.Length; i++)
                Destroy(all[i].gameObject);
        }

        /// <summary>
        /// 销毁场景中残留的 PauseMenu/VictoryPanel 副本。
        /// 使用 InstanceID 排序，确保保留最老的（DDOL）实例。
        /// </summary>
        private void CleanupSceneDuplicates()
        {
            KeepOldestAndDestroyDuplicates<PauseMenu>(ref _pauseMenu);
            KeepOldestAndDestroyDuplicates<VictoryPanel>(ref _victoryPanel);
        }

        private void KeepOldestAndDestroyDuplicates<T>(ref T cached) where T : MonoBehaviour
        {
            var all = FindObjectsByType<T>(FindObjectsSortMode.None);
            if (all.Length <= 1)
            {
                if (all.Length == 1) cached = all[0];
                return;
            }

            // 按 InstanceID 排序，最小的 = 创建最早的 = DDOL 实例
            T oldest = all[0];
            for (int i = 1; i < all.Length; i++)
            {
                if (all[i].GetInstanceID() < oldest.GetInstanceID())
                {
                    Destroy(oldest.gameObject);
                    oldest = all[i];
                }
                else
                {
                    Destroy(all[i].gameObject);
                }
            }
            cached = oldest;
        }
    }
}
