using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Presentation
{
    /// <summary>
    /// 战场广播管理器：队列显示广播，防重复触发。
    /// </summary>
    public class BattleAnnouncementManager : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("广播文字组件（TMP Text）")]
        [SerializeField] private TextMeshProUGUI announcementText;
        [Tooltip("控制广播淡入淡出的 CanvasGroup")]
        [SerializeField] private CanvasGroup announcementGroup;

        [Header("配置")]
        [Tooltip("广播默认显示时长（秒），单条广播可覆盖")]
        [SerializeField] private float defaultDuration = 3f;
        [Tooltip("淡入淡出动画时长（秒）")]
        [SerializeField] private float fadeDuration = 0.3f;

        private Queue<AnnouncementData> _queue = new();
        private HashSet<string> _triggeredIds = new();
        private Coroutine _currentRoutine;

        /// <summary>广播触发事件（供外部监听）</summary>
        public event Action<string, string> OnAnnouncementShown;

        /// <summary>显示广播（支持防重复）</summary>
        public void ShowAnnouncement(AnnouncementType type, string content, float duration = 0f, string uniqueId = null)
        {
            // 防重复
            if (!string.IsNullOrEmpty(uniqueId) && !_triggeredIds.Add(uniqueId))
                return;

            if (duration <= 0f) duration = defaultDuration;

            var data = new AnnouncementData { type = type, content = content, duration = duration };

            if (_currentRoutine != null)
                _queue.Enqueue(data);
            else
                _currentRoutine = StartCoroutine(ShowAnnouncementCoroutine(data));
        }

        /// <summary>重置所有防重复记录</summary>
        public void ResetAllTriggers()
        {
            _triggeredIds.Clear();
        }

        private IEnumerator ShowAnnouncementCoroutine(AnnouncementData data)
        {
            if (announcementText != null)
            {
                announcementText.text = GetAnnouncementPrefix(data.type) + data.content;
                announcementText.color = GetAnnouncementColor(data.type);
            }

            // 淡入
            if (announcementGroup != null)
            {
                announcementGroup.alpha = 0f;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    announcementGroup.alpha = elapsed / fadeDuration;
                    yield return null;
                }
                announcementGroup.alpha = 1f;
            }

            OnAnnouncementShown?.Invoke(data.type.ToString(), data.content);

            // 持续显示
            yield return new WaitForSecondsRealtime(data.duration);

            // 淡出
            if (announcementGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    announcementGroup.alpha = 1f - (elapsed / fadeDuration);
                    yield return null;
                }
                announcementGroup.alpha = 0f;
            }

            _currentRoutine = null;

            // 播放队列中的下一个
            if (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                _currentRoutine = StartCoroutine(ShowAnnouncementCoroutine(next));
            }
        }

        private string GetAnnouncementPrefix(AnnouncementType type)
        {
            switch (type)
            {
                case AnnouncementType.Warning: return "<color=red>⚠ </color>";
                case AnnouncementType.BossHint: return "<color=yellow>Boss: </color>";
                case AnnouncementType.Victory: return "<color=green>★ </color>";
                default: return "";
            }
        }

        private Color GetAnnouncementColor(AnnouncementType type)
        {
            switch (type)
            {
                case AnnouncementType.Warning: return Color.red;
                case AnnouncementType.BossHint: return Color.yellow;
                case AnnouncementType.Victory: return Color.green;
                default: return Color.white;
            }
        }

        private class AnnouncementData
        {
            public AnnouncementType type;
            public string content;
            public float duration;
        }
    }
}
