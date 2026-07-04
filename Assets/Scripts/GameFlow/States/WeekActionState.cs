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
                TryAutoAllocate(GameDevelopmentTrack.Program);
                TryAutoAllocate(GameDevelopmentTrack.Art);
                TryAutoAllocate(GameDevelopmentTrack.Audio);
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

        private void TryAutoAllocate(GameDevelopmentTrack track)
        {
            if (mBlack.RemainingActionPoints <= 0)
            {
                return;
            }

            mBlack.TryAllocate(track, mBlack.RemainingActionPoints >= 2 ? 2 : 1);
        }
    }
}
