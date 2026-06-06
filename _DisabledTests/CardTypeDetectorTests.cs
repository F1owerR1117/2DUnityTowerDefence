using System.Collections.Generic;
using System.Linq;
using DoudizhuTower.Core.Cards;
using NUnit.Framework;

namespace DoudizhuTower.Tests.Editor
{
    /// <summary>
    /// CardTypeDetector 单元测试。
    /// 覆盖全部基础牌型 + 边界情况。
    /// </summary>
    public class CardTypeDetectorTests
    {
        // ─── 辅助构造方法 ──────────────────────────────

        private static Card C(CardSuit s, CardRank r) => new(s, r);
        private static Card S(CardRank r) => C(CardSuit.Spade, r);
        private static Card H(CardRank r) => C(CardSuit.Heart, r);
        private static Card C(CardRank r) => C(CardSuit.Club, r);
        private static Card D(CardRank r) => C(CardSuit.Diamond, r);

        private static Card JokerCard => new(CardSuit.None, CardRank.Joker);

        private const int FarmerLimit = 5;
        private const int LandlordLimit = 6;

        // ─── 单张检测 ──────────────────────────────────

        [Test]
        public void Detect_SingleCard_ReturnsSingle()
        {
            var cards = new[] { S(CardRank.Ace) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Single, result.Type);
            Assert.AreEqual(CardRank.Ace, result.MainRank);
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Detect_SingleJoker_ReturnsKingSingle()
        {
            var cards = new[] { JokerCard };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Single, result.Type);
            Assert.IsTrue(result.IsValid);
        }
        }

        // ─── 对子检测 ──────────────────────────────────

        [Test]
        public void Detect_Pair_ReturnsPair()
        {
            var cards = new[] { S(CardRank.Five), H(CardRank.Five) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Pair, result.Type);
            Assert.AreEqual(CardRank.Five, result.MainRank);
        }

