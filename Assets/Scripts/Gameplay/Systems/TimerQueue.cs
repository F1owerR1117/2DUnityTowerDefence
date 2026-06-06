using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Systems
{
    /// <summary>
    /// 全局异步计时器队列。
    /// 游戏中所有定时事件（补牌/领域/叫分/AI判定等）必须统一经过此系统，
    /// 禁止散落在各 MonoBehaviour 中使用 Invoke() 或 StartCoroutine()。
    /// </summary>
    public class TimerQueue : MonoBehaviour
    {
        private class TimerEntry
        {
            public float TriggerTime;
            public float Interval;
            public Action Callback;
            public bool IsLoop;
            public bool Canceled;
        }

        private readonly List<TimerEntry> _timers = new();
        private readonly Queue<int> _pendingRemovals = new();
        private float _currentTime;
        private int _nextId;

        /// <summary>
        /// 安排一次性延迟回调
        /// </summary>
        /// <param name="delaySeconds">延迟秒数</param>
        /// <param name="callback">回调</param>
        /// <returns>计时器 ID（可用于取消）</returns>
        public int Schedule(float delaySeconds, Action callback)
        {
            var entry = new TimerEntry
            {
                TriggerTime = _currentTime + delaySeconds,
                Interval = 0f,
                Callback = callback,
                IsLoop = false,
                Canceled = false
            };
            _timers.Add(entry);
            return entry.GetHashCode();
        }

        /// <summary>
        /// 安排循环回调
        /// </summary>
        /// <param name="intervalSeconds">循环间隔（秒）</param>
        /// <param name="callback">回调</param>
        /// <returns>计时器 ID</returns>
        public int ScheduleLoop(float intervalSeconds, Action callback)
        {
            var entry = new TimerEntry
            {
                TriggerTime = _currentTime + intervalSeconds,
                Interval = intervalSeconds,
                Callback = callback,
                IsLoop = true,
                Canceled = false
            };
            _timers.Add(entry);
            return entry.GetHashCode();
        }

        /// <summary>
        /// 取消指定计时器
        /// </summary>
        public void Cancel(int timerId)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timers[i].GetHashCode() == timerId)
                {
                    _timers[i].Canceled = true;
                    break;
                }
            }
        }

        /// <summary>
        /// 取消全部计时器（领域切换/场景切换时调用）
        /// </summary>
        public void CancelAll()
        {
            foreach (var t in _timers)
                t.Canceled = true;
            _timers.Clear();
        }

        private void Update()
        {
            _currentTime += Time.deltaTime;

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                var entry = _timers[i];
                if (entry.Canceled)
                {
                    _timers.RemoveAt(i);
                    continue;
                }

                if (_currentTime >= entry.TriggerTime)
                {
                    entry.Callback?.Invoke();

                    if (entry.IsLoop)
                    {
                        entry.TriggerTime = _currentTime + entry.Interval;
                    }
                    else
                    {
                        _timers.RemoveAt(i);
                    }
                }
            }
        }
    }
}
