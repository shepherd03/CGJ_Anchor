using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class NewGameState : GameFlowStateBase
    {
        public NewGameState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.NewGame);
            mBlack.ResetForNewRun(Controller.Settings);
            mFSM.Change(GameFlowState.MonthStart);
        }
    }
}
