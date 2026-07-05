using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Anchor.GameFlow
{
    public sealed class GameFlowResolveService
    {
        private const int EndingQualityThreshold = 60;
        private const int EndingWishlistThreshold = 4_000_000;

        public WeekResolveResult ResolveWeek(GameFlowBlackboard blackboard)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            var program = blackboard.GetAllocatedPoints(GameDevelopmentTrack.Program);
            var art = blackboard.GetAllocatedPoints(GameDevelopmentTrack.Art);
            var audio = blackboard.GetAllocatedPoints(GameDevelopmentTrack.Audio);

            var visualDelta = blackboard.WeeklyVisualDelta;
            var atmosphereDelta = blackboard.WeeklyAtmosphereDelta;
            var bugDelta = blackboard.WeeklyBugDelta;
            var coinDelta = -(program + art + audio) * 35;
            var wishlistModifiers = BuildWeeklyWishlistModifiers(blackboard, program, art, audio, visualDelta, bugDelta);
            var wishlistStartValue = blackboard.WishlistCount;
            var wishlistDelta = ResolveWishlistModifiers(
                wishlistModifiers,
                wishlistStartValue,
                out var wishlistEndValue,
                out var resolvedWishlistModifiers);
            var resolvedQualityScore = GameFlowBlackboard.CalculateQualityScore(
                blackboard.VisualScore + visualDelta,
                blackboard.AtmosphereScore + atmosphereDelta,
                Math.Max(0, blackboard.BugScore + bugDelta));

            var eventId = 0;
            if (blackboard.RemainingActionPoints > 0)
            {
                coinDelta += blackboard.RemainingActionPoints * 20;
            }

            var summary =
                $"第 {blackboard.MonthIndex} 月第 {blackboard.WeekIndex} 周结算：质量分 {resolvedQualityScore}，Bug 值 {bugDelta:+0;-0;0}，愿望单 +{wishlistDelta}，金币 {coinDelta:+0;-0;0}";
            return new WeekResolveResult(
                blackboard.MonthIndex,
                blackboard.WeekIndex,
                visualDelta,
                atmosphereDelta,
                bugDelta,
                coinDelta,
                wishlistDelta,
                wishlistStartValue,
                wishlistEndValue,
                resolvedWishlistModifiers,
                eventId,
                summary);
        }

        public MonthSettlementResult ResolveMonth(GameFlowBlackboard blackboard)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            var month = blackboard.CurrentMonth ?? throw new InvalidOperationException("Cannot settle month before BeginMonth.");
            var qualityFactor = blackboard.QualityScore;
            var rawWishlistDelta = blackboard.WeeklyWishlistGrowth + (int)Math.Round(qualityFactor * GetWishlistMultiplier(month.SettlementType));
            var wishlistGrowthMultiplier = Math.Max(0f, 1f + blackboard.WishlistGrowthPercentBonus / 100f);
            var wishlistDelta = (int)Math.Round(rawWishlistDelta * wishlistGrowthMultiplier);
            var coinDelta = 0;
            var bugDelta = month.SettlementType == MonthSettlementType.ClosedBeta ? -Math.Min(blackboard.BugScore, 8) : 0;
            var summary = $"{month.DisplayName}{GetSettlementTypeName(month.SettlementType)}：愿望单 +{wishlistDelta}";

            return new MonthSettlementResult(
                blackboard.MonthIndex,
                month.SettlementType,
                wishlistDelta,
                coinDelta,
                bugDelta,
                summary);
        }

        public EndingResult ResolveEnding(GameFlowBlackboard blackboard)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            bool hasHighQuality = blackboard.QualityScore > EndingQualityThreshold;
            bool hasManyWishlists = blackboard.WishlistCount > EndingWishlistThreshold;

            if (!hasHighQuality && !hasManyWishlists)
            {
                return new EndingResult("unnoticed", "无人问津", "质量分低于上线标准，愿望单也没有形成足够声量。");
            }

            if (hasHighQuality && !hasManyWishlists)
            {
                return new EndingResult("boutique", "小众精品", "游戏质量不错，但愿望单规模仍偏小。");
            }

            if (!hasHighQuality)
            {
                return new EndingResult("crash", "暴死结局", "愿望单很多，但质量分没有撑住正式上线。");
            }

            return new EndingResult("hit", "好评如潮", "质量和愿望单都达到了爆发线。");
        }

        private static float GetWishlistMultiplier(MonthSettlementType type)
        {
            return type switch
            {
                MonthSettlementType.PvRelease => 1.8f,
                MonthSettlementType.ClosedBeta => 2.4f,
                MonthSettlementType.PublicRelease => 3.0f,
                MonthSettlementType.FinalRelease => 4.0f,
                _ => 1f
            };
        }

        private static string GetSettlementTypeName(MonthSettlementType type)
        {
            return type switch
            {
                MonthSettlementType.PvRelease => "发布 PV",
                MonthSettlementType.ClosedBeta => "内测",
                MonthSettlementType.PublicRelease => "公测",
                MonthSettlementType.FinalRelease => "正式发布",
                _ => type.ToString()
            };
        }

        private static List<WishlistModifier> BuildWeeklyWishlistModifiers(
            GameFlowBlackboard blackboard,
            int program,
            int art,
            int audio,
            int visualDelta,
            int bugDelta)
        {
            var modifiers = new List<WishlistModifier>(blackboard.WeeklyWishlistModifiers);
            var sortOrder = 10000;

            if (blackboard.TotalWeekIndex == 3 ||
                blackboard.TotalWeekIndex == 6 ||
                blackboard.TotalWeekIndex == 9)
            {
                AddFlatModifier(modifiers, "第 3/6/9 周热度奖励", blackboard.MilestoneWeekEndWishlistReward, sortOrder++);
            }

            var resolvedBugScore = Math.Max(0, blackboard.BugScore + bugDelta);
            if (resolvedBugScore < 40)
            {
                AddFlatModifier(modifiers, "低 Bug 口碑奖励", blackboard.LowBugWeeklyWishlistReward, sortOrder++);
            }

            var resolvedVisualScore = blackboard.VisualScore + visualDelta;
            if (resolvedVisualScore > 60)
            {
                AddFlatModifier(modifiers, "高画面曝光奖励", blackboard.HighVisualWeekEndWishlistGrowthBonus, sortOrder++);
            }

            if (program > 0 && art > 0 && audio > 0)
            {
                AddFlatModifier(modifiers, "三房间协同奖励", blackboard.AllRoomsSameWeekWishlistGrowthBonus, sortOrder++);
            }

            if (blackboard.HasAnySameRoomSpentAsPreviousWeek())
            {
                AddFlatModifier(modifiers, "连续同房间投入奖励", blackboard.SameRoomConsecutiveWishlistReward, sortOrder++);
            }

            return modifiers;
        }

        private static void AddFlatModifier(List<WishlistModifier> modifiers, string sourceName, int value, int sortOrder)
        {
            if (value == 0)
            {
                return;
            }

            modifiers.Add(new WishlistModifier(sourceName, WishlistModifierKind.Flat, value, sortOrder));
        }

        private static int ResolveWishlistModifiers(
            List<WishlistModifier> modifiers,
            int wishlistStartValue,
            out int wishlistEndValue,
            out IReadOnlyList<WishlistModifierResult> resolvedModifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                wishlistEndValue = Math.Max(0, wishlistStartValue);
                resolvedModifiers = Array.Empty<WishlistModifierResult>();
                return 0;
            }

            SortWishlistModifiers(modifiers);

            var resolved = new List<WishlistModifierResult>(modifiers.Count);
            var value = 0;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                var before = value;
                value = ResolveWishlistModifierValue(before, modifier);
                var beforeWishlistCount = Math.Max(0, wishlistStartValue + before);
                var afterWishlistCount = Math.Max(0, wishlistStartValue + value);
                resolved.Add(new WishlistModifierResult(
                    modifier.SourceName,
                    modifier.Kind,
                    modifier.Value,
                    before,
                    value,
                    beforeWishlistCount,
                    afterWishlistCount));
            }

            resolvedModifiers = new ReadOnlyCollection<WishlistModifierResult>(resolved);
            wishlistEndValue = Math.Max(0, wishlistStartValue + value);
            return wishlistEndValue - wishlistStartValue;
        }

        private static void SortWishlistModifiers(List<WishlistModifier> modifiers)
        {
            modifiers.Sort((left, right) =>
            {
                var kindComparison = left.Kind.CompareTo(right.Kind);
                if (kindComparison != 0)
                {
                    return kindComparison;
                }

                var orderComparison = left.SortOrder.CompareTo(right.SortOrder);
                if (orderComparison != 0)
                {
                    return orderComparison;
                }

                return string.Compare(left.SourceName, right.SourceName, StringComparison.Ordinal);
            });
        }

        private static int ResolveWishlistModifierValue(int currentValue, WishlistModifier modifier)
        {
            switch (modifier.Kind)
            {
                case WishlistModifierKind.Flat:
                    return currentValue + modifier.Value;
                case WishlistModifierKind.Multiplier:
                    var multiplier = Math.Max(0f, 1f + modifier.Value / 100f);
                    return (int)Math.Round(currentValue * multiplier);
                default:
                    return currentValue;
            }
        }
    }
}
