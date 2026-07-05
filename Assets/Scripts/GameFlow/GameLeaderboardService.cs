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
        public readonly int WishlistCount;
        public readonly string EndingId;
        public readonly string EndingDisplayName;
        public readonly string EndingSummary;
        public readonly long CompletedAtUtcTicks;

        internal GameLeaderboardEntry(
            int rank,
            string runId,
            int wishlistCount,
            string endingId,
            string endingDisplayName,
            string endingSummary,
            long completedAtUtcTicks)
        {
            Rank = rank;
            RunId = runId;
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

        private static LeaderboardSaveData sData;
        private static bool sLoaded;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static GameLeaderboardEntry RecordGameEnd(GameFlowBlackboard blackboard, EndingResult ending)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            return RecordGameEnd(blackboard.WishlistCount, ending);
        }

        public static GameLeaderboardEntry RecordGameEnd(int wishlistCount, EndingResult ending)
        {
            EnsureLoaded();

            var record = new SerializableLeaderboardEntry
            {
                runId = Guid.NewGuid().ToString("N"),
                wishlistCount = Math.Max(0, wishlistCount),
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
            entries.Sort(CompareEntries);
        }

        private static int CompareEntries(SerializableLeaderboardEntry left, SerializableLeaderboardEntry right)
        {
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
            return new GameLeaderboardEntry(
                rank,
                NullToEmpty(entry.runId),
                Math.Max(0, entry.wishlistCount),
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
            public int wishlistCount;
            public string endingId;
            public string endingDisplayName;
            public string endingSummary;
            public long completedAtUtcTicks;
            public int sequence;
        }
    }
}
