using YokiFrame;

namespace Anchor.GameFlow.States
{
    public abstract class GameFlowStateBase : AbstractState<GameFlowState, GameFlowBlackboard>
    {
        protected readonly GameFlowController Controller;

        protected GameFlowStateBase(
            FSM<GameFlowState> fsm,
            GameFlowBlackboard blackboard,
            GameFlowController controller)
            : base(fsm, blackboard)
        {
            Controller = controller;
        }

        protected void NotifyEntered(GameFlowState state)
        {
            Controller.NotifyStateChanged(state);
        }
    }
}
