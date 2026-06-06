using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoudizhuTower.UI
{
    /// <summary>
    /// 场景切换淡入淡出过渡效果。
    /// 单例模式，跨场景持久化。需要在场景中预先放置一个带黑色 Image 的 Canvas。
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        private static SceneFader _instance;

        [Tooltip("黑色遮罩 Image")]
        [SerializeField] private Image fadeImage;

        [Tooltip("淡入淡出曲线")]
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("过渡时长（秒）")]
        [SerializeField] private float duration = 1f;

        public static SceneFader Instance => _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                SetAlpha(1f);
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            StartCoroutine(FadeIn());
        }

        private void OnDestroy()
        {
            if (_instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 新场景加载后自动淡入
            StopAllCoroutines();
            SetAlpha(1f);
            StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 淡出后执行回调（通常用于加载场景）。
        /// </summary>
        public void FadeOutAndLoad(Action onFaded)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOutRoutine(onFaded));
        }

        private IEnumerator FadeIn()
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime / duration;
                float a = curve.Evaluate(Mathf.Clamp01(t));
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(0f);
        }

        private IEnumerator FadeOutRoutine(Action onComplete)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float a = curve.Evaluate(Mathf.Clamp01(t));
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(1f);
            onComplete?.Invoke();
        }

        private void SetAlpha(float alpha)
        {
            if (fadeImage == null) return;
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
        }
    }
}
