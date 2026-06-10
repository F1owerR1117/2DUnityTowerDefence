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
        public const string DRAW_CARD = "DRAW_CARD";

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

        // 房间管理
        public const string ADD_AI = "ADD_AI";
        public const string REMOVE_AI = "REMOVE_AI";
        public const string KICK_PLAYER = "KICK_PLAYER";

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

        // ─── 玩家槽位工具 ───

        public static int GetPlayerSlot(int actorNumber, int[] sortedActorNumbers)
        {
            for (int i = 0; i < sortedActorNumbers.Length; i++)
                if (sortedActorNumbers[i] == actorNumber) return i;
            return -1;
        }
    }
}
