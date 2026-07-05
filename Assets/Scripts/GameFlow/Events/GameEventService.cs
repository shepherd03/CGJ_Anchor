using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Anchor.Character.Attributes;
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
        private readonly List<EventRow> mEligibleWeekEvents = new();
        private readonly List<EventRow> mCurrentWeekEvents = new();
        private readonly ReadOnlyCollection<EventRow> mReadOnlyCurrentWeekEvents;
        private readonly IExtractionRandom mRandom;
        private readonly GameFlowSettings mDefaultSettings;
        private int mCurrentEventIndex;

        public GameEventService(IEnumerable<EventRow> eventRows, IExtractionRandom random)
            : this(eventRows, null, random)
        {
        }

        public GameEventService(IEnumerable<EventRow> eventRows, GameFlowSettings defaultSettings = null, IExtractionRandom random = null)
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
            mDefaultSettings = defaultSettings ?? new GameFlowSettings();
            mReadOnlyCurrentWeekEvents = mCurrentWeekEvents.AsReadOnly();
        }

        public IReadOnlyList<EventRow> CurrentWeekEvents => mReadOnlyCurrentWeekEvents;

        public EventRow CurrentEvent =>
            mCurrentEventIndex >= 0 && mCurrentEventIndex < mCurrentWeekEvents.Count
                ? mCurrentWeekEvents[mCurrentEventIndex]
                : null;

        public bool HasPendingEvent => CurrentEvent != null;
        public int PendingEventCount => Math.Max(0, mCurrentWeekEvents.Count - mCurrentEventIndex);

        /// <summary>
        /// 清空新一局不该继承的事件抽取缓存。
        /// </summary>
        public void ResetForNewRun()
        {
            ClearCurrentWeekEvents();
            mEligibleWeekEvents.Clear();
        }

        /// <summary>
        /// 清空当前周待处理事件。
        /// </summary>
        public void ClearCurrentWeekEvents()
        {
            mCurrentWeekEvents.Clear();
            mCurrentEventIndex = 0;
        }

        public IReadOnlyList<EventRow> RollWeekStartEvents(GameFlowBlackboard blackboard, GameFlowSettings settings = null)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            settings = settings ?? mDefaultSettings;
            ClearCurrentWeekEvents();
            mEligibleWeekEvents.Clear();

            foreach (var row in mEventRows)
            {
                if (row.Id <= 0 || !IsTriggerConditionMet(blackboard, row))
                {
                    continue;
                }

                mEligibleWeekEvents.Add(row);

                if (RollRatio(row))
                {
                    mCurrentWeekEvents.Add(row);
                }
            }

            ApplyPityGuarantee(blackboard, settings);
            ShuffleCurrentWeekEvents();
            ApplyWeeklyEventLimit(settings);
            blackboard.RecordWeekStartEventRoll(mCurrentWeekEvents.Count);
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

        private void ApplyPityGuarantee(GameFlowBlackboard blackboard, GameFlowSettings settings)
        {
            if (mCurrentWeekEvents.Count > 0 ||
                settings.GuaranteedEventAfterEmptyWeeks <= 0 ||
                blackboard.ConsecutiveWeeksWithoutWeekStartEvent < settings.GuaranteedEventAfterEmptyWeeks)
            {
                return;
            }

            var guaranteedEvent = DrawGuaranteedEvent();
            if (guaranteedEvent != null)
            {
                mCurrentWeekEvents.Add(guaranteedEvent);
            }
        }

        private EventRow DrawGuaranteedEvent()
        {
            double totalWeight = 0d;
            for (var i = 0; i < mEligibleWeekEvents.Count; i++)
            {
                var row = mEligibleWeekEvents[i];
                ValidateRatio(row);
                if (row.Ratio > 0f)
                {
                    totalWeight += row.Ratio;
                }
            }

            if (totalWeight <= 0d)
            {
                return null;
            }

            var roll = mRandom.NextNormalizedValue();
            if (float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f || roll >= 1f)
            {
                throw new InvalidOperationException($"Event random source returned {roll}. Expected a finite value in [0, 1).");
            }

            var remaining = roll * totalWeight;
            EventRow lastDrawable = null;
            for (var i = 0; i < mEligibleWeekEvents.Count; i++)
            {
                var row = mEligibleWeekEvents[i];
                if (row.Ratio <= 0f)
                {
                    continue;
                }

                lastDrawable = row;
                if (remaining < row.Ratio)
                {
                    return row;
                }

                remaining -= row.Ratio;
            }

            return lastDrawable;
        }

        private void ShuffleCurrentWeekEvents()
        {
            for (var i = mCurrentWeekEvents.Count - 1; i > 0; i--)
            {
                var roll = mRandom.NextNormalizedValue();
                if (float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f || roll >= 1f)
                {
                    throw new InvalidOperationException($"Event random source returned {roll}. Expected a finite value in [0, 1).");
                }

                var swapIndex = (int)(roll * (i + 1));
                var temporary = mCurrentWeekEvents[i];
                mCurrentWeekEvents[i] = mCurrentWeekEvents[swapIndex];
                mCurrentWeekEvents[swapIndex] = temporary;
            }
        }

        private void ApplyWeeklyEventLimit(GameFlowSettings settings)
        {
            var maxEvents = Math.Max(0, settings.MaxWeekStartEvents);
            if (mCurrentWeekEvents.Count > maxEvents)
            {
                mCurrentWeekEvents.RemoveRange(maxEvents, mCurrentWeekEvents.Count - maxEvents);
            }
        }

        private static void ValidateRatio(EventRow row)
        {
            if (float.IsNaN(row.Ratio) || float.IsInfinity(row.Ratio) || row.Ratio < 0f || row.Ratio > 1f)
            {
                throw new InvalidOperationException($"Event {row.Id} has invalid ratio: {row.Ratio}.");
            }
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
                RequireReadableAttributePair(blackboard, row, fieldName, pairs[i], i, out var attributeId, out var threshold);
                if (!predicate(blackboard.GetAttributeValue(attributeId), threshold))
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
                RequireWritableAttributePair(blackboard, row, fieldName, effects[i], i, out var attributeId, out var delta);
                if (attributeId == CharacterAttributeIds.Wishlist)
                {
                    blackboard.AddWeeklyWishlistFlatModifier(GetEventWishlistSourceName(row, chooseYes), delta);
                    continue;
                }

                blackboard.PlayerAttributes.Add(attributeId, delta);
            }

            return effects.Length;
        }

        private static string GetEventWishlistSourceName(EventRow row, bool chooseYes)
        {
            if (row == null)
            {
                return chooseYes ? "周事件愿望单奖励（Y）" : "周事件愿望单奖励（N）";
            }

            var title = string.IsNullOrWhiteSpace(row.Title) ? $"事件 {row.Id}" : row.Title;
            return chooseYes ? $"事件：{title}（Y）" : $"事件：{title}（N）";
        }

        private static void RequireReadableAttributePair(
            GameFlowBlackboard blackboard,
            EventRow row,
            string fieldName,
            int[] pair,
            int index,
            out int attributeId,
            out int value)
        {
            RequireAttributePairShape(row, fieldName, pair, index, out attributeId, out value);

            if (!blackboard.CanReadAttribute(attributeId))
            {
                throw new InvalidOperationException(
                    $"Event {row.Id} field '{fieldName}' item {index} uses unknown readable attribute id: {attributeId}.");
            }
        }

        private static void RequireWritableAttributePair(
            GameFlowBlackboard blackboard,
            EventRow row,
            string fieldName,
            int[] pair,
            int index,
            out int attributeId,
            out int value)
        {
            RequireAttributePairShape(row, fieldName, pair, index, out attributeId, out value);

            if (!blackboard.CanWriteAttribute(attributeId))
            {
                throw new InvalidOperationException(
                    $"Event {row.Id} field '{fieldName}' item {index} uses unknown writable attribute id: {attributeId}.");
            }
        }

        private static void RequireAttributePairShape(
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
        }
    }
}
