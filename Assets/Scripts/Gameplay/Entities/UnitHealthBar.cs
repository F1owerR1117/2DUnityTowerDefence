using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    public class UnitHealthBar : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private Transform fillTransform;
        [SerializeField] private SpriteRenderer fillRenderer;

        [Header("颜色")]
        [SerializeField] private Color friendlyColor = Color.green;
        [SerializeField] private Color enemyColor = Color.red;

        [Header("平滑动画")]
        [Tooltip("HP 条过渡速度（越大越快）")]
        [SerializeField] private float smoothSpeed = 8f;
        [Tooltip("受击闪烁持续时间（秒）")]
        [SerializeField] private float hitFlashDuration = 0.15f;
        [Tooltip("受击闪烁颜色")]
        [SerializeField] private Color hitFlashColor = Color.white;

        private CardUnit _owner;
        private Vector3 _initialScale;
        private float _initFillLocalX;
        private float _halfWorldWidth;
        private bool _initialized;
        private bool _metricsCached;

        // 平滑动画状态
        private float _currentRatio;
        private float _targetRatio;
        private float _hitFlashTimer;
        private Color _originalColor;
        private bool _skipFirstHPChange;  // 跳过初始化后的第一次 HP 变化（防止闪烁）

        private void CacheFillMetrics()
        {
            if (_metricsCached || fillTransform == null) return;
            _metricsCached = true;
            _initialScale = fillTransform.localScale;
            _initFillLocalX = fillTransform.localPosition.x;
            if (fillRenderer != null && fillRenderer.sprite != null)
            {
                float nativeW = fillRenderer.sprite.rect.width / fillRenderer.sprite.pixelsPerUnit;
                _halfWorldWidth = nativeW * _initialScale.x * 0.5f;
            }
            else
                _halfWorldWidth = _initialScale.x * 0.5f;
        }

        /// <summary>
        /// 统一初始化入口。工厂路径（UnitFactory.Spawn）和预置兵种路径（CardUnit.Start）都调用此方法。
        /// </summary>
        public void Initialize(CardUnit owner)
        {
            if (_initialized || owner == null) return;
            _initialized = true;
            _owner = owner;

            CacheFillMetrics();  // 兼容 Awake 未执行（血条子物体默认 inactive）的情况

            _owner.OnHPChanged += OnHPChanged;

            if (fillRenderer != null)
            {
                _originalColor = owner.IsLandlord == CardUnit.PlayerIsLandlord ? friendlyColor : enemyColor;
                fillRenderer.color = _originalColor;
            }

            // 初始化 HP 比例（不触发闪烁）
            float ratio = _owner.Stats.HP > 0f ? _owner.CurrentHP / _owner.Stats.HP : 0f;
            _currentRatio = Mathf.Clamp01(ratio);
            _targetRatio = _currentRatio;
            UpdateHPBarVisual(_currentRatio);

            // 跳过初始化后的第一次 HP 变化（防止闪烁）
            _skipFirstHPChange = true;
        }

        private void Start()
        {
            // 兜底：CardUnit.Start 未激活血条时的最后防线
            if (!_initialized)
            {
                var parent = GetComponentInParent<CardUnit>();
                if (parent != null)
                    Initialize(parent);
            }
        }

        private void OnHPChanged(int unitId, float currentHP)
        {
            if (_owner == null || fillTransform == null) return;
            float ratio = _owner.Stats.HP > 0f ? currentHP / _owner.Stats.HP : 0f;
            _targetRatio = Mathf.Clamp01(ratio);

            // 跳过初始化后的第一次 HP 变化（防止闪烁）
            if (_skipFirstHPChange)
            {
                _currentRatio = _targetRatio;
                UpdateHPBarVisual(_currentRatio);
                _skipFirstHPChange = false;
                return;
            }

            // 触发受击闪烁（仅在实际 HP 变化时）
            _hitFlashTimer = hitFlashDuration;
        }

        private void LateUpdate()
        {
            if (_owner == null || !_owner.IsAlive) return;
            transform.rotation = Quaternion.identity;

            // 平滑过渡 HP 条
            if (!Mathf.Approximately(_currentRatio, _targetRatio))
            {
                _currentRatio = Mathf.Lerp(_currentRatio, _targetRatio, Time.deltaTime * smoothSpeed);
                UpdateHPBarVisual(_currentRatio);
            }

            // 受击闪烁
            if (_hitFlashTimer > 0f && fillRenderer != null)
            {
                _hitFlashTimer -= Time.deltaTime;
                fillRenderer.color = (_hitFlashTimer > 0f) ? hitFlashColor : _originalColor;
            }
        }

        private void UpdateHPBarVisual(float ratio)
        {
            if (fillTransform == null) return;

            // 右对齐：HP 减少时从右往左扣减
            fillTransform.localScale = new Vector3(_initialScale.x * ratio, _initialScale.y, _initialScale.z);
            Vector3 pos = fillTransform.localPosition;
            fillTransform.localPosition = new Vector3(_initFillLocalX - _halfWorldWidth * (1f - ratio), pos.y, pos.z);
        }

        private void OnDisable()
        {
            // 对象池回收时重置状态，防止下次复用 _initialized 锁死初始化
            if (_owner != null)
            {
                _owner.OnHPChanged -= OnHPChanged;
                _owner = null;
            }
            _initialized = false;
            _currentRatio = 0f;
            _targetRatio = 0f;
            _hitFlashTimer = 0f;
        }

        private void OnDestroy()
        {
            if (_owner != null)
                _owner.OnHPChanged -= OnHPChanged;
        }
    }
}
