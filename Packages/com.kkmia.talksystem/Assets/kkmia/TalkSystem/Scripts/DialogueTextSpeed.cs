namespace kkmia.TalkSystem
{
    /// <summary>
    /// 文字送り速度の正規化値（0..1）とタイプライター間隔（秒）の相互変換。純関数。
    /// </summary>
    public static class DialogueTextSpeed
    {
        /// <summary>遅い側の既定間隔（秒/文字）。</summary>
        public const float DefaultSlowestInterval = 0.12f;

        /// <summary>速い側の既定間隔（秒/文字）。0 に近いほど瞬間表示。</summary>
        public const float DefaultFastestInterval = 0.005f;

        /// <summary>
        /// 正規化速度(0=遅い,1=速い)をタイプライター間隔(秒)へ変換する。
        /// </summary>
        public static float ToInterval(float normalized, float slowest = DefaultSlowestInterval, float fastest = DefaultFastestInterval)
        {
            if (normalized < 0f) normalized = 0f;
            if (normalized > 1f) normalized = 1f;
            return slowest + (fastest - slowest) * normalized;
        }
    }
}
