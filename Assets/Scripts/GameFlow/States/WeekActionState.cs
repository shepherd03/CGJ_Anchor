using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class WeekActionState : GameFlowStateBase
    {
        public WeekActionState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.WeekAction);

            if (!Controller.AutoAdvanceInteractiveStates)
            {
                return;
            }

            var points = mBlack.RemainingActionPoints;
            if (points > 0)
            {
                mBlack.TryAllocate(GameDevelopmentTrack.Program, points / 2);
                mBlack.TryAllocate(GameDevelopmentTrack.Art, (points + 1) / 2);
            }

            mFSM.Change(GameFlowState.WeekResolve);
        }

        protected override void OnMessage<TMsg>(TMsg message)
        {
            switch (message)
            {
                case AllocateActionPointsMessage allocate:
                    mBlack.TryAllocate(allocate.Track, allocate.Points);
                    break;
                case FinishWeekActionMessage:
                    mFSM.Change(GameFlowState.WeekResolve);
                    break;
            }
        }
    }
}
