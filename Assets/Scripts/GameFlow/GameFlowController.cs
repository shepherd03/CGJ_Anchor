using System.Collections.Generic;
using Anchor.GameFlow.States;
using Anchor.Character.Attributes;
using Anchor.GameFlow.Buffs;
using YokiFrame;

using BuffRow = Anchor.Config.game.buff;

namespace Anchor.GameFlow
{
    public sealed class GameFlowController
    {
        public const int DefaultBudgetShopBuffOfferCount = 6;

        private readonly FSM<GameFlowState> mFsm;

        public GameFlowController(
            CharacterAttributeCatalog attributeCatalog,
            GameFlowSettings settings = null,
            bool autoAdvanceInteractiveStates = false,
            IEnumerable<BuffRow> buffRows = null)
        {
            Settings = settings ?? new GameFlowSettings();
            AutoAdvanceInteractiveStates = autoAdvanceInteractiveStates;
            Blackboard = new GameFlowBlackboard(attributeCatalog);
            Definitions = new GameFlowDefinitionProvider(Settings);
            ResolveService = new GameFlowResolveService();
            BuffShop = new BuffShopService(buffRows);
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
        public BuffShopService BuffShop { get; }
        public bool AutoAdvanceInteractiveStates { get; }
        public GameFlowState CurrentState => mFsm.CurEnum;
        public MachineState MachineState => mFsm.MachineState;
        public IReadOnlyList<BuffRow> CurrentBudgetShopBuffOffers => BuffShop.CurrentOffers;

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

        public IReadOnlyList<BuffRow> RefreshBudgetShopBuffOffers(int count = DefaultBudgetShopBuffOfferCount)
        {
            var offers = BuffShop.RefreshOffers(Blackboard, count);
            NotifyBudgetShopBuffOffersRefreshed(offers);
            return offers;
        }

        public bool CanPurchaseBudgetShopBuff(int buffId)
        {
            return CurrentState == GameFlowState.BudgetShop
                && BuffShop.CanPurchaseOfferedBuff(Blackboard, buffId);
        }

        public bool TryPurchaseBudgetShopBuff(int buffId, out BuffPurchaseResult result)
        {
            if (CurrentState != GameFlowState.BudgetShop)
            {
                result = BuffPurchaseResult.Fail(
                    BuffPurchaseStatus.NotInBudgetShop,
                    buffId,
                    BuffShop.CostAttributeId,
                    0,
                    $"Cannot purchase buff during {CurrentState}.");
                return false;
            }

            if (!BuffShop.TryPurchaseOfferedBuff(Blackboard, buffId, out result))
            {
                return false;
            }

            NotifyBuffPurchased(result);
            return true;
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

        internal void NotifyBudgetShopBuffOffersRefreshed(IReadOnlyList<BuffRow> offers)
        {
            EventKit.Type.Send(new BudgetShopBuffOffersRefreshedEvent(Blackboard, offers));
        }

        internal void NotifyBuffPurchased(BuffPurchaseResult result)
        {
            EventKit.Type.Send(new BuffPurchasedEvent(Blackboard, result));
        }
    }
}
