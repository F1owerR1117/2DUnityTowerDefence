using DoudizhuTower.Core.Battle;
using TMPro;
using UnityEngine;

namespace DoudizhuTower.UI.Floating
{
    /// <summary>
    /// 伤害飘字组件。固定在 World Space Canvas 下，
    /// 向上浮动 + 渐隐后自动回池。
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageFloatText : MonoBehaviour
    {
        [Header("动画参数")]
        [SerializeField] private float floatHeight = 1.5f;
        [SerializeField] private float duration = 1f;
        [SerializeField] private float critThreshold = 50f;

        private TextMeshPro _text;
        private float _elapsed;
        private Vector3 _startPos;
        private System.Action _onComplete;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            _startPos = transform.position;
        }

        /// <summary>
        /// 显示伤害数字
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="worldPos">世界坐标</param>
        /// <param name="type">伤害类型</param>
        /// <param name="onComplete">动画结束后回调（回池）</param>
        public void Show(float damage, Vector3 worldPos, DamageType type, System.Action onComplete)
        {
            _elapsed = 0f;
            _startPos = worldPos;
            transform.position = worldPos;
            _onComplete = onComplete;

            _text.text = Mathf.RoundToInt(damage).ToString();

            // 颜色规则：大伤害红色加粗，物理白色，特殊紫色，真实伤害橙色
            bool isCrit = damage >= critThreshold;
            _text.color = type switch
            {
                DamageType.True => new Color(1f, 0.5f, 0f),
                DamageType.Special => new Color(0.7f, 0.3f, 1f),
                _ => isCrit ? Color.red : Color.white
            };
            _text.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;
            _text.fontSize = isCrit ? 120f : 100f;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / duration);

            // 向上浮动
            Vector3 pos = _startPos;
            pos.y += t * floatHeight;
            transform.position = pos;

            // 渐隐（前 20% 不透明，后 80% 逐渐消失）
            float alpha = t < 0.2f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.2f) / 0.8f);
            _text.alpha = alpha;

            if (t >= 1f)
            {
                var cb = _onComplete;
                _onComplete = null;
                gameObject.SetActive(false);
                cb?.Invoke();
            }
        }
    }
}
