namespace DoudizhuTower.Core.Cards
{
    public enum CardType
    {
        Invalid, Single, Pair, Triple,
        TripleWithOne, TripleWithPair, Straight, Bomb,
        ConsecutivePair, FourWithTwo, Plane, Straight6Plus,
        /// <summary>双 Joker（占满出牌上限，召唤觉醒英雄）</summary>
        DoubleKingBomb,
    }
}
