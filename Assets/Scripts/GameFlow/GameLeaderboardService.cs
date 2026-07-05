using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;

namespace Anchor.GameFlow
{
    public readonly struct GameLeaderboardEntry
    {
        public readonly int Rank;
        public readonly string RunId;
        public readonly int LeaderboardScore;
        public readonly int QualityScore;
        public readonly int WishlistCount;
        public readonly string EndingId;
        public readonly string EndingDisplayName;
        public readonly string EndingSummary;
        public readonly long CompletedAtUtcTicks;

        internal GameLeaderboardEntry(
            int rank,
            string runId,
            int leaderboardScore,
            int qualityScore,
            int wishlistCount,
            string endingId,
            string endingDisplayName,
            string endingSummary,
            long completedAtUtcTicks)
        {
            Rank = rank;
            RunId = runId;
            LeaderboardScore = leaderboardScore;
            QualityScore = qualityScore;
            WishlistCount = wishlistCount;
            EndingId = endingId;
            EndingDisplayName = endingDisplayName;
            EndingSummary = endingSummary;
            CompletedAtUtcTicks = completedAtUtcTicks;
        }

        public DateTime CompletedAtUtc => new DateTime(CompletedAtUtcTicks, DateTimeKind.Utc);
    }

    public static class GameLeaderboardService
    {
        private const string SaveFileName = "game_leaderboard.json";
        private const double QualityPower = 1.1d;
        private const double QualityMultiplier = 100d;
        private const double WishlistBonusWeight = 0.25d;
        private const double WishlistNormalizeValue = 1_000_000d;
        private const double WishlistPower = 0.6d;

        private static LeaderboardSaveData sData;
        private static bool sLoaded;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static GameLeaderboardEntry RecordGameEnd(GameFlowBlackboard blackboard, EndingResult ending)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            return RecordGameEnd(blackboard.QualityScore, blackboard.WishlistCount, ending);
        }

        public static GameLeaderboardEntry RecordGameEnd(int wishlistCount, EndingResult ending)
        {
            return RecordGameEnd(0, wishlistCount, ending);
        }

        public static GameLeaderboardEntry RecordGameEnd(int qualityScore, int wishlistCount, EndingResult ending)
        {
            EnsureLoaded();

            int resolvedQualityScore = Math.Max(0, qualityScore);
            int resolvedWishlistCount = Math.Max(0, wishlistCount);
            int leaderboardScore = CalculateLeaderboardScore(resolvedQualityScore, resolvedWishlistCount);
            var record = new SerializableLeaderboardEntry
            {
                runId = Guid.NewGuid().ToString("N"),
                leaderboardScore = leaderboardScore,
                qualityScore = resolvedQualityScore,
                wishlistCount = resolvedWishlistCount,
                endingId = NullToEmpty(ending.EndingId),
                endingDisplayName = NullToEmpty(ending.DisplayName),
                endingSummary = NullToEmpty(ending.Summary),
                completedAtUtcTicks = DateTime.UtcNow.Ticks,
                sequence = sData.nextSequence++
            };

            sData.entries.Add(record);
            SortEntries(sData.entries);
            Save();

            var rank = sData.entries.IndexOf(record) + 1;
            return ToEntry(record, rank);
        }

        public static int CalculateLeaderboardScore(int qualityScore, int wishlistCount)
        {
            double normalizedQuality = Math.Max(0, qualityScore);
            double normalizedWishlist = Math.Max(0, wishlistCount);
            double qualityScorePart = Math.Pow(normalizedQuality, QualityPower) * QualityMultiplier;
            double wishlistBonus = 1d + WishlistBonusWeight * Math.Pow(normalizedWishlist / WishlistNormalizeValue, WishlistPower);
            double score = qualityScorePart * wishlistBonus;

            if (score >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Round(score);
        }

        public static IReadOnlyList<GameLeaderboardEntry> GetRankings(int maxCount = 0)
        {
            EnsureLoaded();
            SortEntries(sData.entries);

            var count = maxCount > 0 ? Math.Min(maxCount, sData.entries.Count) : sData.entries.Count;
            var results = new List<GameLeaderboardEntry>(count);
            for (var i = 0; i < count; i++)
            {
                results.Add(ToEntry(sData.entries[i], i + 1));
            }

            return new ReadOnlyCollection<GameLeaderboardEntry>(results);
        }

        public static void Clear()
        {
            sData = new LeaderboardSaveData();
            sLoaded = true;
            Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            sData = null;
            sLoaded = false;
        }

        private static void EnsureLoaded()
        {
            if (sLoaded)
            {
                return;
            }

            sData = Load();
            sLoaded = true;
            SortEntries(sData.entries);
        }

        private static LeaderboardSaveData Load()
        {
            try
            {
                var path = SaveFilePath;
                if (!File.Exists(path))
                {
                    return new LeaderboardSaveData();
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new LeaderboardSaveData();
                }

                var data = JsonUtility.FromJson<LeaderboardSaveData>(json) ?? new LeaderboardSaveData();
                data.entries ??= new List<SerializableLeaderboardEntry>();
                NormalizeEntries(data.entries);
                data.nextSequence = Math.Max(data.nextSequence, GetNextSequence(data.entries));
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"读取排行榜失败，将使用空排行榜：{exception.Message}");
                return new LeaderboardSaveData();
            }
        }

