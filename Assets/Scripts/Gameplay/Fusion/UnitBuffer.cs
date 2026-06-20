using System;

namespace DoudizhuTower.Gameplay.Fusion
{
    /// <summary>
    /// 单位状态双缓冲。
    /// 读取时使用 ReadBuffer，写入时使用 WriteBuffer。
    /// 每帧结束后调用 Swap() 交换缓冲区。
    /// </summary>
    public class UnitBuffer
    {
        private const int MAX_UNITS = 64;

        private UnitState[] _bufferA = new UnitState[MAX_UNITS];
        private UnitState[] _bufferB = new UnitState[MAX_UNITS];

        private UnitState[] _readBuffer;
        private UnitState[] _writeBuffer;

        private int _count;

        public int Count => _count;
        public int Capacity => MAX_UNITS;

        /// <summary>
        /// 当前帧只读数据
        /// </summary>
        public UnitState[] Read => _readBuffer;

        /// <summary>
        /// 当前帧写入数据
        /// </summary>
        public UnitState[] Write => _writeBuffer;

        public UnitBuffer()
        {
            _readBuffer = _bufferA;
            _writeBuffer = _bufferB;
        }

        /// <summary>
        /// 交换缓冲区（帧末调用）
        /// </summary>
        public void Swap()
        {
            var temp = _readBuffer;
            _readBuffer = _writeBuffer;
            _writeBuffer = temp;
        }

        /// <summary>
        /// 添加单位
        /// </summary>
        public int Add(UnitState unit)
        {
            if (_count >= MAX_UNITS) return -1;

            _readBuffer[_count] = unit;
            _writeBuffer[_count] = unit;
            _count++;

            return unit.UnitId;
        }

        /// <summary>
        /// 获取单位（只读）
        /// </summary>
        public UnitState Get(int index)
        {
            if (index < 0 || index >= _count) return default;
            return _readBuffer[index];
        }

        /// <summary>
        /// 获取单位（可写）
        /// </summary>
        public void Set(int index, UnitState unit)
        {
            if (index < 0 || index >= _count) return;
            _writeBuffer[index] = unit;
        }

        /// <summary>
        /// 根据 UnitId 查找索引
        /// </summary>
        public int FindIndex(int unitId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_readBuffer[i].UnitId == unitId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 移除死亡单位
        /// </summary>
        public void CleanupDead()
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _count; readIndex++)
            {
                if (_readBuffer[readIndex].State != UnitStateConstants.Dead)
                {
                    _readBuffer[writeIndex] = _readBuffer[readIndex];
                    _writeBuffer[writeIndex] = _writeBuffer[readIndex];
                    writeIndex++;
                }
            }
            _count = writeIndex;
        }

        /// <summary>
        /// 清空所有单位
        /// </summary>
        public void Clear()
        {
            _count = 0;
        }
    }
}