using System.Text;

namespace DoudizhuTower.Core.Cards
{
    public struct CardTypeResult
    {
        public CardType Type { get; set; }
        public CardRank MainRank { get; set; }
        public CardRank[] KickerRanks { get; set; }
        public int Length { get; set; }
        public readonly bool IsValid => Type != CardType.Invalid;
        public static CardTypeResult Invalid => new() { Type = CardType.Invalid };

        public override readonly string ToString()
        {
            if (!IsValid) return "无效";
            var sb = new StringBuilder();
            sb.Append(ToChineseTypeName(Type));
            if (Type == CardType.Plane && Length > 1)
            {
                // 飞机：显示连续三张组的点数范围，如 3+4、3+4+5
                int low = (int)MainRank - (Length - 1);
                for (int r = low; r <= (int)MainRank; r++)
                {
                    if (r > low) sb.Append('+');
                    sb.Append(((CardRank)r).ToDisplayString());
                }
            }
            else if (Type == CardType.Straight && Length > 0)
            {
                // 顺子：显示起止点数，如 3~7
                int low = (int)MainRank - (Length - 1);
                sb.Append(((CardRank)low).ToDisplayString());
                sb.Append('~');
                sb.Append(MainRank.ToDisplayString());
            }
            else if (Type == CardType.ConsecutivePair && Length > 0)
            {
                // 连对：显示起止点数，如 3~5
                int low = (int)MainRank - (Length - 1);
                sb.Append(((CardRank)low).ToDisplayString());
                sb.Append('~');
                sb.Append(MainRank.ToDisplayString());
            }
            else
            {
                sb.Append(MainRank.ToDisplayString());
                if (Length > 0) sb.Append($"×{Length}");
            }
            if (KickerRanks is { Length: > 0 })
            {
                sb.Append(" 带");
                // TripleWithPair 的 kicker 存的是对子点数（单元素），需加"对"前缀
                if (Type == CardType.TripleWithPair)
                {
                    sb.Append($"对{KickerRanks[0].ToDisplayString()}");
                }
                else
                {
                    int i = 0;
                    while (i < KickerRanks.Length)
                    {
                        if (i + 1 < KickerRanks.Length && KickerRanks[i] == KickerRanks[i + 1])
                        {
                            sb.Append($"对{KickerRanks[i].ToDisplayString()}");
                            i += 2;
                        }
                        else
                        {
                            sb.Append(KickerRanks[i].ToDisplayString());
                            i++;
                        }
                        if (i < KickerRanks.Length) sb.Append(' ');
                    }
                }
            }
            return sb.ToString();
        }

        private static string ToChineseTypeName(CardType type) => type switch
        {
            CardType.Single => "单张 ",
            CardType.Pair => "对子 ",
            CardType.Triple => "三条 ",
            CardType.TripleWithOne => "三带一 ",
            CardType.TripleWithPair => "三带二 ",
            CardType.Straight => "顺子 ",
            CardType.Bomb => "炸弹 ",
            CardType.ConsecutivePair => "连对 ",
            CardType.FourWithTwo => "四带二 ",
            CardType.Plane => "飞机 ",
            CardType.DoubleKingBomb => "王炸",
            _ => type.ToString()
        };
    }
}
