using UnityEngine;

namespace DoudizhuTower.UI.Dialogue
{
    /// <summary>
    /// 对话数据 ScriptableObject。
    /// 每关/每段剧情创建一个资产，定义对话序列。
    /// 通过菜单 DoudizhuTower/DialogueData 创建。
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "DoudizhuTower/DialogueData")]
    public class DialogueData : ScriptableObject
    {
        [Tooltip("对话序列")]
        public DialogueLine[] lines;
    }

    /// <summary>
    /// 单条对话数据。
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("说话人名称")]
        public string speakerName;

        [Tooltip("角色立绘（留空则显示占位符）")]
        public Sprite portrait;

        [Tooltip("立绘显示宽度（Unity 单位）")]
        public float portraitWidth = 4f;

        [Tooltip("立绘显示高度（Unity 单位）")]
        public float portraitHeight = 6f;

        [Tooltip("立绘水平偏移（相对于对话框左侧）")]
        public float portraitOffsetX = -2.5f;

        [Tooltip("立绘垂直偏移（相对于对话框底部）")]
        public float portraitOffsetY = 0f;

        [Tooltip("对话内容")]
        [TextArea(3, 6)]
        public string content;

        [Tooltip("打字速度（秒/字，0 = 使用默认值）")]
        public float typeSpeed = 0.03f;

        [Tooltip("说话人名称颜色")]
        public Color speakerColor = new Color(0.914f, 0.271f, 0.376f); // #E94560

        /// <summary>获取打字速度（兜底默认值）</summary>
        public float GetTypeSpeed() => typeSpeed > 0f ? typeSpeed : 0.03f;
    }
}
