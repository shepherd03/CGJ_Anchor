namespace Anchor.GameFlow
{
    public sealed class GameFlowSettings
    {
        public int TotalMonths { get; set; } = 3;
        public int WeeksPerMonth { get; set; } = 4;
        public int MaxWeekStartEvents { get; set; } = 2;
        public int GuaranteedEventAfterEmptyWeeks { get; set; } = 1;
    }
}
