using System;
using System.Collections.Generic;

namespace Anchor.GameFlow
{
    public sealed class GameFlowDefinitionProvider
    {
        private readonly GameFlowSettings mSettings;
        private readonly List<MonthDefinition> mMonths = new();

        public GameFlowDefinitionProvider(GameFlowSettings settings)
        {
            mSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            BuildDefaultMonths();
        }

        public int MonthCount => mMonths.Count;

        public MonthDefinition GetMonth(int monthIndex)
        {
            if (monthIndex <= 0 || monthIndex > mMonths.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(monthIndex), monthIndex, "Month index is outside the configured flow.");
            }

            return mMonths[monthIndex - 1];
        }

        private void BuildDefaultMonths()
        {
            mMonths.Clear();

            for (int i = 1; i <= mSettings.TotalMonths; i++)
            {
                mMonths.Add(new MonthDefinition(
                    i,
                    GetDisplayName(i),
                    GetSettlementType(i, mSettings.TotalMonths),
                    mSettings.WeeksPerMonth));
            }
        }

        private static string GetDisplayName(int monthIndex)
        {
            return monthIndex switch
            {
                1 => "第一月",
                2 => "第二月",
                3 => "第三月",
                _ => $"第{monthIndex}月"
            };
        }

        private static MonthSettlementType GetSettlementType(int monthIndex, int totalMonths)
        {
            if (monthIndex >= totalMonths)
            {
                return MonthSettlementType.FinalRelease;
            }

            return monthIndex switch
            {
                1 => MonthSettlementType.PvRelease,
                2 => MonthSettlementType.ClosedBeta,
                _ => MonthSettlementType.PublicRelease
            };
        }
    }
}
