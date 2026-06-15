using System;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// Event + Snapshot + Tick 三层确定性模型 v1.0
    /// Event = 不可变操作记录（append-only），必须携带 Tick，不可直接修改状态。
    /// </summary>
    [Serializable]
    public readonly struct GameEvent
    {
        public readonly int Tick;
        public readonly int Slot;
        public readonly string Type;
        public readonly object[] Payload;

        public GameEvent(int tick, int slot, string type, object[] payload)
        {
            Tick = tick;
            Slot = slot;
            Type = type;
            Payload = payload;
        }

        /// <summary>序列化为 NetworkProtocol 传输格式</summary>
        public object[] Serialize()
        {
            return new object[] { Tick, Slot, Type, Payload };
        }

        /// <summary>反序列化</summary>
        public static GameEvent Deserialize(object[] data)
        {
            if (data == null || data.Length < 4)
                return default;
            return new GameEvent(
                NetworkProtocol.SafeInt(data[0]),
                NetworkProtocol.SafeInt(data[1]),
                data[2] as string ?? "",
                data[3] as object[] ?? Array.Empty<object>()
            );
        }

        public bool IsValid => Tick > 0 && !string.IsNullOrEmpty(Type);
    }
}
