using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    // =========================
    // 事件类型枚举
    // =========================
    public enum EventType : byte
    {
        None = 0,
        Damage = 1,
        Heal = 2,
        Dash = 3,
        Spawn = 4,
        Buff = 5,
    }

    // =========================
    // 基础事件
    // =========================
    public struct GameEvent
    {
        public EventType Type;
        public int TargetId;      // 目标单位 ID (-1=无目标)
        public int SourceId;      // 来源单位 ID
        public float Value;       // 数值（伤害/治疗量）
        public float Duration;    // 持续时间（Buff）
        public float PosX;        // 位置 X
        public float PosY;        // 位置 Y
        public float ExtraFloat;  // 额外浮点（冲锋宽度等）
    }

    // =========================
    // 事件缓冲区
    // =========================
    public class EventBuffer
    {
        private const int MAX_EVENTS = 256;
        private GameEvent[] _events = new GameEvent[MAX_EVENTS];
        private int _count;

        public int Count => _count;

        /// <summary>
        /// 添加事件
        /// </summary>
        public void Add(GameEvent evt)
        {
            if (_count >= MAX_EVENTS) return;
            _events[_count] = evt;
            _count++;
        }

        /// <summary>
        /// 获取事件
        /// </summary>
        public GameEvent Get(int index)
        {
            if (index < 0 || index >= _count) return default;
            return _events[index];
        }

        /// <summary>
        /// 清空事件
        /// </summary>
        public void Clear()
        {
            _count = 0;
        }

        // =========================
        // 便捷工厂方法
        // =========================

        public void AddDamage(int targetId, int sourceId, float damage)
        {
            Add(new GameEvent
            {
                Type = EventType.Damage,
                TargetId = targetId,
                SourceId = sourceId,
                Value = damage
            });
        }

        public void AddHeal(int targetId, float heal)
        {
            Add(new GameEvent
            {
                Type = EventType.Heal,
                TargetId = targetId,
                Value = heal
            });
        }

        public void AddDash(int sourceId, float startX, float startY, float endX, float endY, float damage, float width)
        {
            Add(new GameEvent
            {
                Type = EventType.Dash,
                SourceId = sourceId,
                PosX = startX,
                PosY = startY,
                Value = damage,
                ExtraFloat = width,
                // EndPos 存在 ExtraFloat2（简化：用两个事件表示）
            });
        }
    }
}