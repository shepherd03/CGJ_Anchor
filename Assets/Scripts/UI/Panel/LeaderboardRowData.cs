using System;
using Anchor.GameFlow;

namespace Anchor.UI.Panel
{
    /// <summary>
    /// 排行榜 UI 的通用输入数据。Score 越高排名越靠前；同分时完成时间越晚越靠前。
    /// </summary>
    public readonly struct LeaderboardRowData
    {
        public readonly string PlayerName;
        public readonly int Score;
        public readonly int QualityScore;
        public readonly int WishlistCount;
        public readonly string EndingName;
        public readonly string Summary;
        public readonly long CompletedAtUtcTicks;

        public LeaderboardRowData(
            string playerName,
            int qualityScore,
            int wishlistCount,
            string endingName = null,
            string summary = null,
            long completedAtUtcTicks = 0)
        {
            PlayerName = playerName ?? string.Empty;
            QualityScore = Math.Max(0, qualityScore);
            WishlistCount = Math.Max(0, wishlistCount);
            Score = GameLeaderboardService.CalculateLeaderboardScore(QualityScore, WishlistCount);
            EndingName = endingName ?? string.Empty;
            Summary = summary ?? string.Empty;
            CompletedAtUtcTicks = Math.Max(0, completedAtUtcTicks);
        }

        public LeaderboardRowData(
            string playerName,
            int score,
            string endingName = null,
            string summary = null,
            long completedAtUtcTicks = 0)
        {
            PlayerName = playerName ?? string.Empty;
            Score = Math.Max(0, score);
            QualityScore = 0;
            WishlistCount = 0;
            EndingName = endingName ?? string.Empty;
            Summary = summary ?? string.Empty;
            CompletedAtUtcTicks = Math.Max(0, completedAtUtcTicks);
        }

        public DateTime CompletedAtUtc =>
            CompletedAtUtcTicks > 0
                ? new DateTime(CompletedAtUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
    }
}
