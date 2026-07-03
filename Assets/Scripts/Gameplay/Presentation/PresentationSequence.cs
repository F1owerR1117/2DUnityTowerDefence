using System;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Presentation
{
    /// <summary>
    /// 演出序列配置（MonoBehaviour）。
    /// 挂在场景中，包含镜头、对话、广播、特效的完整演出配置。
    /// </summary>
    public class PresentationSequence : MonoBehaviour
    {
        [Header("演出控制")]
        [Tooltip("演出期间是否暂停战斗逻辑（AI/索敌/攻击）")]
        public bool pauseBattle = true;

        [Tooltip("是否允许玩家跳过")]
        public bool allowSkip = true;

        [Header("镜头动作")]
        [Tooltip("镜头动作列表（按顺序执行：聚焦→震动→返回等）")]
        public CameraAction[] cameraActions;

        [Header("对话动作")]
        [Tooltip("对话动作列表（按顺序播放多组对话）")]
        public DialogueAction[] dialogues;

        [Header("广播动作")]
        [Tooltip("战场广播列表（按顺序显示警告/提示等文字）")]
        public AnnouncementAction[] announcements;

        [Header("特效动作")]
        [Tooltip("特效动作列表（按顺序生成粒子特效）")]
        public VfxAction[] effects;
    }

    [Serializable]
    public class CameraAction
    {
        [Tooltip("镜头动作类型")]
        public CameraActionType type;
        [Tooltip("镜头目标（FocusTarget/FollowTarget 时使用）")]
        public Transform target;
        [Tooltip("动作持续时间（秒）")]
        public float duration = 1f;
        [Tooltip("动作延迟执行时间（秒）")]
        public float delay = 0f;
        [Tooltip("镜头震动强度（Shake 时使用）")]
        public float shakeIntensity = 0.3f;
        [Tooltip("镜头缩放大小（Zoom 时使用，2D 用 orthographicSize）")]
        public float zoomSize = 3f;
    }

    public enum CameraActionType
    {
        FocusTarget,
        FollowTarget,
        Return,
        Shake,
        Zoom
    }

    [Serializable]
    public class DialogueAction
    {
        public DialogueLine[] lines;
    }

    [Serializable]
    public class DialogueLine
    {
        [Tooltip("说话人名字（如 Boss、旁白）")]
        public string speaker;
        [Tooltip("对话内容")]
        [TextArea(2, 5)]
        public string content;
        [Tooltip("对话显示时长（秒），waitForClick=true 时无效")]
        public float duration = 2f;
        [Tooltip("是否等待玩家点击后才消失")]
        public bool waitForClick;
    }

    [Serializable]
    public class AnnouncementAction
    {
        [Tooltip("广播类型（Warning=红色警告 / BossHint=黄色Boss提示 / System=系统 / Victory=胜利）")]
        public AnnouncementType type;
        [Tooltip("广播文字内容")]
        [TextArea(2, 5)]
        public string content;
        [Tooltip("广播显示时长（秒）")]
        public float duration = 3f;
        [Tooltip("广播延迟显示时间（秒）")]
        public float delay = 0f;
    }

    public enum AnnouncementType
    {
        Warning,
        BossHint,
        System,
        Victory
    }

    [Serializable]
    public class VfxAction
    {
        [Tooltip("特效预制体")]
        public GameObject vfxPrefab;
        [Tooltip("特效生成位置（留空则在原点生成）")]
        public Transform spawnPoint;
        [Tooltip("特效延迟生成时间（秒）")]
        public float delay = 0f;
    }
}
