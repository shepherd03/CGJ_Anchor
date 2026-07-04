namespace Anchor.GameFlow
{
    /// <summary>
    /// 月结算类型。它决定月结算公式和展示语义，不是玩家属性。
    /// </summary>
    public enum MonthSettlementType
    {
        /// <summary>
        /// 发布 PV 的月结算。
        /// </summary>
        PvRelease,

        /// <summary>
        /// 内测阶段的月结算。
        /// </summary>
        ClosedBeta,

        /// <summary>
        /// 公测阶段的月结算。
        /// </summary>
        PublicRelease,

        /// <summary>
        /// 最终上线/正式发布结算。
        /// </summary>
        FinalRelease
    }
}
