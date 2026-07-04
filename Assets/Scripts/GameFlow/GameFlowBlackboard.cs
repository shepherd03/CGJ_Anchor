using System;
using System.Collections.Generic;
using Anchor.Character;
using Anchor.Character.Attributes;

namespace Anchor.GameFlow
{
    public sealed class GameFlowBlackboard
    {
        private readonly Dictionary<GameDevelopmentTrack, int> mActionAllocations = new();
        private readonly List<int> mActiveBuffIds = new();
        private readonly List<int> mTriggeredEventIds = new();
        private readonly CharacterAttributeCatalog mAttributeCatalog;

        public int MonthIndex { get; private set; }
        public int WeekIndex { get; private set; }
        public int TotalWeekIndex { get; private set; }
        public int RemainingActionPoints => CurrentWeekActionPower;
        public GamePlayer Player { get; } = new();
        public CharacterAttributeSet PlayerAttributes => Player.Attributes;
        public CharacterAttributeCatalog AttributeCatalog => mAttributeCatalog;
        public int BaseWeeklyActionPower => GetInt(CharacterAttributeIds.BaseWeeklyActionPower);
        public int CurrentWeekActionPower => GetInt(CharacterAttributeIds.WeeklyActionPower);
        public int MonthlyCoinIncome => GetInt(CharacterAttributeIds.MonthlyCoinIncome);
        public int WeeklyWishlistGrowth => GetInt(CharacterAttributeIds.WeeklyWishlistGrowth);
        public int Coins => GetInt(CharacterAttributeIds.Coins);
        public int WishlistCount => GetInt(CharacterAttributeIds.Wishlist);
        public int QualityScore => PlayerAttributes.Get(CharacterAttributeIds.Quality);
        public int BugScore => PlayerAttributes.Get(CharacterAttributeIds.Bug);
        public int VisualScore => PlayerAttributes.Get(CharacterAttributeIds.Visual);
        public int AtmosphereScore => PlayerAttributes.Get(CharacterAttributeIds.Atmosphere);
        public int MilestoneWeekEndWishlistReward => GetInt(CharacterAttributeIds.MilestoneWeekEndWishlistReward);
        public int LowBugWeeklyWishlistReward => GetInt(CharacterAttributeIds.LowBugWeeklyWishlistReward);
        public int WishlistGrowthPercentBonus => GetInt(CharacterAttributeIds.WishlistGrowthPercentBonus);
        public int HighVisualWeekEndWishlistGrowthBonus => GetInt(CharacterAttributeIds.HighVisualWeekEndWishlistGrowthBonus);
        public int AllRoomsSameWeekWishlistGrowthBonus => GetInt(CharacterAttributeIds.AllRoomsSameWeekWishlistGrowthBonus);
        public int WeeklyBugDelta => GetInt(CharacterAttributeIds.WeeklyBugDelta);
        public int WeeklyVisualDelta => GetInt(CharacterAttributeIds.WeeklyVisualDelta);
        public int WeeklyAtmosphereDelta => GetInt(CharacterAttributeIds.WeeklyAtmosphereDelta);
        public MonthDefinition CurrentMonth { get; private set; }
        public WeekResolveResult LastWeekResult { get; private set; }
        public MonthSettlementResult LastMonthResult { get; private set; }
        public EndingResult EndingResult { get; private set; }

        public IReadOnlyDictionary<GameDevelopmentTrack, int> ActionAllocations => mActionAllocations;
        public IReadOnlyList<int> ActiveBuffIds => mActiveBuffIds;
        public IReadOnlyList<int> TriggeredEventIds => mTriggeredEventIds;

        public GameFlowBlackboard(CharacterAttributeCatalog attributeCatalog)
        {
            mAttributeCatalog = attributeCatalog ?? throw new ArgumentNullException(nameof(attributeCatalog));
            RequireCoreAttributes();
        }

