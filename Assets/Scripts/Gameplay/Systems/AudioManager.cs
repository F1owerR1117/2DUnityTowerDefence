using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 场景-BGM 映射。
    /// </summary>
    [System.Serializable]
    public class SceneBGMPair
    {
        public string sceneName;
        public AudioClip music;
    }

    /// <summary>
    /// 音效管理器：统一管理所有游戏音效。
    /// 采用单例模式，跨场景持久化。
    /// 通过多 AudioSource 优先级通道确保关键音效不被挤掉。
    /// 支持按场景自动切换 BGM。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        #region 音频源配置

        [Header("音频源（优先级通道）")]
        [Tooltip("UI/关键音效通道（priority=0，最高优先级）")]
        [SerializeField] private AudioSource uiSource;

        [Tooltip("战斗高优先级音效通道（priority=64）")]
        [SerializeField] private AudioSource combatHighSource;

        [Tooltip("战斗普通音效通道（priority=128）")]
        [SerializeField] private AudioSource combatSource;

        [Tooltip("战斗低优先级音效通道（priority=200，攻击/技能）")]
        [SerializeField] private AudioSource combatLowSource;

        [Tooltip("背景音乐播放源")]
        [SerializeField] private AudioSource bgmSource;

        #endregion

        #region 音效剪辑配置

        [Header("-- 按钮音效 --")]
        [Tooltip("按钮点击音效")]
        [SerializeField] private AudioClip buttonClickClip;

        [Tooltip("按钮悬停音效")]
        [SerializeField] private AudioClip buttonHoverClip;

        [Tooltip("卡牌选中音效")]
        [SerializeField] private AudioClip cardSelectClip;

        [Tooltip("卡牌部署音效")]
        [SerializeField] private AudioClip cardDeployClip;

        [Tooltip("获得手牌（抽牌）音效")]
        [SerializeField] private AudioClip drawCardClip;

        [Header("-- 背景音乐 --")]
        [Tooltip("默认背景音乐（未匹配到场景时的回退）")]
        [SerializeField] private AudioClip bgmClip;

        [Tooltip("按场景自动切换 BGM 列表（优先于默认 bgmClip）")]
        [SerializeField] private SceneBGMPair[] sceneBGMPairs;

        [Header("-- 领域音效 --")]
        [Tooltip("要不起领域激活音效")]
        [SerializeField] private AudioClip domainActivateClip;

        [Tooltip("要不起领域关闭音效")]
        [SerializeField] private AudioClip domainDeactivateClip;

        [Tooltip("反制护盾激活音效")]
        [SerializeField] private AudioClip counterShieldClip;

        [Tooltip("要不起领域被破解音效（炸弹/反制击破）")]
        [SerializeField] private AudioClip domainBrokenClip;

        [Tooltip("反制护盾被破解音效（到时间自动解除不播放此音效）")]
        [SerializeField] private AudioClip counterShieldBrokenClip;

        #endregion

        #region 音量配置

        [Header("-- 音量设置 --")]
        [Tooltip("音效音量（0-1）")]
        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 1f;

        [Tooltip("背景音乐音量（0-1）")]
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.5f;

        #endregion

        #region 单例实例

        /// <summary>全局唯一实例</summary>
        private static AudioManager _instance;

        /// <summary>获取 AudioManager 实例</summary>
        public static AudioManager Instance => _instance;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 初始化优先级通道
            uiSource = CreateSource("UI", 0, false);
            combatHighSource = CreateSource("CombatHigh", 64, false);
            combatSource = CreateSource("Combat", 128, false);
            combatLowSource = CreateSource("CombatLow", 200, false);

            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
            }

            if (bgmClip != null)
            {
                PlayBGM(bgmClip);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayBGMForScene(scene.name);
        }

        private AudioSource CreateSource(string name, int priority, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.priority = priority;
            src.loop = loop;
            return src;
        }

        #endregion

        #region 公共方法 - 按钮音效（UI 通道）

        public void PlayButtonClick()
        {
            PlayUI(buttonClickClip);
        }

        public void PlayButtonHover()
        {
            PlayUI(buttonHoverClip);
        }

        public void PlayCardSelect()
        {
            PlayUI(cardSelectClip);
        }

        public void PlayCardDeploy()
        {
            PlayUI(cardDeployClip);
        }

        public void PlayDrawCard()
        {
            PlayUI(drawCardClip);
        }

        #endregion

        #region 公共方法 - 领域音效（UI 通道）

        public void PlayDomainActivate()
        {
            PlayUI(domainActivateClip);
        }

        public void PlayDomainDeactivate()
        {
            PlayUI(domainDeactivateClip);
        }

        public void PlayCounterShield()
        {
            PlayUI(counterShieldClip);
        }

        public void PlayDomainBroken()
        {
            PlayUI(domainBrokenClip);
        }

        public void PlayCounterShieldBroken()
        {
            PlayUI(counterShieldBrokenClip);
        }

        #endregion

        #region 公共方法 - 通用播放

        /// <summary>
        /// 通过 UI 通道播放（最高优先级，用于按钮/出牌/领域等关键音效）。
        /// </summary>
        public void PlayUI(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || uiSource == null) return;
            uiSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        /// <summary>
        /// 通过战斗高优先级通道播放（用于破封/护盾/死亡等重要战斗音效）。
        /// </summary>
        public void PlayCombatHigh(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || combatHighSource == null) return;
            combatHighSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        /// <summary>
        /// 通过战斗普通通道播放（用于受击等中等优先级音效）。
        /// </summary>
        public void PlayCombat(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || combatSource == null) return;
            combatSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        /// <summary>
        /// 通过战斗低优先级通道播放（用于攻击/技能等高频但不关键的音效）。
        /// 通道满时低优先级音效会被自动丢弃，确保 UI 音效不受影响。
        /// </summary>
        public void PlayCombatLow(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || combatLowSource == null) return;
            combatLowSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        /// <summary>
        /// 兼容旧接口：播放音效（走战斗普通通道）。
        /// 新代码请直接使用 PlayUI / PlayCombatHigh / PlayCombat / PlayCombatLow。
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            PlayCombat(clip, volumeScale);
        }

        /// <summary>
        /// 兼容旧接口：3D 位置音效（2D 游戏不推荐使用，改为走战斗普通通道）。
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            PlayCombat(clip, volumeScale);
        }

        #endregion

        #region 公共方法 - 背景音乐

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }

        /// <summary>
        /// 根据场景名查找并播放对应的 BGM。未匹配时使用默认 bgmClip。
        /// </summary>
        public void PlayBGMForScene(string sceneName)
        {
            if (sceneBGMPairs != null)
            {
                foreach (var pair in sceneBGMPairs)
                {
                    if (pair.sceneName == sceneName && pair.music != null)
                    {
                        PlayBGM(pair.music);
                        return;
                    }
                }
            }
            // 未匹配到场景，使用默认 BGM
            if (bgmClip != null)
                PlayBGM(bgmClip);
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        public void PauseBGM()
        {
            if (bgmSource != null) bgmSource.Pause();
        }

        public void ResumeBGM()
        {
            if (bgmSource != null) bgmSource.UnPause();
        }

        #endregion

        #region 公共方法 - 音量控制

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
        }

        public float GetSFXVolume() => sfxVolume;
        public float GetBGMVolume() => bgmVolume;

        public void MuteSFX(bool mute)
        {
            if (uiSource != null) uiSource.mute = mute;
            if (combatHighSource != null) combatHighSource.mute = mute;
            if (combatSource != null) combatSource.mute = mute;
            if (combatLowSource != null) combatLowSource.mute = mute;
        }

        public void MuteBGM(bool mute)
        {
            if (bgmSource != null) bgmSource.mute = mute;
        }

        #endregion
    }
}
