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

        private CardUnit _owner;
        private Vector3 _initialScale;
        private float _initFillLocalX;
        private float _halfWorldWidth;
        private bool _initialized;
        private bool _metricsCached;

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
                fillRenderer.color = owner.IsLandlord == CardUnit.PlayerIsLandlord ? friendlyColor : enemyColor;

            OnHPChanged(0, owner.CurrentHP);
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
            ratio = Mathf.Clamp01(ratio);

            fillTransform.localScale = new Vector3(_initialScale.x * ratio, _initialScale.y, _initialScale.z);
            Vector3 pos = fillTransform.localPosition;
            fillTransform.localPosition = new Vector3(_initFillLocalX + _halfWorldWidth * (1f - ratio), pos.y, pos.z);
        }

        private void LateUpdate()
        {
            if (_owner == null || !_owner.IsAlive) return;
            transform.rotation = Quaternion.identity;
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
        }

        private void OnDestroy()
        {
            if (_owner != null)
                _owner.OnHPChanged -= OnHPChanged;
        }
    }
}
