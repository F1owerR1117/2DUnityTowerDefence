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

        // 叫分状态（Fusion Networked）
        public byte CurrentBidTurn;  // 当前叫分轮到谁
        public byte HighestBid;      // 最高叫分
        public byte HighestBidder;   // 最高叫分者 slot
        public byte BidCount;        // 已叫分人数
        public byte BidWinnerSlot;   // 叫分赢家 slot
        public byte IsBiddingFinished; // 0=进行中 1=已结束

        public int TickCounter;      // Heartbeat
        public int StateHash;        // 同步验证

        // 领域状态
        public byte DomainActive;
        public byte DomainType;
        public byte DomainSlot;
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

        public float PosX;        // 位置
        public float PosY;

        public int HP;
        public int MaxHP;

        public int ATK;           // 攻击力
        public float AttackSpeed; // 攻击间隔（秒）
        public float AttackTimer; // 攻击冷却计时
        public float AttackRange; // 攻击范围

        public int TargetId;      // 攻击目标 (-1=无目标)

        public byte State;        // 0=Idle 1=Move 2=Attack 3=Dead

        public float MoveSpeed;   // 移动速度
        public byte IsLandlord;   // 0=false, 1=true
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
        public byte Action;       // 0=none, 1=play, 2=draw, 3=bid
        public byte CardId;       // 单张牌 ID（play 时为第一张）
        public byte BidValue;     // 叫分值 (1/2/3)
        public byte RouteIndex;   // 路线索引
        public byte BaseIndex;    // 基地索引
        public byte CardCount;    // 出牌数量（多张牌时）
        public byte Card2;        // 第二张牌
        public byte Card3;        // 第三张牌
        public byte Card4;        // 第四张牌
        public byte Card5;        // 第五张牌
        public byte Card6;        // 第六张牌（地主上限 6 张）
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