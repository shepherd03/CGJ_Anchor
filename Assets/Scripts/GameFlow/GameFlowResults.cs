namespace Anchor.GameFlow
{
    public readonly struct WeekResolveResult
    {
        public readonly int MonthIndex;
        public readonly int WeekIndex;
        public readonly int VisualDelta;
        public readonly int AtmosphereDelta;
        public readonly int BugDelta;
        public readonly int CoinDelta;
        public readonly int WishlistDelta;
        public readonly int EventId;
        public readonly string Summary;

        public WeekResolveResult(
            int monthIndex,
            int weekIndex,
            int visualDelta,
            int atmosphereDelta,
            int bugDelta,
            int coinDelta,
            int wishlistDelta,
            int eventId,
            string summary)
        {
            MonthIndex = monthIndex;
            WeekIndex = weekIndex;
            VisualDelta = visualDelta;
            AtmosphereDelta = atmosphereDelta;
            BugDelta = bugDelta;
            CoinDelta = coinDelta;
            WishlistDelta = wishlistDelta;
            EventId = eventId;
            Summary = summary;
        }
    }

    public readonly struct MonthSettlementResult
    {
        public readonly int MonthIndex;
        public readonly MonthSettlementType SettlementType;
        public readonly int WishlistDelta;
        public readonly int CoinDelta;
        public readonly int BugDelta;
        public readonly string Summary;

        public MonthSettlementResult(
            int monthIndex,
            MonthSettlementType settlementType,
            int wishlistDelta,
            int coinDelta,
            int bugDelta,
            string summary)
        {
            MonthIndex = monthIndex;
            SettlementType = settlementType;
            WishlistDelta = wishlistDelta;
            CoinDelta = coinDelta;
            BugDelta = bugDelta;
            Summary = summary;
        }
    }

    public readonly struct EndingResult
    {
        public readonly string EndingId;
        public readonly string DisplayName;
        public readonly string Summary;

        public EndingResult(string endingId, string displayName, string summary)
        {
            EndingId = endingId;
            DisplayName = displayName;
            Summary = summary;
        }
    }
}
