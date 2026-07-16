using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    // =========================
    // 意图类型枚举
    // =========================
    public enum IntentType : byte
    {
        None = 0,
        Idle = 1,
        Move = 2,
        Attack = 3,
        CastSkill = 4,
        Retreat = 5,
        Spawn = 6,
    }

    // =========================
    // 单位意图
    // =========================
    public struct UnitIntent
    {
        public int UnitId;
        public IntentType Type;
        public int TargetId;
        public float TargetPosX;
        public float TargetPosY;
        public int SkillId;
    }

    // =========================
    // 叫分意图
    // =========================
    public struct BidIntent
    {
        public int Slot;
        public int Bid;
    }

    // =========================
    // 意图缓冲区
    // =========================
    public class IntentBuffer
    {
        private const int MAX_INTENTS = 128;
        private UnitIntent[] _intents = new UnitIntent[MAX_INTENTS];
        private int _count;

        // 叫分意图队列（独立于单位意图）
        private const int MAX_BID_INTENTS = 8;
        private BidIntent[] _bidIntents = new BidIntent[MAX_BID_INTENTS];
        private int _bidCount;

        public int Count => _count;
        public int BidCount => _bidCount;

        /// <summary>
        /// 添加意图
        /// </summary>
        public void Add(UnitIntent intent)
        {
            if (_count >= MAX_INTENTS) return;
            _intents[_count] = intent;
            _count++;
        }

        /// <summary>
        /// 获取意图
        /// </summary>
        public UnitIntent Get(int index)
        {
            if (index < 0 || index >= _count) return default;
            return _intents[index];
        }

        /// <summary>
        /// 清空意图
        /// </summary>
        public void Clear()
        {
            _count = 0;
            _bidCount = 0;
        }

        // =========================
        // 叫分意图方法
        // =========================

        public void AddBid(int slot, int bid)
        {
            if (_bidCount >= MAX_BID_INTENTS) return;
            _bidIntents[_bidCount] = new BidIntent { Slot = slot, Bid = bid };
            _bidCount++;
        }

        public bool HasBid() => _bidCount > 0;

        public BidIntent PopBid()
        {
            if (_bidCount <= 0) return default;
            var intent = _bidIntents[0];
            // 移动后续元素
            for (int i = 1; i < _bidCount; i++)
                _bidIntents[i - 1] = _bidIntents[i];
            _bidCount--;
            return intent;
        }

        /// <summary>
        /// 查找指定单位的意图
        /// </summary>
        public UnitIntent FindByUnitId(int unitId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_intents[i].UnitId == unitId)
                    return _intents[i];
            }
            return default;
        }

        // =========================
        // 便捷工厂方法
        // =========================

        public void AddIdle(int unitId)
        {
            Add(new UnitIntent
            {
                UnitId = unitId,
                Type = IntentType.Idle
            });
        }

        public void AddMove(int unitId, float targetX, float targetY)
        {
            Add(new UnitIntent
            {
                UnitId = unitId,
                Type = IntentType.Move,
                TargetPosX = targetX,
                TargetPosY = targetY
            });
        }

        public void AddAttack(int unitId, int targetId)
        {
            Add(new UnitIntent
            {
                UnitId = unitId,
                Type = IntentType.Attack,
                TargetId = targetId
            });
        }

        public void AddSpawn(int unitId, int cardId, float targetX, float targetY)
        {
            Add(new UnitIntent
            {
                UnitId = unitId,
                Type = IntentType.Spawn,
                TargetPosX = targetX,
                TargetPosY = targetY,
                SkillId = cardId
            });
        }
    }
}