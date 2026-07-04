using Anchor.GameFlow.States;
using Anchor.Character.Attributes;
using YokiFrame;

namespace Anchor.GameFlow
{
    public sealed class GameFlowController
    {
        private readonly FSM<GameFlowState> mFsm;

        public GameFlowController(
            CharacterAttributeCatalog attributeCatalog,
            GameFlowSettings settings = null,
            bool autoAdvanceInteractiveStates = false)
        {
            Settings = settings ?? new GameFlowSettings();
            AutoAdvanceInteractiveStates = autoAdvanceInteractiveStates;
            Blackboard = new GameFlowBlackboard(attributeCatalog);
            Definitions = new GameFlowDefinitionProvider(Settings);
            ResolveService = new GameFlowResolveService();
            mFsm = new FSM<GameFlowState>("GameFlowFSM");

            mFsm.Add(GameFlowState.NewGame, new NewGameState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.MonthStart, new MonthStartState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.BudgetShop, new BudgetShopState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.WeekStart, new WeekStartState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.WeekAction, new WeekActionState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.WeekResolve, new WeekResolveState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.MonthSettlement, new MonthSettlementState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.Ending, new EndingState(mFsm, Blackboard, this));
        }

        public GameFlowSettings Settings { get; }
        public GameFlowBlackboard Blackboard { get; }
        public GameFlowDefinitionProvider Definitions { get; }
        public GameFlowResolveService ResolveService { get; }
        public bool AutoAdvanceInteractiveStates { get; }
        public GameFlowState CurrentState => mFsm.CurEnum;
        public MachineState MachineState => mFsm.MachineState;

        public void StartNewGame()
        {
            if (mFsm.MachineState != MachineState.End)
            {
                mFsm.End();
            }

            mFsm.Start(GameFlowState.NewGame);
        }

        public void Update()
        {
            mFsm.Update();
        }

        public void Stop()
        {
            mFsm.End();
        }

        public void ConfirmBudgetShop()
        {
            mFsm.SendMessage(new ConfirmBudgetShopMessage());
        }

        public bool TryAllocateActionPoints(GameDevelopmentTrack track, int points)
        {
            if (CurrentState != GameFlowState.WeekAction)
            {
                return false;
            }

            var before = Blackboard.RemainingActionPoints;
            mFsm.SendMessage(new AllocateActionPointsMessage(track, points));
            return Blackboard.RemainingActionPoints != before || points <= 0;
        }

        public void FinishWeekAction()
        {
            mFsm.SendMessage(new FinishWeekActionMessage());
        }

        public void Continue()
        {
            mFsm.SendMessage(new ContinueFlowMessage());
        }

        internal void NotifyStateChanged(GameFlowState state)
        {
            EventKit.Type.Send(new GameFlowStateChangedEvent(state, Blackboard));
        }

        internal void NotifyWeekResolved(WeekResolveResult result)
        {
            EventKit.Type.Send(new WeekResolvedEvent(Blackboard, result));
        }

        internal void NotifyMonthSettled(MonthSettlementResult result)
        {
            EventKit.Type.Send(new MonthSettledEvent(Blackboard, result));
        }

        internal void NotifyEndingSelected(EndingResult result)
        {
            EventKit.Type.Send(new GameEndingSelectedEvent(Blackboard, result));
        }
    }
}
