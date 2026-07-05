using System;

namespace Anchor.GameFlow
{
    /// <summary>
    /// HUD、周结算和最终结算共同展示的核心流程数值快照。
    /// </summary>
    public readonly struct GameFlowCoreStatsSnapshot
    {
        public GameFlowCoreStatsSnapshot(int bugScore, int visualScore, int atmosphereScore)
        {
            BugScore = bugScore;
            VisualScore = visualScore;
            AtmosphereScore = atmosphereScore;
        }

        public int BugScore { get; }
        public int VisualScore { get; }
        public int AtmosphereScore { get; }

        public static GameFlowCoreStatsSnapshot From(GameFlowBlackboard blackboard)
        {
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            return new GameFlowCoreStatsSnapshot(
                blackboard.BugScore,
                blackboard.VisualScore,
                blackboard.AtmosphereScore);
        }
    }
}
