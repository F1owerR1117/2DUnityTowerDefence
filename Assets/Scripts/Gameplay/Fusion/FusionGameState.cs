using Fusion;
using System;
using UnityEngine;

namespace DoudizhuTower.Gameplay.Fusion
{
    // =========================
    // ① 全局游戏状态
    // =========================
    public struct GameState : INetworkStruct
    {
        public int Seed;
        public byte Phase;           // 0=叫分 1=出牌 2=结算
        public byte TurnSlot;        // 当前行动玩家
        public byte DeckCount;       // 剩余牌数
        public byte CurrentBidTurn;  // 当前叫分轮到谁
        public byte HighestBid;      // 最高叫分
        public byte HighestBidder;   // 最高叫分者 slot
        public byte BidCount;        // 已叫分人数
        public int TickCounter;      // Heartbeat：保证 snapshot 捕捉
        public int StateHash;        // 同步验证：Client 对比用
        public int BidTick;          // 叫分节拍（Host 写，Client 只读）
    }

    // =========================
    // ② 玩家状态（核心替代 slotHands + economy）
    // =========================
    public struct PlayerState : INetworkStruct
    {
        public byte Slot;         // 0/1/2
        public byte IsAI;         // 0=真人 1=AI
        public byte Role;         // 0=未定 1=地主 2=农民
        public byte Bid;          // 叫分值 (0=未叫 1/2/3)

        public int Gold;
        public int IncomeRate;

        public byte HandCount;

        public byte IsLandlord;   // 0=false, 1=true（避免 bool）
    }

    // =========================
    // ③ 战斗单位状态（唯一真相源）
    // =========================
    public struct UnitState : INetworkStruct
    {
        public int UnitId;
        public int Owner;         // 所属玩家 Slot

        public float PosX;        // 位置（避免 Vector2，Fusion 更友好）
        public float PosY;

        public int HP;
        public int MaxHP;

        public int TargetId;      // 攻击目标 (-1=无目标)

        public byte State;        // 0=Idle 1=Move 2=Attack 3=Dead

        public float AttackTimer; // 攻击冷却计时
        public float MoveSpeed;   // 移动速度
        public float AttackRange; // 攻击范围

        public byte IsLandlord;   // 0=false, 1=true

        // 被动标志位
        public byte PassiveFlags; // PassiveFlags 枚举
    }

    // =========================
    // ③-a 被动标志枚举
    // =========================
    [System.Flags]
    public enum PassiveFlags : byte
    {
        None = 0,
        HasAura = 1 << 0,
        HasRegen = 1 << 1,
        HasThorns = 1 << 2,
        HasShield = 1 << 3,
        HasSlow = 1 << 4,
        IsSlowed = 1 << 5,
        IsShielded = 1 << 6,
    }

    // =========================
    // ④ 卡牌系统（完全压缩）
    // =========================
    public struct FusionCard : INetworkStruct
    {
        public byte Id;   // 0~52
    }

    // =========================
    // ⑤ 输入系统
    // =========================
    public struct FusionPlayerInput : INetworkInput
    {
        public byte Action;     // 0=none,1=play,2=draw,3=bid
        public byte CardId;
        public byte Target;
    }

    // =========================
    // ⑥ 世界状态容器
    // =========================
    public struct WorldState : INetworkStruct
    {
        public GameState Game;
        public PlayerState Player0;
        public PlayerState Player1;
        public PlayerState Player2;
    }

    // =========================
    // ⑦ 单位状态常量
    // =========================
    public static class UnitStateConstants
    {
        public const byte Idle = 0;
        public const byte Move = 1;
        public const byte Attack = 2;
        public const byte Dead = 3;
    }
}