using UnityEngine;
using UnityEngine.SceneManagement;
using DoudizhuTower.UI;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 统一场景切换 API。UI_Scene 始终保留在内存中不被卸载。
    /// 有 SceneFader 时使用淡入淡出过渡，否则直接切换。
    /// </summary>
    public static class SceneLoader
    {
        public const string MAIN_MENU_SCENE = "MainMenu";
        public const string LEVEL_SELECT_SCENE = "LevelSelect";
        public const string ONLINE_LOBBY_SCENE = "OnlineLobby";
        public const string BIDDING_SCENE = "Bidding";
        public const string GAME_SCENE = "DoudizhuTower_Game";
        public const string CODEX_SCENE = "Codex";

        // ── 关卡跟踪 ──
        public static int CurrentLevelIndex { get; private set; } = -1;
        public static string[] LevelSceneNames { get; set; } = System.Array.Empty<string>();
        public static bool HasNextLevel => CurrentLevelIndex >= 0 && CurrentLevelIndex + 1 < LevelSceneNames.Length;

        public static void SetCurrentLevel(int index) => CurrentLevelIndex = index;

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(MAIN_MENU_SCENE))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{MAIN_MENU_SCENE}' 未添加到 Build Settings，请通过 File → Build Settings 添加");
                return;
            }
            LoadSceneWithFade(MAIN_MENU_SCENE);
        }

        public static void LoadLevelSelect()
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(LEVEL_SELECT_SCENE))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{LEVEL_SELECT_SCENE}' 未添加到 Build Settings");
                return;
            }
            LoadSceneWithFade(LEVEL_SELECT_SCENE);
        }

        /// <summary>通用场景加载（供关卡选择等外部调用）</summary>
        public static void LoadScene(string sceneName)
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{sceneName}' 未添加到 Build Settings");
                return;
            }
            LoadSceneWithFade(sceneName);
        }

        public static void LoadOnlineLobby()
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(ONLINE_LOBBY_SCENE))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{ONLINE_LOBBY_SCENE}' 未添加到 Build Settings");
                return;
            }
            LoadSceneWithFade(ONLINE_LOBBY_SCENE);
        }

        public static void LoadBidding()
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(BIDDING_SCENE))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{BIDDING_SCENE}' 未添加到 Build Settings");
                return;
            }
            LoadSceneWithFade(BIDDING_SCENE);
        }

        public static void LoadGame()
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(GAME_SCENE))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{GAME_SCENE}' 未添加到 Build Settings");
                return;
            }
            LoadSceneWithFade(GAME_SCENE);
        }

        public static void LoadCodex()
        {
            Time.timeScale = 1f;
            if (!IsSceneInBuild(CODEX_SCENE))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{CODEX_SCENE}' 未添加到 Build Settings");
                return;
            }
            LoadSceneWithFade(CODEX_SCENE);
        }

        public static void RestartGame()
        {
            Time.timeScale = 1f;
            LoadSceneWithFade(SceneManager.GetActiveScene().name);
        }

        public static void LoadNextLevel()
        {
            int next = CurrentLevelIndex + 1;
            if (next < LevelSceneNames.Length)
            {
                CurrentLevelIndex = next;
                LoadScene(LevelSceneNames[next]);
            }
            else
            {
                LoadLevelSelect();
            }
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void LoadSceneWithFade(string sceneName)
        {
            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeOutAndLoad(() => SceneManager.LoadScene(sceneName));
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        private static bool IsSceneInBuild(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
    }
}
