namespace Anchor.GameFlow
{
    internal readonly struct ConfirmBudgetShopMessage
    {
    }

    internal readonly struct AllocateActionPointsMessage
    {
        public readonly GameDevelopmentTrack Track;
        public readonly int Points;

        public AllocateActionPointsMessage(GameDevelopmentTrack track, int points)
        {
            Track = track;
            Points = points;
        }
    }

    internal readonly struct FinishWeekActionMessage
    {
    }

    internal readonly struct ContinueFlowMessage
    {
    }
}
