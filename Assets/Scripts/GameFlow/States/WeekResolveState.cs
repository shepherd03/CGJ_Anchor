using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class WeekResolveState : GameFlowStateBase
    {
        public WeekResolveState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.WeekResolve);
            var result = Controller.ResolveService.ResolveWeek(mBlack);
            mBlack.ApplyWeekResult(result);
            Controller.NotifyWeekResolved(result);

            if (Controller.AutoAdvanceInteractiveStates)
            {
                Continue();
            }
        }

        protected override void OnMessage<TMsg>(TMsg message)
        {
            if (message is ContinueFlowMessage)
            {
                Continue();
            }
        }

        private void Continue()
        {
            if (mBlack.WeekIndex >= mBlack.CurrentMonth.WeekCount)
            {
                mFSM.Change(GameFlowState.MonthSettlement);
                return;
            }

            mFSM.Change(GameFlowState.WeekStart);
        }
    }
}