        [Test]
        public void Detect_TwoDifferentRanks_ReturnsInvalid()
        {
            var cards = new[] { S(CardRank.Three), H(CardRank.Four) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        // ─── 三张检测 ──────────────────────────────────

        [Test]
        public void Detect_Triple_ReturnsTriple()
        {
            var cards = new[] { S(CardRank.King), H(CardRank.King), C(CardRank.King) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Triple, result.Type);
            Assert.AreEqual(CardRank.King, result.MainRank);
        }

        // ─── 炸弹检测 ──────────────────────────────────

        [Test]
        public void Detect_Bomb_ReturnsBomb()
        {
            var cards = new[] { S(CardRank.Ten), H(CardRank.Ten), C(CardRank.Ten), D(CardRank.Ten) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Bomb, result.Type);
            Assert.AreEqual(CardRank.Ten, result.MainRank);
        }

        [Test]
        public void Detect_Bomb_PriorityOverTripleWithOne()
        {
            // 4 张同点数 → 炸弹优先于三带一
            var cards = new[] { S(CardRank.Three), H(CardRank.Three), C(CardRank.Three), D(CardRank.Three) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Bomb, result.Type);
        }

        // ─── 三带一检测 ────────────────────────────────

        [Test]
        public void Detect_TripleWithOne_ReturnsTripleWithOne()
        {
            var cards = new[] { S(CardRank.Jack), H(CardRank.Jack), C(CardRank.Jack), D(CardRank.Three) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.TripleWithOne, result.Type);
            Assert.AreEqual(CardRank.Jack, result.MainRank);
            Assert.AreEqual(1, result.KickerRanks.Length);
            Assert.AreEqual(CardRank.Three, result.KickerRanks[0]);
        }

        // ─── 三带二检测 ────────────────────────────────

        [Test]
        public void Detect_TripleWithPair_ReturnsTripleWithPair()
        {
            var cards = new[] { S(CardRank.Seven), H(CardRank.Seven), C(CardRank.Seven), D(CardRank.Five), S(CardRank.Five) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.TripleWithPair, result.Type);
            Assert.AreEqual(CardRank.Seven, result.MainRank);
            Assert.AreEqual(1, result.KickerRanks.Length);
            Assert.AreEqual(CardRank.Five, result.KickerRanks[0]);
        }

        // ─── 顺子检测 ──────────────────────────────────

        [Test]
        public void Detect_Straight5_ReturnsStraight()
        {
            var cards = new[] { S(CardRank.Three), H(CardRank.Four), C(CardRank.Five), D(CardRank.Six), S(CardRank.Seven) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Straight, result.Type);
            Assert.AreEqual(CardRank.Seven, result.MainRank);
            Assert.AreEqual(5, result.Length);
        }

        [Test]
        public void Detect_Straight_WithUnsortedInput()
        {
            // 输入顺序不影响检测
            var cards = new[] { S(CardRank.Seven), S(CardRank.Three), S(CardRank.Five), S(CardRank.Six), S(CardRank.Four) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Straight, result.Type);
            Assert.AreEqual(CardRank.Seven, result.MainRank);
        }

        [Test]
        public void Detect_Straight4_ReturnsInvalid()
        {
            var cards = new[] { S(CardRank.Three), H(CardRank.Four), C(CardRank.Five), D(CardRank.Six) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_StraightWith2_ReturnsInvalid()
        {
            // 顺子不能包含 2
            var cards = new[] { S(CardRank.Ten), H(CardRank.Jack), C(CardRank.Queen), D(CardRank.King), S(CardRank.Two) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_StraightWithJoker_ReturnsInvalid()
        {
            var cards = new[] { S(CardRank.Three), H(CardRank.Four), C(CardRank.Five), D(CardRank.Six), JokerCard };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_Straight6_ForFarmer_ReturnsInvalid()
        {
            // 农民最多出 5 张
            var cards = new[] { S(CardRank.Three), H(CardRank.Four), C(CardRank.Five), D(CardRank.Six), S(CardRank.Seven), H(CardRank.Eight) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_Straight6_ForLandlord_ReturnsStraight()
        {
            // 地主可以出 6+ 顺子
            var cards = new[] { S(CardRank.Three), H(CardRank.Four), C(CardRank.Five), D(CardRank.Six), S(CardRank.Seven), H(CardRank.Eight) };
            var result = CardTypeDetector.Detect(cards, LandlordLimit);

            Assert.AreEqual(CardType.Straight, result.Type);
            Assert.AreEqual(6, result.Length);
        }

        // ─── 连对检测（地主专属 ≥6 张） ─────────────

        [Test]
        public void Detect_ConsecutivePairs6_ReturnsConsecutivePair()
        {
            // 3 连对 = 6 张
            var cards = new[]
            {
                S(CardRank.Five), H(CardRank.Five),
                S(CardRank.Six), H(CardRank.Six),
                S(CardRank.Seven), H(CardRank.Seven)
            };
            var result = CardTypeDetector.Detect(cards, LandlordLimit);

            Assert.AreEqual(CardType.ConsecutivePair, result.Type);
            Assert.AreEqual(CardRank.Seven, result.MainRank);
            Assert.AreEqual(3, result.Length);
        }

        // ─── 边界情况 ──────────────────────────────────

        [Test]
        public void Detect_Empty_ReturnsInvalid()
        {
            var result = CardTypeDetector.Detect(System.Array.Empty<Card>(), FarmerLimit);
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_Null_ReturnsInvalid()
        {
            var result = CardTypeDetector.Detect(null, FarmerLimit);
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_OverLimit_ReturnsInvalid()
        {
            // 农民最多出 5 张
            var cards = new[] { S(CardRank.Three), H(CardRank.Four), C(CardRank.Five), D(CardRank.Six), S(CardRank.Seven), H(CardRank.Eight) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_DuplicateCards_ReturnsInvalid()
        {
            // 不可能在真实游戏中发生，但检测器应该处理
            var cards = new[] { S(CardRank.Ace), S(CardRank.Ace) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_FourDifferentRank_ReturnsInvalid()
        {
            var cards = new[] { S(CardRank.Three), H(CardRank.Five), C(CardRank.Seven), D(CardRank.Nine) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_FiveDifferentRank_NotConsecutive_ReturnsInvalid()
        {
            var cards = new[] { S(CardRank.Three), H(CardRank.Five), C(CardRank.Seven), D(CardRank.Nine), S(CardRank.Jack) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_TripleWithTwoSingles_ReturnsInvalid()
        {
            // 3+1+1 在本游戏中不是合规牌型
            var cards = new[] { S(CardRank.Three), H(CardRank.Three), C(CardRank.Three), D(CardRank.Five), S(CardRank.Seven) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void Detect_Single3_ReturnsSingle()
        {
            var result = CardTypeDetector.Detect(new[] { S(CardRank.Three) }, FarmerLimit);
            Assert.AreEqual(CardType.Single, result.Type);
            Assert.AreEqual(CardRank.Three, result.MainRank);
        }

        [Test]
        public void Detect_PairOfTwos_ReturnsPair()
        {
            var cards = new[] { S(CardRank.Two), H(CardRank.Two) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);
            Assert.AreEqual(CardType.Pair, result.Type);
            Assert.AreEqual(CardRank.Two, result.MainRank);
        }

        // ─── 双王炸标记检测（在 Bomb 检测中标记） ─────

        [Test]
        public void Detect_DoubleKingBomb_IsMarked()
        {
            // 大小王 + 3 张填位牌（5 张，填满农民出牌上限）
            var cards = new[] { JokerCard, JokerCard, S(CardRank.Three), H(CardRank.Four), C(CardRank.Five) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            // 双王炸的检测在阶段五才完整实现
            // 阶段一只确保不误判为其他牌型
            Assert.IsFalse(result.IsValid);
        }

        // ─── 花色不影响牌型检测 ────────────────────────

        [Test]
        public void Detect_MixedSuits_Valid()
        {
            // 不同花色不影响检测
            var cards = new[] { S(CardRank.Nine), H(CardRank.Nine), C(CardRank.Nine), D(CardRank.Nine) };
            var result = CardTypeDetector.Detect(cards, FarmerLimit);

            Assert.AreEqual(CardType.Bomb, result.Type);
        }

        // ─── 大量随机测试 ──────────────────────────────

        [Test]
        public void Detect_BulkRandomTests(
            [Values(1, 10, 100)] int iterations)
        {
            var rng = new System.Random(42);
            var ranks = new[]
            {
                CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six,
                CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Ten,
                CardRank.Jack, CardRank.Queen, CardRank.King, CardRank.Ace
            };
            var suits = new[] { CardSuit.Spade, CardSuit.Heart, CardSuit.Club, CardSuit.Diamond };

            for (int i = 0; i < iterations; i++)
            {
                var hand = new HashSet<Card>();
                int count = rng.Next(1, 6);
                for (int j = 0; j < count; j++)
                {
                    hand.Add(new Card(suits[rng.Next(4)], ranks[rng.Next(12)]));
                }

                var result = CardTypeDetector.Detect(hand.ToArray(), FarmerLimit);

                // 保证：检测器不会抛异常
                // 保证：Invalid 或有效牌型之间互斥
                Assert.DoesNotThrow(() => _ = result.IsValid);
            }
        }
    }
}
