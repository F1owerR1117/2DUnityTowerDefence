using DoudizhuTower.Gameplay.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI
{
    /// <summary>
    /// 主菜单控制器。
    /// 管理所有主菜单按钮和设置面板。
    /// 挂载到 MainMenu 场景 Canvas 上的 GameObject。
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("模式按钮")]
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiplayerButton;

        [Header("功能按钮")]
        [SerializeField] private Button shopButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button settingsBackButton;

        private void Start()
        {
            // 模式按钮
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(OnSinglePlayer);
            if (multiplayerButton != null)
                multiplayerButton.onClick.AddListener(OnMultiplayer);

            // 功能按钮
            if (shopButton != null)
            {
                shopButton.onClick.AddListener(OnShop);
                shopButton.interactable = false; // 暂未实现
            }
            if (collectionButton != null)
            {
                collectionButton.onClick.AddListener(OnCollection);
                collectionButton.interactable = false; // 暂未实现
            }
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettings);
            if (quitButton != null)
                quitButton.onClick.AddListener(SceneLoader.QuitGame);

            // 设置面板
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            if (volumeSlider != null)
            {
                volumeSlider.value = AudioListener.volume;
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
            }
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(OnSettingsBack);
        }

        // ─── 模式按钮 ───

        private void OnSinglePlayer()
        {
            SceneLoader.LoadLevelSelect();
        }

        private void OnMultiplayer()
        {
            SceneLoader.LoadOnlineLobby();
        }

        // ─── 功能按钮 ───

        private void OnShop()
        {
            // TODO: 商店场景实现后启用
            Debug.Log("[MainMenu] 商店功能暂未实现");
        }

        private void OnCollection()
        {
            // TODO: 图鉴场景实现后启用
            Debug.Log("[MainMenu] 图鉴功能暂未实现");
        }

        // ─── 设置面板 ───

        private void OnSettings()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(true);
        }

        private void OnSettingsBack()
        {
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }

        private void OnVolumeChanged(float value)
        {
            AudioListener.volume = value;
        }

        private void OnFullscreenToggleChanged(bool isFullscreen)
        {
            if (isFullscreen)
            {
                var maxRes = Screen.currentResolution;
                Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }
    }
}
