using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class WeekEventState : GameFlowStateBase
    {
        public WeekEventState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.WeekEvent);
            PresentCurrentEventOrContinue();
        }

        protected override void OnMessage<TMsg>(TMsg message)
        {
            if (message is ResolveWeekGameEventMessage resolve)
            {
                ResolveCurrentEvent(resolve.ChooseYes);
            }
        }

        private void PresentCurrentEventOrContinue()
        {
            while (Controller.HasPendingWeekGameEvent)
            {
                Controller.NotifyWeekGameEventTriggered(Controller.CurrentWeekGameEvent);

                if (!Controller.AutoAdvanceInteractiveStates)
                {
                    return;
                }

                Controller.TryResolveCurrentWeekGameEvent(true, out _);
            }

            mFSM.Change(GameFlowState.WeekAction);
        }

        private void ResolveCurrentEvent(bool chooseYes)
        {
            if (!Controller.TryResolveCurrentWeekGameEvent(chooseYes, out _))
            {
                mFSM.Change(GameFlowState.WeekAction);
                return;
            }

            PresentCurrentEventOrContinue();
        }
    }
}
