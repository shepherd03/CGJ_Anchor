using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class MonthSettlementState : GameFlowStateBase
    {
        public MonthSettlementState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.MonthSettlement);
            var result = Controller.ResolveService.ResolveMonth(mBlack);
            mBlack.ApplyMonthSettlement(result);
            Controller.NotifyMonthSettled(result);

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
            if (mBlack.MonthIndex >= Controller.Definitions.MonthCount)
            {
                mFSM.Change(GameFlowState.Ending);
                return;
            }

            mFSM.Change(GameFlowState.MonthStart);
        }
    }
}
