namespace Anchor.GameFlow
{
    public readonly struct GameFlowStateChangedEvent
    {
        public readonly GameFlowState State;
        public readonly GameFlowBlackboard Blackboard;

        public GameFlowStateChangedEvent(GameFlowState state, GameFlowBlackboard blackboard)
        {
            State = state;
            Blackboard = blackboard;
        }
    }

    public readonly struct WeekResolvedEvent
    {
        public readonly GameFlowBlackboard Blackboard;
        public readonly WeekResolveResult Result;

        public WeekResolvedEvent(GameFlowBlackboard blackboard, WeekResolveResult result)
        {
            Blackboard = blackboard;
            Result = result;
        }
    }

    public readonly struct MonthSettledEvent
    {
        public readonly GameFlowBlackboard Blackboard;
        public readonly MonthSettlementResult Result;

        public MonthSettledEvent(GameFlowBlackboard blackboard, MonthSettlementResult result)
        {
            Blackboard = blackboard;
            Result = result;
        }
    }

    public readonly struct GameEndingSelectedEvent
    {
        public readonly GameFlowBlackboard Blackboard;
        public readonly EndingResult Result;

        public GameEndingSelectedEvent(GameFlowBlackboard blackboard, EndingResult result)
        {
            Blackboard = blackboard;
            Result = result;
        }
    }
}
