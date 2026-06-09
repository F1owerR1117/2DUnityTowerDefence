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
        [Tooltip("飘字向上浮动的总高度（世界坐标单位）")]
        [SerializeField] private float floatHeight = 1.5f;

        [Tooltip("动画总时长（秒），从出现到完全消失")]
        [SerializeField] private float duration = 1f;

        [Tooltip("暴击阈值：伤害 ≥ 此值时变红加粗加大")]
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

            // ── 颜色规则 ──
            // 真实伤害 → 橙色
            // 特殊伤害 → 紫色
            // 物理/炸弹/燃烧 → 暴击红色，否则白色
            bool isCrit = damage >= critThreshold;
            _text.color = type switch
            {
                DamageType.True => new Color(1f, 0.5f, 0f),       // 真实伤害：橙色
                DamageType.Special => new Color(0.7f, 0.3f, 1f),  // 特殊伤害：紫色
                _ => isCrit ? Color.red : Color.white              // 其他：暴击红 / 普通白
            };
            _text.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;  // 暴击加粗
            _text.fontSize = isCrit ? 120f : 100f;                            // 暴击字号更大

            gameObject.SetActive(true);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / duration); // 0→1 归一化进度

            // ── 向上浮动 ──
            // 从起始位置线性向上移动 floatHeight 距离
            Vector3 pos = _startPos;
            pos.y += t * floatHeight;
            transform.position = pos;

            // ── 渐隐 ──
            // 前 20% 时间：完全不透明（让玩家看清数字）
            // 后 80% 时间：从不透明渐变到透明
            float alpha = t < 0.2f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.2f) / 0.8f);
            _text.alpha = alpha;

            // ── 动画结束 → 隐藏并回调对象池回收 ──
            if (t >= 1f)
            {
                var cb = _onComplete;
                _onComplete = null;
                gameObject.SetActive(false);
                cb?.Invoke(); // 通知 FloatingTextPool 回收
            }
        }
    }
}
