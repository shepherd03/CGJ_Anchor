namespace Anchor.GameFlow
{
    /// <summary>
    /// 周行动投点方向。它是本周行为分类，不是玩家长期属性。
    /// </summary>
    public enum GameDevelopmentTrack
    {
        /// <summary>
        /// 程序开发投入，主要降低 Bug。
        /// </summary>
        Program,

        /// <summary>
        /// 美术开发投入，主要影响画面表现，并通过画面影响动态质量分。
        /// </summary>
        Art,

        /// <summary>
        /// 音效开发投入，主要影响氛围表现。
        /// </summary>
        Audio
    }
}
