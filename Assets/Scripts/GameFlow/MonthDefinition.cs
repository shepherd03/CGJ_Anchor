namespace Anchor.GameFlow
{
    public sealed class MonthDefinition
    {
        public int MonthIndex { get; }
        public string DisplayName { get; }
        public MonthSettlementType SettlementType { get; }
        public int WeekCount { get; }

        public MonthDefinition(
            int monthIndex,
            string displayName,
            MonthSettlementType settlementType,
            int weekCount)
        {
            MonthIndex = monthIndex;
            DisplayName = displayName;
            SettlementType = settlementType;
            WeekCount = weekCount;
        }
    }
}
