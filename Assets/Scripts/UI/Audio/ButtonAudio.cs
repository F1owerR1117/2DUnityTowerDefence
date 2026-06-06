using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoudizhuTower.UI.Audio
{
    /// <summary>
    /// 按钮音效组件：自动为按钮添加点击和悬停音效。
    ///
    /// 使用方法：
    /// 1. 选中按钮 GameObject
    /// 2. Add Component → ButtonAudio
    /// 3. 完成！点击和悬停会自动播放音效
    ///
    /// 自定义音效：
    /// 在 Inspector 中拖入自定义音效剪辑，
    /// 如果为空则使用 AudioManager 的默认音效。
    ///
    /// 注意事项：
    /// - 需要场景中有 AudioManager 实例
    /// - 按钮禁用时不会播放音效
    /// - 支持自定义音效剪辑覆盖默认音效
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonAudio : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        #region 音效配置

        [Header("自定义音效")]
        [Tooltip("点击音效（为空时使用 AudioManager 默认音效）")]
        [SerializeField] private AudioClip clickClip;

        [Tooltip("悬停音效（为空时使用 AudioManager 默认音效）")]
        [SerializeField] private AudioClip hoverClip;

        [Tooltip("音量缩放（0-1）")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeScale = 1f;

        #endregion

        #region 私有字段

        /// <summary>按钮组件引用</summary>
        private Button _button;

        #endregion

        #region 生命周期

        private void Awake()
        {
            // 缓存按钮组件引用
            _button = GetComponent<Button>();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 按钮点击事件处理。
        /// 当玩家点击按钮时自动调用。
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            // 按钮禁用时不播放音效
            if (_button != null && !_button.interactable) return;

            var audio = Gameplay.Systems.AudioManager.Instance;
            if (audio == null) return;

            // 优先使用自定义音效，否则使用默认音效
            if (clickClip != null)
                audio.PlayUI(clickClip, volumeScale);
            else
                audio.PlayButtonClick();
        }

        /// <summary>
        /// 按钮悬停事件处理。
        /// 当鼠标悬停到按钮上时自动调用。
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // 按钮禁用时不播放音效
            if (_button != null && !_button.interactable) return;

            var audio = Gameplay.Systems.AudioManager.Instance;
            if (audio == null) return;

            // 优先使用自定义音效，否则使用默认音效
            if (hoverClip != null)
                audio.PlayUI(hoverClip, volumeScale);
            else
                audio.PlayButtonHover();
        }

        #endregion
    }
}
