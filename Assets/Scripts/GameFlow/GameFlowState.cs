namespace Anchor.GameFlow
{
    /// <summary>
    /// 游戏主流程状态。它只描述流程推进位置，不直接代表玩家属性。
    /// </summary>
    public enum GameFlowState
    {
        /// <summary>
        /// 新游戏初始化，负责重置玩家属性和流程黑板。
        /// </summary>
        NewGame,

        /// <summary>
        /// 月份开始，负责切换当前月份并发放月初资源。
        /// </summary>
        MonthStart,

        /// <summary>
        /// 月初预算/商店阶段，玩家可购买或选择本月增益。
        /// </summary>
        BudgetShop,

        /// <summary>
        /// 周开始，负责刷新本周行动力并清空本周投点记录。
        /// </summary>
        WeekStart,

        /// <summary>
        /// 周事件阶段，处理周开始触发的配置事件和玩家选择。
        /// </summary>
        WeekEvent,

        /// <summary>
        /// 周行动阶段，玩家把行动力投入到不同开发方向。
        /// </summary>
        WeekAction,

        /// <summary>
        /// 周结算阶段，将本周投点转换为属性变化和金币变化。
        /// </summary>
        WeekResolve,

        /// <summary>
        /// 月结算阶段，根据当前月份结算类型计算愿望单、金币和其他结果。
        /// </summary>
        MonthSettlement,

        /// <summary>
        /// 游戏结束阶段，根据最终玩家属性选择结局。
        /// </summary>
        Ending
    }
}
