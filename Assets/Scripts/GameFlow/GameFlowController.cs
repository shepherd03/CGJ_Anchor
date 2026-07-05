using System.Collections.Generic;
using Anchor.GameFlow.States;
using Anchor.Character.Attributes;
using Anchor.GameFlow.Buffs;
using Anchor.GameFlow.Events;
using YokiFrame;

using BuffRow = Anchor.Config.game.buff;
using EventRow = Anchor.Config.game.gameEvent;

namespace Anchor.GameFlow
{
    public sealed class GameFlowController
    {
        public const int DefaultBudgetShopBuffOfferCount = 3;

        private readonly FSM<GameFlowState> mFsm;

        public GameFlowController(
            CharacterAttributeCatalog attributeCatalog,
            GameFlowSettings settings = null,
            bool autoAdvanceInteractiveStates = false,
            IEnumerable<BuffRow> buffRows = null,
            IEnumerable<EventRow> eventRows = null)
        {
            Settings = settings ?? new GameFlowSettings();
            AutoAdvanceInteractiveStates = autoAdvanceInteractiveStates;
            Blackboard = new GameFlowBlackboard(attributeCatalog);
            Definitions = new GameFlowDefinitionProvider(Settings);
            ResolveService = new GameFlowResolveService();
            BuffShop = new BuffShopService(buffRows);
            GameEvents = new GameEventService(eventRows, Settings);
            mFsm = new FSM<GameFlowState>("GameFlowFSM");

            mFsm.Add(GameFlowState.NewGame, new NewGameState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.MonthStart, new MonthStartState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.BudgetShop, new BudgetShopState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.WeekStart, new WeekStartState(mFsm, Blackboard, this));
            mFsm.Add(GameFlowState.WeekEvent, new WeekEventState(mFsm, Blackboard, this));
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
        public GameEventService GameEvents { get; }
        public bool AutoAdvanceInteractiveStates { get; }
        public GameFlowState CurrentState => mFsm.CurEnum;
        public MachineState MachineState => mFsm.MachineState;
        public IReadOnlyList<BuffRow> CurrentBudgetShopBuffOffers => BuffShop.CurrentOffers;
        public IReadOnlyList<EventRow> CurrentWeekGameEvents => GameEvents.CurrentWeekEvents;
        public EventRow CurrentWeekGameEvent => GameEvents.CurrentEvent;
        public bool HasPendingWeekGameEvent => GameEvents.HasPendingEvent;

        public void StartNewGame()
        {
            if (mFsm.MachineState != MachineState.End)
            {
                mFsm.End();
            }

            GameEvents.ClearCurrentWeekEvents();
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

        public IReadOnlyList<EventRow> RollWeekStartGameEvents()
        {
            return GameEvents.RollWeekStartEvents(Blackboard, Settings);
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

        public bool TrySpendProgramOneActionPoint()
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Program, 1);
        }

        public bool TrySpendProgramTwoActionPoints()
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Program, 2);
        }

        public bool TrySpendArtOneActionPoint()
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Art, 1);
        }

        public bool TrySpendArtTwoActionPoints()
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Art, 2);
        }

        public bool TrySpendAudioOneActionPoint()
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Audio, 1);
        }

        public bool TrySpendAudioTwoActionPoints()
        {
            return TryAllocateActionPoints(GameDevelopmentTrack.Audio, 2);
        }

        public void FinishWeekAction()
        {
            mFsm.SendMessage(new FinishWeekActionMessage());
        }

        public bool ChooseWeekGameEvent(bool chooseYes)
        {
            if (CurrentState != GameFlowState.WeekEvent || !HasPendingWeekGameEvent)
            {
                return false;
            }

            mFsm.SendMessage(new ResolveWeekGameEventMessage(chooseYes));
            return true;
        }

        public bool ChooseWeekGameEventYes()
        {
            return ChooseWeekGameEvent(true);
        }

        public bool ChooseWeekGameEventNo()
        {
            return ChooseWeekGameEvent(false);
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

        internal void NotifyWeekGameEventTriggered(EventRow eventRow)
        {
            EventKit.Type.Send(new WeekGameEventTriggeredEvent(
                Blackboard,
                eventRow,
                GameEvents.PendingEventCount,
                GameEvents.CurrentWeekEvents.Count));
        }

        internal bool TryResolveCurrentWeekGameEvent(bool chooseYes, out GameEventResolveResult result)
        {
            if (!GameEvents.TryResolveCurrentEvent(Blackboard, chooseYes, out result))
            {
                return false;
            }

            NotifyWeekGameEventResolved(result);
            return true;
        }

        internal void NotifyWeekGameEventResolved(GameEventResolveResult result)
        {
            EventKit.Type.Send(new WeekGameEventResolvedEvent(Blackboard, result));
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
