namespace Anchor.Character.Attributes
{
    /// <summary>
    /// 玩家属性表中的核心属性 ID。这里不负责加载配置，只集中声明代码中常用的表格 ID。
    /// </summary>
    public static class CharacterAttributeIds
    {
        /// <summary>每周刷新行动力时使用的基础值。</summary>
        public const int BaseWeeklyActionPower = 1001;

        /// <summary>当前周剩余行动力，投点会消耗。</summary>
        public const int WeeklyActionPower = 1002;

        /// <summary>每个月开始时发放的金币数量。</summary>
        public const int MonthlyCoinIncome = 1003;

        /// <summary>每个月结算时使用的基础愿望单增长。</summary>
        public const int MonthlyWishlistGrowth = 1004;

        /// <summary>当前 Bug 值。</summary>
        public const int Bug = 1005;

        /// <summary>画面表现分。</summary>
        public const int Visual = 1006;

        /// <summary>氛围表现分。</summary>
        public const int Atmosphere = 1007;

        /// <summary>当前持有金币，唯一货币。</summary>
        public const int Coins = 1008;

        /// <summary>当前愿望单数量。</summary>
        public const int Wishlist = 1009;

        /// <summary>当前质量分。</summary>
        public const int Quality = 1010;
    }
}