        private static void Save()
        {
            try
            {
                var path = SaveFilePath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, JsonUtility.ToJson(sData, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"保存排行榜失败：{exception.Message}");
            }
        }

        private static void SortEntries(List<SerializableLeaderboardEntry> entries)
        {
            NormalizeEntries(entries);
            entries.Sort(CompareEntries);
        }

        private static void NormalizeEntries(List<SerializableLeaderboardEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                entry.qualityScore = Math.Max(0, entry.qualityScore);
                entry.wishlistCount = Math.Max(0, entry.wishlistCount);
                entry.completedAtUtcTicks = Math.Max(0, entry.completedAtUtcTicks);

                if (entry.leaderboardScore <= 0)
                {
                    entry.leaderboardScore = CalculateLeaderboardScore(entry.qualityScore, entry.wishlistCount);
                }
            }
        }

        private static int CompareEntries(SerializableLeaderboardEntry left, SerializableLeaderboardEntry right)
        {
            var scoreCompare = right.leaderboardScore.CompareTo(left.leaderboardScore);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            var qualityCompare = right.qualityScore.CompareTo(left.qualityScore);
            if (qualityCompare != 0)
            {
                return qualityCompare;
            }

            var wishlistCompare = right.wishlistCount.CompareTo(left.wishlistCount);
            if (wishlistCompare != 0)
            {
                return wishlistCompare;
            }

            var timeCompare = right.completedAtUtcTicks.CompareTo(left.completedAtUtcTicks);
            if (timeCompare != 0)
            {
                return timeCompare;
            }

            return right.sequence.CompareTo(left.sequence);
        }

        private static int GetNextSequence(List<SerializableLeaderboardEntry> entries)
        {
            var next = 1;
            for (var i = 0; i < entries.Count; i++)
            {
                next = Math.Max(next, entries[i].sequence + 1);
            }

            return next;
        }

        private static GameLeaderboardEntry ToEntry(SerializableLeaderboardEntry entry, int rank)
        {
            int qualityScore = Math.Max(0, entry.qualityScore);
            int wishlistCount = Math.Max(0, entry.wishlistCount);
            int leaderboardScore = entry.leaderboardScore > 0
                ? entry.leaderboardScore
                : CalculateLeaderboardScore(qualityScore, wishlistCount);

            return new GameLeaderboardEntry(
                rank,
                NullToEmpty(entry.runId),
                leaderboardScore,
                qualityScore,
                wishlistCount,
                NullToEmpty(entry.endingId),
                NullToEmpty(entry.endingDisplayName),
                NullToEmpty(entry.endingSummary),
                Math.Max(0, entry.completedAtUtcTicks));
        }

        private static string NullToEmpty(string value)
        {
            return value ?? string.Empty;
        }

        [Serializable]
        private sealed class LeaderboardSaveData
        {
            public int nextSequence = 1;
            public List<SerializableLeaderboardEntry> entries = new();
        }

        [Serializable]
        private sealed class SerializableLeaderboardEntry
        {
            public string runId;
            public int leaderboardScore;
            public int qualityScore;
            public int wishlistCount;
            public string endingId;
            public string endingDisplayName;
            public string endingSummary;
            public long completedAtUtcTicks;
            public int sequence;
        }
    }
}
