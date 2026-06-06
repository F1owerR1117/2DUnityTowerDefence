using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI
{
    /// <summary>
    /// 全屏闪白/闪红等屏幕反馈效果。
    /// 挂载到场景中的 Canvas Image 上，通过 ShowEffect 触发。
    /// </summary>
    public class ScreenEffect : MonoBehaviour
    {
        [Tooltip("效果 Image（需填满屏幕）")]
        [SerializeField] private Image effectImage;

        [Tooltip("效果显示时的透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float effectAlpha = 0.5f;

        [Tooltip("淡出时长（秒）")]
        [SerializeField] private float fadeOutDuration = 0.5f;

        private Coroutine _activeRoutine;

        private void Awake()
        {
            if (effectImage == null)
                effectImage = GetComponent<Image>();

            HideImmediate();
        }

        /// <summary>
        /// 显示屏幕闪烁效果。holdDuration 为保持时间，之后淡出。
        /// </summary>
        public void ShowEffect(float holdDuration = 0.1f)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            effectImage.enabled = true;
            _activeRoutine = StartCoroutine(FlashRoutine(holdDuration));
        }

        /// <summary>
        /// 以指定颜色和透明度显示效果。
        /// </summary>
        public void ShowEffect(Color color, float alpha, float holdDuration = 0.1f)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            effectImage.enabled = true;
            effectImage.color = new Color(color.r, color.g, color.b, alpha);
            _activeRoutine = StartCoroutine(FadeOutRoutine(holdDuration));
        }

        /// <summary>
        /// 立即隐藏效果。
        /// </summary>
        public void HideImmediate()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            if (effectImage != null)
            {
                Color c = effectImage.color;
                c.a = 0f;
                effectImage.color = c;
                effectImage.enabled = false;
            }
        }

        private IEnumerator FlashRoutine(float holdDuration)
        {
            Color c = effectImage.color;
            c.a = effectAlpha;
            effectImage.color = c;

            yield return new WaitForSecondsRealtime(holdDuration);

            yield return FadeOutRoutine(0f);
        }

        private IEnumerator FadeOutRoutine(float holdDuration)
        {
            if (holdDuration > 0f)
                yield return new WaitForSecondsRealtime(holdDuration);

            float elapsed = 0f;
            float startAlpha = effectImage.color.a;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                Color c = effectImage.color;
                c.a = a;
                effectImage.color = c;
                yield return null;
            }

            Color final = effectImage.color;
            final.a = 0f;
            effectImage.color = final;
            effectImage.enabled = false;
            _activeRoutine = null;
        }
    }
}
