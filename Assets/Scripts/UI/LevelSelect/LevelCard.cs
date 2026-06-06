using DoudizhuTower.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoudizhuTower.UI.LevelSelect
{
    /// <summary>
    /// 关卡卡片组件。
    /// 显示关卡缩略图、名称、描述、难度。
    /// 由 LevelSelectController 管理缩放和选中状态。
    /// </summary>
    public class LevelCard : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private GameObject lockOverlay;

        [Header("缩放配置")]
        [Tooltip("中心位置的缩放")]
        [SerializeField] private float maxScale = 1f;
        [Tooltip("边缘位置的缩放")]
        [SerializeField] private float minScale = 0.6f;

        private LevelConfig _config;
        private RectTransform _rectTransform;
        private Vector3 _baseScale;

        public LevelConfig Config => _config;
        public bool IsUnlocked => _config != null && _config.isUnlocked;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _baseScale = transform.localScale;
        }

        /// <summary>初始化卡片数据</summary>
        public void Setup(LevelConfig config)
        {
            _config = config;
            if (config == null) return;

            if (nameText != null)
                nameText.text = config.levelName;

            if (descText != null)
                descText.text = config.description;

            if (difficultyText != null)
            {
                string stars = "";
                for (int i = 0; i < config.difficulty; i++) stars += "★";
                difficultyText.text = stars;
            }

            if (thumbnailImage != null)
                thumbnailImage.sprite = config.thumbnail;

            if (lockOverlay != null)
                lockOverlay.SetActive(!config.isUnlocked);
        }

        /// <summary>
        /// 根据距离中心的归一化距离更新缩放。
        /// distance = 0 表示在中心，distance = 1 表示在边缘。
        /// </summary>
        public void UpdateScale(float normalizedDistance)
        {
            float t = Mathf.Clamp01(normalizedDistance);
            float scale = Mathf.Lerp(maxScale, minScale, t);
            transform.localScale = _baseScale * scale;
        }
    }
}
