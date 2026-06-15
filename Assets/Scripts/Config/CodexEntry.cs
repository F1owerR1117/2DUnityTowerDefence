using UnityEngine;

namespace DoudizhuTower.Config
{
    public enum CodexCategory
    {
        CardValue,
        CardType,
        Boss,
        Building,
        Passive,
        Rule
    }

    [CreateAssetMenu(fileName = "NewCodexEntry", menuName = "DoudizhuTower/Codex Entry")]
    public class CodexEntry : ScriptableObject
    {
        [Header("基本信息")]
        public string Id;
        public string DisplayName;
        public CodexCategory Category;

        [Header("显示内容")]
        public Sprite Icon;
        [TextArea(3, 10)]
        public string Description;
        [TextArea(3, 10)]
        public string ExtraInfo;

        [Header("搜索")]
        public string[] Keywords;

        [Header("扩展")]
        public string[] RelatedEntries;
    }
}
