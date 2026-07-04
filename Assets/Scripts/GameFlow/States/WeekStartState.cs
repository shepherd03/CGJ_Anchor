using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class WeekStartState : GameFlowStateBase
    {
        public WeekStartState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.WeekStart);
            mBlack.BeginWeek();
            Controller.RollWeekStartGameEvents();
            mFSM.Change(Controller.HasPendingWeekGameEvent ? GameFlowState.WeekEvent : GameFlowState.WeekAction);
        }
    }
}
