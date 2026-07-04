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

        /// <summary>每周结算时使用的基础愿望单增长。</summary>
        public const int WeeklyWishlistGrowth = 1004;

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

        /// <summary>进入程序房间触发选项时，选择消耗 1 点行动点获得的属性数值。</summary>
        public const int ProgramRoomOneActionReward = 1011;

        /// <summary>进入程序房间触发选项时，选择消耗 2 点行动点获得的属性数值。</summary>
        public const int ProgramRoomTwoActionReward = 1012;

        /// <summary>进入美术房间触发选项时，选择消耗 1 点行动点获得的属性数值。</summary>
        public const int ArtRoomOneActionReward = 1013;

        /// <summary>进入美术房间触发选项时，选择消耗 2 点行动点获得的属性数值。</summary>
        public const int ArtRoomTwoActionReward = 1014;

        /// <summary>进入音效房间触发选项时，选择消耗 1 点行动点获得的属性数值。</summary>
        public const int AudioRoomOneActionReward = 1015;

        /// <summary>进入音效房间触发选项时，选择消耗 2 点行动点获得的属性数值。</summary>
        public const int AudioRoomTwoActionReward = 1016;

        /// <summary>每在程序房间消耗 1 点行动点额外获得的属性数值。</summary>
        public const int ProgramRoomPerActionReward = 1017;

        /// <summary>每在美术房间消耗 1 点行动点额外获得的属性数值。</summary>
        public const int ArtRoomPerActionReward = 1018;

        /// <summary>每在音效房间消耗 1 点行动点额外获得的属性数值。</summary>
        public const int AudioRoomPerActionReward = 1019;

        /// <summary>连续两回合在同一房间消耗行动点时获得的愿望单数量。</summary>
        public const int SameRoomConsecutiveWishlistReward = 1020;

        /// <summary>每回合开始时，50% 概率获得的愿望单倍率。</summary>
        public const int WeekStartWishlistChanceMultiplier = 1021;

        /// <summary>第 3、6、9 回合结束时获得的愿望单数量。</summary>
        public const int MilestoneWeekEndWishlistReward = 1022;

        /// <summary>Bug 低于 40 时，每回合获得的愿望单数量。</summary>
        public const int LowBugWeeklyWishlistReward = 1023;

        /// <summary>愿望单增长量提高百分比。</summary>
        public const int WishlistGrowthPercentBonus = 1024;

        /// <summary>每回合结束时画面值高于 60 时，愿望单增长量提高量。</summary>
        public const int HighVisualWeekEndWishlistGrowthBonus = 1025;

        /// <summary>同一回合在三个房间都消耗行动点时，愿望单增长量提高量。</summary>
        public const int AllRoomsSameWeekWishlistGrowthBonus = 1026;

        /// <summary>每回合可消费的固定强化费用；为 0 时不弹出选择。</summary>
        public const int WeeklyFixedUpgradeCost = 1027;

        /// <summary>每周结算时应用的 Bug 值变化量。</summary>
        public const int WeeklyBugDelta = 1028;

        /// <summary>每周结算时应用的画面值变化量。</summary>
        public const int WeeklyVisualDelta = 1029;

        /// <summary>每周结算时应用的氛围值变化量。</summary>
        public const int WeeklyAtmosphereDelta = 1030;

        /// <summary>程序房间消耗 1 点行动点时，Bug 变化随机下限。</summary>
        public const int ProgramOneActionBugDeltaMin = 1031;

        /// <summary>程序房间消耗 1 点行动点时，Bug 变化随机上限。</summary>
        public const int ProgramOneActionBugDeltaMax = 1032;

        /// <summary>程序房间消耗 2 点行动点时，Bug 变化随机下限。</summary>
        public const int ProgramTwoActionBugDeltaMin = 1033;

        /// <summary>程序房间消耗 2 点行动点时，Bug 变化随机上限。</summary>
        public const int ProgramTwoActionBugDeltaMax = 1034;

        /// <summary>美术房间消耗 1 点行动点时，画面变化随机下限。</summary>
        public const int ArtOneActionVisualDeltaMin = 1035;

        /// <summary>美术房间消耗 1 点行动点时，画面变化随机上限。</summary>
        public const int ArtOneActionVisualDeltaMax = 1036;

        /// <summary>美术房间消耗 2 点行动点时，画面变化随机下限。</summary>
        public const int ArtTwoActionVisualDeltaMin = 1037;

        /// <summary>美术房间消耗 2 点行动点时，画面变化随机上限。</summary>
        public const int ArtTwoActionVisualDeltaMax = 1038;

        /// <summary>音效房间消耗 1 点行动点时，氛围变化随机下限。</summary>
        public const int AudioOneActionAtmosphereDeltaMin = 1039;

        /// <summary>音效房间消耗 1 点行动点时，氛围变化随机上限。</summary>
        public const int AudioOneActionAtmosphereDeltaMax = 1040;

        /// <summary>音效房间消耗 2 点行动点时，氛围变化随机下限。</summary>
        public const int AudioTwoActionAtmosphereDeltaMin = 1041;

        /// <summary>音效房间消耗 2 点行动点时，氛围变化随机上限。</summary>
        public const int AudioTwoActionAtmosphereDeltaMax = 1042;
    }
}
