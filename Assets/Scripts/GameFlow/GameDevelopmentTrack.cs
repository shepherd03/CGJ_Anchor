namespace Anchor.GameFlow
{
    /// <summary>
    /// 周行动投点方向。它是本周行为分类，不是玩家长期属性。
    /// </summary>
    public enum GameDevelopmentTrack
    {
        /// <summary>
        /// 程序开发投入，主要影响质量分，同时可能增加 Bug。
        /// </summary>
        Program,

        /// <summary>
        /// 美术开发投入，主要影响画面表现和质量分。
        /// </summary>
        Art,

        /// <summary>
        /// 音效开发投入，主要影响氛围表现和质量分，同时可能增加 Bug。
        /// </summary>
        Audio
    }
}
