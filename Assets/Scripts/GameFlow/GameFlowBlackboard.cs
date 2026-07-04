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
        private readonly Random mRandom = new();
        private readonly HashSet<GameDevelopmentTrack> mPreviousWeekSpentTracks = new();
        private float mCurrentWeekWishlistMultiplier = 1f;

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
        public int BugScore => PlayerAttributes.Get(CharacterAttributeIds.Bug);
        public int VisualScore => PlayerAttributes.Get(CharacterAttributeIds.Visual);
        public int AtmosphereScore => PlayerAttributes.Get(CharacterAttributeIds.Atmosphere);
        public int BaseQualityScore => CalculateBaseQualityScore(VisualScore, AtmosphereScore);
        public int QualityScore => CalculateQualityScore(VisualScore, AtmosphereScore, BugScore);
        public int ProgramRoomOneActionWishlistReward => GetInt(CharacterAttributeIds.ProgramRoomOneActionReward);
        public int ProgramRoomTwoActionWishlistReward => GetInt(CharacterAttributeIds.ProgramRoomTwoActionReward);
        public int ArtRoomOneActionWishlistReward => GetInt(CharacterAttributeIds.ArtRoomOneActionReward);
        public int ArtRoomTwoActionWishlistReward => GetInt(CharacterAttributeIds.ArtRoomTwoActionReward);
        public int AudioRoomOneActionWishlistReward => GetInt(CharacterAttributeIds.AudioRoomOneActionReward);
        public int AudioRoomTwoActionWishlistReward => GetInt(CharacterAttributeIds.AudioRoomTwoActionReward);
        public int ProgramRoomPerActionWishlistReward => GetInt(CharacterAttributeIds.ProgramRoomPerActionReward);
        public int ArtRoomPerActionWishlistReward => GetInt(CharacterAttributeIds.ArtRoomPerActionReward);
        public int AudioRoomPerActionWishlistReward => GetInt(CharacterAttributeIds.AudioRoomPerActionReward);
        public int SameRoomConsecutiveWishlistReward => GetInt(CharacterAttributeIds.SameRoomConsecutiveWishlistReward);
        public int WeekStartWishlistChanceMultiplier => GetInt(CharacterAttributeIds.WeekStartWishlistChanceMultiplier);
        public float CurrentWeekWishlistMultiplier => mCurrentWeekWishlistMultiplier;
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
            mPreviousWeekSpentTracks.Clear();
            mCurrentWeekWishlistMultiplier = 1f;
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
            RollWeekStartWishlistMultiplier();
            mActionAllocations.Clear();
        }

        public bool TryAllocate(GameDevelopmentTrack track, int points)
        {
            points = Math.Max(0, points);
            if (points != 1 && points != 2)
            {
                return false;
            }

            if (RemainingActionPoints < points)
            {
                return false;
            }

            PlayerAttributes.Set(CharacterAttributeIds.WeeklyActionPower, RemainingActionPoints - points);
            mActionAllocations.TryGetValue(track, out var current);
            mActionAllocations[track] = current + points;
            ApplyRoomActionReward(track, points);
            return true;
        }

        public void ApplyWeekResult(WeekResolveResult result)
        {
            LastWeekResult = result;
            PlayerAttributes.Add(CharacterAttributeIds.Visual, result.VisualDelta);
            PlayerAttributes.Add(CharacterAttributeIds.Atmosphere, result.AtmosphereDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Bug, Math.Max(0, BugScore + result.BugDelta));
            PlayerAttributes.Add(CharacterAttributeIds.Coins, result.CoinDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Wishlist, Math.Max(0, WishlistCount + result.WishlistDelta));

            if (result.EventId > 0)
            {
                mTriggeredEventIds.Add(result.EventId);
            }

            RecordResolvedWeekSpentTracks();
        }

        public void ApplyMonthSettlement(MonthSettlementResult result)
        {
            LastMonthResult = result;
            PlayerAttributes.Add(CharacterAttributeIds.Coins, result.CoinDelta);
            PlayerAttributes.Set(CharacterAttributeIds.Wishlist, Math.Max(0, WishlistCount + result.WishlistDelta));
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

        public bool HasAnySameRoomSpentAsPreviousWeek()
        {
            foreach (var allocation in mActionAllocations)
            {
                if (allocation.Value > 0 && mPreviousWeekSpentTracks.Contains(allocation.Key))
                {
                    return true;
                }
            }

            return false;
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

        public bool CanReadAttribute(int attributeId)
        {
            return attributeId == CharacterAttributeIds.Quality || mAttributeCatalog.Contains(attributeId);
        }

        public bool CanWriteAttribute(int attributeId)
        {
            return attributeId != CharacterAttributeIds.Quality && mAttributeCatalog.Contains(attributeId);
        }

        public int GetAttributeValue(int attributeId)
        {
            return attributeId == CharacterAttributeIds.Quality
                ? QualityScore
                : PlayerAttributes.Get(attributeId);
        }

        public void RecordTriggeredEvent(int eventId)
        {
            if (eventId > 0)
            {
                mTriggeredEventIds.Add(eventId);
            }
        }

        private int GetInt(int attributeId)
        {
            return (int)PlayerAttributes.Get(attributeId);
        }

        public static int CalculateBaseQualityScore(int visualScore, int atmosphereScore)
        {
            return (int)Math.Round(Math.Max(0d, (visualScore + atmosphereScore) * 0.5d));
        }

        public static int CalculateQualityScore(int visualScore, int atmosphereScore, int bugScore)
        {
            var baseQuality = Math.Max(0d, (visualScore + atmosphereScore) * 0.5d);
            var normalizedBug = Math.Max(0, bugScore);
            var bugMultiplier = Math.Max(0d, Math.Min(1d, 1d - normalizedBug / 100d));
            return (int)Math.Round(baseQuality * bugMultiplier);
        }

        private void RollWeekStartWishlistMultiplier()
        {
            var percentBonus = WeekStartWishlistChanceMultiplier;
            mCurrentWeekWishlistMultiplier = percentBonus > 0 && mRandom.NextDouble() < 0.5d
                ? 1f + percentBonus / 100f
                : 1f;
        }

        private void RecordResolvedWeekSpentTracks()
        {
            mPreviousWeekSpentTracks.Clear();
            foreach (var allocation in mActionAllocations)
            {
                if (allocation.Value > 0)
                {
                    mPreviousWeekSpentTracks.Add(allocation.Key);
                }
            }
        }

        private void ApplyRoomActionReward(GameDevelopmentTrack track, int points)
        {
            switch (track)
            {
                case GameDevelopmentTrack.Program:
                    AddClamped(CharacterAttributeIds.Bug, GetRoomActionDelta(
                        points,
                        CharacterAttributeIds.ProgramOneActionBugDeltaMin,
                        CharacterAttributeIds.ProgramOneActionBugDeltaMax,
                        CharacterAttributeIds.ProgramTwoActionBugDeltaMin,
                        CharacterAttributeIds.ProgramTwoActionBugDeltaMax));
                    AddRoomWishlistReward(points, ProgramRoomOneActionWishlistReward, ProgramRoomTwoActionWishlistReward, ProgramRoomPerActionWishlistReward);
                    break;
                case GameDevelopmentTrack.Art:
                    PlayerAttributes.Add(CharacterAttributeIds.Visual, GetRoomActionDelta(
                        points,
                        CharacterAttributeIds.ArtOneActionVisualDeltaMin,
                        CharacterAttributeIds.ArtOneActionVisualDeltaMax,
                        CharacterAttributeIds.ArtTwoActionVisualDeltaMin,
                        CharacterAttributeIds.ArtTwoActionVisualDeltaMax));
                    AddRoomWishlistReward(points, ArtRoomOneActionWishlistReward, ArtRoomTwoActionWishlistReward, ArtRoomPerActionWishlistReward);
                    break;
                case GameDevelopmentTrack.Audio:
                    PlayerAttributes.Add(CharacterAttributeIds.Atmosphere, GetRoomActionDelta(
                        points,
                        CharacterAttributeIds.AudioOneActionAtmosphereDeltaMin,
                        CharacterAttributeIds.AudioOneActionAtmosphereDeltaMax,
                        CharacterAttributeIds.AudioTwoActionAtmosphereDeltaMin,
                        CharacterAttributeIds.AudioTwoActionAtmosphereDeltaMax));
                    AddRoomWishlistReward(points, AudioRoomOneActionWishlistReward, AudioRoomTwoActionWishlistReward, AudioRoomPerActionWishlistReward);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(track), track, null);
            }
        }

        private int GetRoomActionDelta(
            int points,
            int onePointMinId,
            int onePointMaxId,
            int twoPointMinId,
            int twoPointMaxId)
        {
            return points == 1
                ? GetRandomInclusive(GetInt(onePointMinId), GetInt(onePointMaxId))
                : GetRandomInclusive(GetInt(twoPointMinId), GetInt(twoPointMaxId));
        }

        private void AddRoomWishlistReward(int points, int onePointReward, int twoPointReward, int perPointReward)
        {
            var tierReward = points == 1 ? onePointReward : twoPointReward;
            var reward = tierReward + perPointReward * points;
            if (reward != 0)
            {
                AddClamped(CharacterAttributeIds.Wishlist, reward);
            }
        }

        private int GetRandomInclusive(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                (minValue, maxValue) = (maxValue, minValue);
            }

            return mRandom.Next(minValue, maxValue + 1);
        }

        private void AddClamped(int attributeId, int delta)
        {
            PlayerAttributes.Set(attributeId, Math.Max(0, PlayerAttributes.Get(attributeId) + delta));
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
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramRoomOneActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramRoomTwoActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtRoomOneActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtRoomTwoActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioRoomOneActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioRoomTwoActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramRoomPerActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtRoomPerActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioRoomPerActionReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.SameRoomConsecutiveWishlistReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeekStartWishlistChanceMultiplier);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.MilestoneWeekEndWishlistReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.LowBugWeeklyWishlistReward);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WishlistGrowthPercentBonus);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.HighVisualWeekEndWishlistGrowthBonus);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AllRoomsSameWeekWishlistGrowthBonus);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyBugDelta);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyVisualDelta);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.WeeklyAtmosphereDelta);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramOneActionBugDeltaMin);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramOneActionBugDeltaMax);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramTwoActionBugDeltaMin);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ProgramTwoActionBugDeltaMax);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtOneActionVisualDeltaMin);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtOneActionVisualDeltaMax);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtTwoActionVisualDeltaMin);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.ArtTwoActionVisualDeltaMax);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioOneActionAtmosphereDeltaMin);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioOneActionAtmosphereDeltaMax);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioTwoActionAtmosphereDeltaMin);
            mAttributeCatalog.GetRequiredRow(CharacterAttributeIds.AudioTwoActionAtmosphereDeltaMax);
        }
    }
}
