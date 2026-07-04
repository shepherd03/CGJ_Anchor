using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Orange.Extraction;

using EventRow = Anchor.Config.game.gameEvent;

namespace Anchor.GameFlow.Events
{
    public readonly struct GameEventResolveResult
    {
        public readonly EventRow Event;
        public readonly bool ChooseYes;
        public readonly int AppliedEffectCount;

        public int EventId => Event != null ? Event.Id : 0;

        public GameEventResolveResult(EventRow eventRow, bool chooseYes, int appliedEffectCount)
        {
            Event = eventRow;
            ChooseYes = chooseYes;
            AppliedEffectCount = appliedEffectCount;
        }
    }

    public sealed class GameEventService
    {
        private readonly List<EventRow> mEventRows = new();
        private readonly List<EventRow> mCurrentWeekEvents = new();
        private readonly ReadOnlyCollection<EventRow> mReadOnlyCurrentWeekEvents;
        private readonly IExtractionRandom mRandom;
        private int mCurrentEventIndex;

        public GameEventService(IEnumerable<EventRow> eventRows, IExtractionRandom random = null)
        {
            if (eventRows != null)
            {
                foreach (var row in eventRows)
                {
                    if (row != null)
                    {
                        mEventRows.Add(row);
                    }
                }
            }

            mRandom = random ?? new SystemExtractionRandom();
            mReadOnlyCurrentWeekEvents = mCurrentWeekEvents.AsReadOnly();
        }

        public IReadOnlyList<EventRow> CurrentWeekEvents => mReadOnlyCurrentWeekEvents;

        public EventRow CurrentEvent =>
            mCurrentEventIndex >= 0 && mCurrentEventIndex < mCurrentWeekEvents.Count
                ? mCurrentWeekEvents[mCurrentEventIndex]
                : null;

        public bool HasPendingEvent => CurrentEvent != null;
        public int PendingEventCount => Math.Max(0, mCurrentWeekEvents.Count - mCurrentEventIndex);

        public void ClearCurrentWeekEvents()
        {
            mCurrentWeekEvents.Clear();
            mCurrentEventIndex = 0;
        }

        public IReadOnlyList<EventRow> RollWeekStartEvents(GameFlowBlackboard blackboard)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            ClearCurrentWeekEvents();

            foreach (var row in mEventRows)
            {
                if (row.Id <= 0 || !IsTriggerConditionMet(blackboard, row) || !RollRatio(row))
                {
                    continue;
                }

                mCurrentWeekEvents.Add(row);
            }

            return CurrentWeekEvents;
        }

        public bool TryResolveCurrentEvent(
            GameFlowBlackboard blackboard,
            bool chooseYes,
            out GameEventResolveResult result)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            var row = CurrentEvent;
            if (row == null)
            {
                result = default;
                return false;
            }

            var effects = chooseYes ? row.YesEffects : row.NoEffects;
            var appliedEffectCount = ApplyEffects(blackboard, row, chooseYes, effects);
            blackboard.RecordTriggeredEvent(row.Id);
            mCurrentEventIndex++;

            result = new GameEventResolveResult(row, chooseYes, appliedEffectCount);
            return true;
        }

        private static bool IsTriggerConditionMet(GameFlowBlackboard blackboard, EventRow row)
        {
            return AreConditionPairsMet(
                    blackboard,
                    row,
                    row.TriggerGreaterOrEqualConditions,
                    "triggerGreaterOrEqualConditions",
                    (current, threshold) => current >= threshold)
                && AreConditionPairsMet(
                    blackboard,
                    row,
                    row.TriggerLessThanConditions,
                    "triggerLessThanConditions",
                    (current, threshold) => current < threshold);
        }

        private bool RollRatio(EventRow row)
        {
            if (float.IsNaN(row.Ratio) || float.IsInfinity(row.Ratio) || row.Ratio < 0f || row.Ratio > 1f)
            {
                throw new InvalidOperationException($"Event {row.Id} has invalid ratio: {row.Ratio}.");
            }

            if (row.Ratio <= 0f)
            {
                return false;
            }

            if (row.Ratio >= 1f)
            {
                return true;
            }

            var roll = mRandom.NextNormalizedValue();
            if (float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f || roll >= 1f)
            {
                throw new InvalidOperationException($"Event random source returned {roll}. Expected a finite value in [0, 1).");
            }

            return roll < row.Ratio;
        }

        private static bool AreConditionPairsMet(
            GameFlowBlackboard blackboard,
            EventRow row,
            int[][] pairs,
            string fieldName,
            Func<int, int, bool> predicate)
        {
            if (pairs == null || pairs.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < pairs.Length; i++)
            {
                RequireAttributePair(blackboard, row, fieldName, pairs[i], i, out var attributeId, out var threshold);
                if (!predicate(blackboard.PlayerAttributes.Get(attributeId), threshold))
                {
                    return false;
                }
            }

            return true;
        }

        private static int ApplyEffects(
            GameFlowBlackboard blackboard,
            EventRow row,
            bool chooseYes,
            int[][] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                return 0;
            }

            var fieldName = chooseYes ? "yesEffects" : "noEffects";
            for (var i = 0; i < effects.Length; i++)
            {
                RequireAttributePair(blackboard, row, fieldName, effects[i], i, out var attributeId, out var delta);
                blackboard.PlayerAttributes.Add(attributeId, delta);
            }

            return effects.Length;
        }

        private static void RequireAttributePair(
            GameFlowBlackboard blackboard,
            EventRow row,
            string fieldName,
            int[] pair,
            int index,
            out int attributeId,
            out int value)
        {
            if (pair == null || pair.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Event {row.Id} field '{fieldName}' item {index} must be [attributeId, value].");
            }

            attributeId = pair[0];
            value = pair[1];

            if (!blackboard.AttributeCatalog.Contains(attributeId))
            {
                throw new InvalidOperationException(
                    $"Event {row.Id} field '{fieldName}' item {index} uses unknown attribute id: {attributeId}.");
            }
        }
    }
}
