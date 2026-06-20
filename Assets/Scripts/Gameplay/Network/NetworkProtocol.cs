using System;
using DoudizhuTower.Core.Cards;

namespace DoudizhuTower.Gameplay.Network
{
    /// <summary>
    /// 网络协议常量与序列化工具。
    /// 所有联机事件的 Key 定义、数据序列化/反序列化方法集中管理。
    /// </summary>
    public static class NetworkProtocol
    {
        // ─── 事件 Key ───

        // 叫分阶段
        public const string BID_TURN = "BID_TURN";
        public const string BID_ACTION = "BID_ACTION";
        public const string BID_RESULT = "BID_RESULT";

        // 游戏初始化
        public const string GAME_INIT = "GAME_INIT";

        // 出牌
        public const string PLAY_CARDS = "PLAY_CARDS";
        public const string PLAY_APPROVED = "PLAY_APPROVED";
        public const string PLAY_REJECTED = "PLAY_REJECTED";

        // 抽牌
        public const string DRAW_CARD = "DRAW_CARD";            // 客户端 → Master：请求摸牌
        public const string DRAW_CARD_RESULT = "DRAW_CARD_RESULT"; // Master → 所有客户端：摸牌结果

        // 领域系统
        public const string DOMAIN_ACTIVATE = "DOMAIN_ACTIVATE";
        public const string COUNTER_ACTIVATE = "COUNTER_ACTIVATE";

        // 状态校验
        public const string STATE_CHECKSUM = "STATE_CHECKSUM";

        // 控制
        public const string GAME_END = "GAME_END";
        public const string PLAYER_LEFT = "PLAYER_LEFT";

        // 经济同步
        public const string GOLD_UPDATE = "GOLD_UPDATE";

        // 客户端就绪（非房主报告初始金币给 Master）
        public const string PLAYER_READY = "PLAYER_READY";

        // 领域/反制 pending 状态同步
        public const string DOMAIN_PENDING = "DOMAIN_PENDING";
        public const string COUNTER_PENDING = "COUNTER_PENDING";

        // 飞筒传牌
        public const string CARD_TRANSFER = "CARD_TRANSFER";   // 发送方 → Master
        public const string CARD_ARRIVE = "CARD_ARRIVE";       // Master → 接收方
        public const string CARD_TAKE = "CARD_TAKE";           // 接收方 → Master（取走暂存槽牌）

        // Master 迁移
        public const string MASTER_STATE_SYNC = "MASTER_STATE_SYNC"; // Master 定期广播 / 迁移时广播

        // 战斗 HP 校验
        public const string HP_CHECKSUM = "HP_CHECKSUM";             // Master 定期广播 HP 校验和
        public const string HP_CORRECTION = "HP_CORRECTION";         // Master 广播完整 HP 修正数据
        public const string UNIT_DIED = "UNIT_DIED";                 // Master 广播单位死亡（Client 播放死亡动画+回收）

        // 战斗表现事件（Master → Client，仅用于视觉表现）
        public const string UNIT_ATTACK = "UNIT_ATTACK";             // Master 广播单位攻击（Client 播放攻击动画）
        public const string UNIT_HIT = "UNIT_HIT";                   // Master 广播单位受击（Client 播放受击动画+飘字）
        public const string UNIT_STUN = "UNIT_STUN";                 // Master 广播眩晕状态（Client 播放眩晕特效）
        public const string UNIT_KNOCKBACK = "UNIT_KNOCKBACK";       // Master 广播击退（Client 播放击退动画）

        // 快照同步
        public const string SNAPSHOT_PUSH = "SNAPSHOT_PUSH";

        // 弃牌同步
        public const string CARD_DISCARDED = "CARD_DISCARDED";

        // 新牌堆
        public const string NEW_DECK = "NEW_DECK";

        // 游戏开始（收敛门）
        public const string GAME_START = "GAME_START";

        // 运行期自愈
        public const string RECONCILE_REQUEST = "RECONCILE_REQ";
        public const string SNAPSHOT_RESPONSE = "SNAPSHOT_RESP";

        // 房间管理
        public const string ADD_AI = "ADD_AI";
        public const string REMOVE_AI = "REMOVE_AI";
        public const string KICK_PLAYER = "KICK_PLAYER";

        // ─── 初始化同步 ───
        public const string PLAYER_LIST_LOCKED = "PLAYER_LIST_LOCKED";
        public const string HAND_SYNC = "HAND_SYNC";  // Master → Client：同步初始手牌

        // ─── Card 序列化 ───

        public static int[] SerializeCards(Card[] cards)
        {
            var indices = new int[cards.Length];
            for (int i = 0; i < cards.Length; i++)
                indices[i] = cards[i].DeckIndex;
            return indices;
        }

        public static Card[] DeserializeCards(int[] indices, CardDeck deck)
        {
            var cards = new Card[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                cards[i] = deck.GetCardByIndex(indices[i]);
            return cards;
        }

        // ─── CardTypeResult 序列化 ───

        public static object[] SerializeCardTypeResult(CardTypeResult r)
        {
            if (r.KickerRanks is { Length: > 0 })
            {
                var kickers = new int[r.KickerRanks.Length];
                for (int i = 0; i < kickers.Length; i++)
                    kickers[i] = (int)r.KickerRanks[i];
                return new object[] { (int)r.Type, (int)r.MainRank, r.Length, kickers };
            }
            return new object[] { (int)r.Type, (int)r.MainRank, r.Length, null };
        }

        public static CardTypeResult DeserializeCardTypeResult(object[] data)
        {
            var result = new CardTypeResult
            {
                Type = (CardType)(int)data[0],
                MainRank = (CardRank)(int)data[1],
                Length = (int)data[2]
            };
            if (data[3] != null)
            {
                var kickers = (int[])data[3];
                result.KickerRanks = new CardRank[kickers.Length];
                for (int i = 0; i < kickers.Length; i++)
                    result.KickerRanks[i] = (CardRank)kickers[i];
            }
            return result;
        }

        // ─── 安全拆箱工具 ───

        public static int SafeInt(object o) => Convert.ToInt32(o ?? 0);
        public static float SafeFloat(object o) => Convert.ToSingle(o ?? 0f);

        /// <summary>Tick 有效性校验（必须 > 0）</summary>
        public static bool IsValidTick(this int tick) => tick > 0;

        // ─── 玩家槽位工具 ───

        public static int GetPlayerSlot(int actorNumber, int[] sortedActorNumbers)
        {
            for (int i = 0; i < sortedActorNumbers.Length; i++)
                if (sortedActorNumbers[i] == actorNumber) return i;
            return -1;
        }
    }
}
