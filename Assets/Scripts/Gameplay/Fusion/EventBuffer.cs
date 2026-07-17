namespace DoudizhuTower.Gameplay.Fusion
{
    // =========================
    // 事件类型枚举（只保留 3 种）
    // =========================
    public enum EventType : byte
    {
        None = 0,
        Spawn = 1,
        Hit = 2,
        Death = 3,
    }

    // =========================
    // 基础事件
    // =========================
    public struct GameEvent
    {
        public EventType Type;
        public int TargetId;      // 目标单位 ID
        public int SourceId;      // 来源单位 ID
        public float Value;       // 数值（伤害量）
        public float PosX;        // 位置 X
        public float PosY;        // 位置 Y
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

        public void Add(GameEvent evt)
        {
            if (_count >= MAX_EVENTS) return;
            _events[_count] = evt;
            _count++;
        }

        public GameEvent Get(int index)
        {
            if (index < 0 || index >= _count) return default;
            return _events[index];
        }

        public void Clear()
        {
            _count = 0;
        }

        // =========================
        // 便捷工厂方法（只保留 3 种）
        // =========================

        public void AddSpawn(int unitId, int ownerId)
        {
            Add(new GameEvent
            {
                Type = EventType.Spawn,
                TargetId = unitId,
                SourceId = ownerId
            });
        }

        public void AddHit(int targetId, int sourceId, float damage)
        {
            Add(new GameEvent
            {
                Type = EventType.Hit,
                TargetId = targetId,
                SourceId = sourceId,
                Value = damage
            });
        }

        public void AddDeath(int targetId, int sourceId)
        {
            Add(new GameEvent
            {
                Type = EventType.Death,
                TargetId = targetId,
                SourceId = sourceId
            });
        }
    }
}
