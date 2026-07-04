using YokiFrame;

namespace Anchor.GameFlow.States
{
    public sealed class BudgetShopState : GameFlowStateBase
    {
        public BudgetShopState(FSM<GameFlowState> fsm, GameFlowBlackboard blackboard, GameFlowController controller)
            : base(fsm, blackboard, controller)
        {
        }

        protected override void OnEnter()
        {
            NotifyEntered(GameFlowState.BudgetShop);
            Controller.RefreshBudgetShopBuffOffers();

            if (Controller.AutoAdvanceInteractiveStates)
            {
                mFSM.Change(GameFlowState.WeekStart);
            }
        }

        protected override void OnMessage<TMsg>(TMsg message)
        {
            if (message is ConfirmBudgetShopMessage)
            {
                mFSM.Change(GameFlowState.WeekStart);
            }
        }
    }
}
