using UnityEngine;

namespace DoudizhuTower.Config
{
    /// <summary>
    /// 关卡配置表（ScriptableObject）。
    /// 每个关卡一个资产文件，添加新关卡只需创建新资产。
    /// 通过菜单 DoudizhuTower/LevelConfig 创建。
    /// </summary>
    [CreateAssetMenu(fileName = "Level_01", menuName = "DoudizhuTower/LevelConfig")]
    public class LevelConfig : ScriptableObject
    {
        [Header("关卡信息")]
        [Tooltip("关卡名称")]
        public string levelName = "未命名关卡";

        [Tooltip("关卡描述")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("难度星级（1~5）")]
        [Range(1, 5)]
        public int difficulty = 1;

        [Header("场景")]
        [Tooltip("关卡场景名称（需在 Build Settings 中注册）")]
        public string sceneName = "DoudizhuTower_Game";

        [Header("显示")]
        [Tooltip("关卡缩略图")]
        public Sprite thumbnail;

        [Tooltip("是否已解锁（后续接入存档系统）")]
        public bool isUnlocked = true;

        [Tooltip("关卡序号（用于排序）")]
        public int sortOrder;
    }
}
