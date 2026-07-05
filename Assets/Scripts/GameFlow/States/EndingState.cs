using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class EndingState : GameFlowStateBase
    {
        public EndingState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.Ending);
            var result = Controller.ResolveService.ResolveEnding(mBlack);
            mBlack.SetEnding(result);
            GameLeaderboardService.RecordGameEnd(mBlack, result);
            Controller.NotifyEndingSelected(result);
        }
    }
}
