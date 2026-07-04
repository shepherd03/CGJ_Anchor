using System.Collections.Generic;
using Anchor.GameFlow.Buffs;

using BuffRow = Anchor.Config.game.buff;

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

    public readonly struct BudgetShopBuffOffersRefreshedEvent
    {
        public readonly GameFlowBlackboard Blackboard;
        public readonly IReadOnlyList<BuffRow> Offers;

        public BudgetShopBuffOffersRefreshedEvent(GameFlowBlackboard blackboard, IReadOnlyList<BuffRow> offers)
        {
            Blackboard = blackboard;
            Offers = offers;
        }
    }

    public readonly struct BuffPurchasedEvent
    {
        public readonly GameFlowBlackboard Blackboard;
        public readonly BuffPurchaseResult Result;

        public BuffPurchasedEvent(GameFlowBlackboard blackboard, BuffPurchaseResult result)
        {
            Blackboard = blackboard;
            Result = result;
        }
    }
}
