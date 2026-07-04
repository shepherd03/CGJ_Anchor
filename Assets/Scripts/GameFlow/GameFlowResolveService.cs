using System;

namespace Anchor.GameFlow
{
    public sealed class GameFlowResolveService
    {
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
            var qualityDelta = 0;
            var bugDelta = blackboard.WeeklyBugDelta;
            var coinDelta = -(program + art + audio) * 35;
            var wishlistDelta = 0;
            wishlistDelta += GetWeeklyWishlistBonus(blackboard, program, art, audio, visualDelta, bugDelta);
            wishlistDelta = (int)Math.Round(wishlistDelta * blackboard.CurrentWeekWishlistMultiplier);

            var eventId = 0;
            if (blackboard.RemainingActionPoints > 0)
            {
                coinDelta += blackboard.RemainingActionPoints * 20;
            }

            var summary =
                $"第 {blackboard.MonthIndex} 月第 {blackboard.WeekIndex} 周结算：质量分 +{qualityDelta}，Bug 值 {bugDelta:+0;-0;0}，愿望单 +{wishlistDelta}，金币 {coinDelta:+0;-0;0}";
            return new WeekResolveResult(
                blackboard.MonthIndex,
                blackboard.WeekIndex,
                visualDelta,
                atmosphereDelta,
                qualityDelta,
                bugDelta,
                coinDelta,
                wishlistDelta,
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
            var bugPenalty = Math.Max(0f, blackboard.BugScore * 0.35f);
            var qualityFactor = Math.Max(0f, blackboard.QualityScore - bugPenalty);
            var rawWishlistDelta = blackboard.WeeklyWishlistGrowth + (int)Math.Round(qualityFactor * GetWishlistMultiplier(month.SettlementType));
            var wishlistGrowthMultiplier = Math.Max(0f, 1f + blackboard.WishlistGrowthPercentBonus / 100f);
            var wishlistDelta = (int)Math.Round(rawWishlistDelta * wishlistGrowthMultiplier);
            var coinDelta = GetCoinDelta(month.SettlementType, wishlistDelta, blackboard.BugScore);
            var qualityDelta = 0;
            var bugDelta = month.SettlementType == MonthSettlementType.ClosedBeta ? -Math.Min(blackboard.BugScore, 8) : 0;
            var summary = $"{month.DisplayName}{GetSettlementTypeName(month.SettlementType)}：愿望单 +{wishlistDelta}，金币 {coinDelta:+0;-0;0}";

            return new MonthSettlementResult(
                blackboard.MonthIndex,
                month.SettlementType,
                wishlistDelta,
                coinDelta,
                qualityDelta,
                bugDelta,
                summary);
        }

        public EndingResult ResolveEnding(GameFlowBlackboard blackboard)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            if (blackboard.QualityScore >= 220 && blackboard.WishlistCount >= 900 && blackboard.BugScore <= 20)
            {
                return new EndingResult("hit", "好评如潮", "质量、愿望单和稳定性都达到了优秀线。");
            }

            if (blackboard.QualityScore >= 160 && blackboard.WishlistCount >= 550)
            {
                return new EndingResult("boutique", "小众精品", "整体完成度不错，积累了一批稳定玩家。");
            }

            if (blackboard.BugScore >= 55)
            {
                return new EndingResult("buggy", "无人问津", "Bug 压过了游戏亮点，口碑没有撑起来。");
            }

            return new EndingResult("storm", "暴死结局", "质量和热度都不足以支撑正式上线。");
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

        private static int GetCoinDelta(MonthSettlementType type, int wishlistDelta, int bugScore)
        {
            var baseIncome = type switch
            {
                MonthSettlementType.PvRelease => 120,
                MonthSettlementType.ClosedBeta => 220,
                MonthSettlementType.PublicRelease => 360,
                MonthSettlementType.FinalRelease => 520,
                _ => 0
            };

            return baseIncome + wishlistDelta / 4 - (int)Math.Round(bugScore * 5f);
        }

        private static int GetWeeklyWishlistBonus(
            GameFlowBlackboard blackboard,
            int program,
            int art,
            int audio,
            int visualDelta,
            int bugDelta)
        {
            var bonus = 0;

            if (blackboard.TotalWeekIndex == 3 ||
                blackboard.TotalWeekIndex == 6 ||
                blackboard.TotalWeekIndex == 9)
            {
                bonus += blackboard.MilestoneWeekEndWishlistReward;
            }

            var resolvedBugScore = Math.Max(0, blackboard.BugScore + bugDelta);
            if (resolvedBugScore < 40)
            {
                bonus += blackboard.LowBugWeeklyWishlistReward;
            }

            var resolvedVisualScore = blackboard.VisualScore + visualDelta;
            if (resolvedVisualScore > 60)
            {
                bonus += blackboard.HighVisualWeekEndWishlistGrowthBonus;
            }

            if (program > 0 && art > 0 && audio > 0)
            {
                bonus += blackboard.AllRoomsSameWeekWishlistGrowthBonus;
            }

            if (blackboard.HasAnySameRoomSpentAsPreviousWeek())
            {
                bonus += blackboard.SameRoomConsecutiveWishlistReward;
            }

            return bonus;
        }
    }
}