        public void ResetForNewRun(GameFlowSettings settings)
        {
            MonthIndex = 0;
            WeekIndex = 0;
            TotalWeekIndex = 0;
            PlayerAttributes.Clear();
            foreach (var row in mAttributeCatalog.RowsById.Values)
            {
                PlayerAttributes.Set(row.Id, row.DefaultValue);
            }

            CurrentMonth = null;
            LastWeekResult = default;
            LastMonthResult = default;
            EndingResult = default;
            mActionAllocations.Clear();
            mActiveBuffIds.Clear();
            mTriggeredEventIds.Clear();
        }

        public void BeginMonth(MonthDefinition definition)
        {
            CurrentMonth = definition ?? throw new ArgumentNullException(nameof(definition));
            MonthIndex = definition.MonthIndex;
            WeekIndex = 0;
            PlayerAttributes.Add(CharacterAttributeIds.Coins, MonthlyCoinIncome);
        }

        public void BeginWeek()
        {
            WeekIndex++;
            TotalWeekIndex++;
            PlayerAttributes.Set(CharacterAttributeIds.WeeklyActionPower, Math.Max(0, BaseWeeklyActionPower));
            mActionAllocations.Clear();
        }

        public bool TryAllocate(GameDevelopmentTrack track, int points)
        {
            points = Math.Max(0, points);
            if (points == 0)
            {
                return true;
            }

            if (RemainingActionPoints < points)
            {
                return false;
            }

            PlayerAttributes.Set(CharacterAttributeIds.WeeklyActionPower, RemainingActionPoints - points);
            mActionAllocations.TryGetValue(track, out var current);
            mActionAllocations[track] = current + points;
            return true;
        }

        public void ApplyWeekResult(WeekResolveResult result)
        {
            LastWeekResult = result;
            PlayerAttributes.Add(CharacterAttributeIds.Visual, result.VisualDelta);
            PlayerAttributes.Add(CharacterAttributeIds.Atmosphere, result.AtmosphereDelta);
            PlayerAttributes.Add(CharacterAttributeIds.Quality, result.QualityDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Bug, Math.Max(0, BugScore + result.BugDelta));
            PlayerAttributes.Add(CharacterAttributeIds.Coins, result.CoinDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Wishlist, Math.Max(0, WishlistCount + result.WishlistDelta));

            if (result.EventId > 0)
            {
                mTriggeredEventIds.Add(result.EventId);
            }
        }

        public void ApplyMonthSettlement(MonthSettlementResult result)
        {
            LastMonthResult = result;
            PlayerAttributes.Add(CharacterAttributeIds.Coins, result.CoinDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Wishlist, Math.Max(0, WishlistCount + result.WishlistDelta));
            PlayerAttributes.Add(CharacterAttributeIds.Quality, result.QualityDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Bug, Math.Max(0, BugScore + result.BugDelta));
        }

        public void SetEnding(EndingResult result)
        {
            EndingResult = result;
        }

        public int GetAllocatedPoints(GameDevelopmentTrack track)
        {
            return mActionAllocations.TryGetValue(track, out var points) ? points : 0;
        }

        public bool HasActiveBuff(int buffId)
        {
            return mActiveBuffIds.Contains(buffId);
        }

        public bool AddActiveBuff(int buffId)
        {
            if (buffId <= 0 || HasActiveBuff(buffId))
            {
                return false;
            }

            mActiveBuffIds.Add(buffId);
            return true;
        }

        private int GetInt(int attributeId)
        {
            return (int)PlayerAttributes.Get(attributeId);
        }

        private void RequireCoreAttributes()
        {
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.BaseWeeklyActionPower);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyActionPower);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.MonthlyCoinIncome);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyWishlistGrowth);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.Bug);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.Visual);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.Atmosphere);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.Coins);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.Wishlist);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.Quality);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.MilestoneWeekEndWishlistReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.LowBugWeeklyWishlistReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WishlistGrowthPercentBonus);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.HighVisualWeekEndWishlistGrowthBonus);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AllRoomsSameWeekWishlistGrowthBonus);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyBugDelta);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyVisualDelta);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyAtmosphereDelta);
        }
    }
}
