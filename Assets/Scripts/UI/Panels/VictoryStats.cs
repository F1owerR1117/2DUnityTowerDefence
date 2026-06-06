namespace DoudizhuTower.UI.Panels
{
    public struct VictoryStats
    {
        public float gameDuration;
        public int cardsPlayed;
        public int unitsSpawned;
        public int unitsKilled;
        public float goldEarned;

        // 结算公式字段（联机模式）
        public int identityBaseScore;      // 地主=100, 农民=50
        public float bidMultiplier;        // 叫分乘数 1.0/2.0/3.0
        public float gameStateCoefficient; // 完胜=1.5, 标准=1.0

        public float SettlementAmount => identityBaseScore * bidMultiplier * gameStateCoefficient;
    }
}
