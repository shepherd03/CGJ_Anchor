using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class MonthStartState : GameFlowStateBase
    {
        public MonthStartState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.MonthStart);
            var nextMonth = mBlack.MonthIndex + 1;
            var definition = Controller.Definitions.GetMonth(nextMonth);
            mBlack.BeginMonth(definition);
            mFSM.Change(GameFlowState.BudgetShop);
        }
    }
}
