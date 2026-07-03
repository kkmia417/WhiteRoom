namespace kkmia.TalkSystem
{
    public enum DialoguePlaybackMode
    {
        Normal,
        Auto,
        Skip
    }

    public readonly struct DialoguePlaybackState
    {
        public DialoguePlaybackState(
            DialoguePlaybackMode mode,
            bool hasCurrentLine,
            bool hasChoices,
            bool isCurrentLineRead)
        {
            Mode = mode;
            HasCurrentLine = hasCurrentLine;
            HasChoices = hasChoices;
            IsCurrentLineRead = isCurrentLineRead;
        }

        public DialoguePlaybackMode Mode { get; }
        public bool HasCurrentLine { get; }
        public bool HasChoices { get; }
        public bool IsCurrentLineRead { get; }
        public bool IsAuto { get { return Mode == DialoguePlaybackMode.Auto; } }
        public bool IsSkip { get { return Mode == DialoguePlaybackMode.Skip; } }
    }

    /// <summary>
    /// 行が表示待ちになったときに、どう進めるかの判断結果。
    /// </summary>
    public readonly struct DialogueAdvancePlan
    {
        public DialogueAdvancePlan(bool shouldAdvance, float delay, bool cancelSkip)
        {
            ShouldAdvance = shouldAdvance;
            Delay = delay;
            CancelSkip = cancelSkip;
        }

        /// <summary>自動で次へ進めるべきか（false は手動入力待ち）。</summary>
        public bool ShouldAdvance { get; }

        /// <summary>進めるまでの待ち秒数。</summary>
        public float Delay { get; }

        /// <summary>スキップを解除すべきか（未読に到達した等）。</summary>
        public bool CancelSkip { get; }

        public static DialogueAdvancePlan Wait()
        {
            return new DialogueAdvancePlan(false, 0f, false);
        }

        public static DialogueAdvancePlan Advance(float delay)
        {
            return new DialogueAdvancePlan(true, delay, false);
        }

        public static DialogueAdvancePlan StopSkip()
        {
            return new DialogueAdvancePlan(false, 0f, true);
        }
    }

    /// <summary>
    /// オート/スキップの進行判断（純ロジック）。Unity 型に依存しないためテスト可能。
    /// </summary>
    public sealed class DialoguePlaybackPlanner
    {
        /// <summary>
        /// 表示待ちになった行に対する進行プランを返す。
        /// </summary>
        /// <param name="mode">現在の再生モード。</param>
        /// <param name="hasChoices">選択肢が出ているか（出ていれば常に手動待ち）。</param>
        /// <param name="isRead">この行が既読か（スキップの既読限定判定に使う）。</param>
        /// <param name="settings">設定（オート待ち秒数・スキップ既読限定）。</param>
        public DialogueAdvancePlan Plan(DialoguePlaybackMode mode, bool hasChoices, bool isRead, DialogueSettings settings)
        {
            if (hasChoices)
                return DialogueAdvancePlan.Wait();

            switch (mode)
            {
                case DialoguePlaybackMode.Skip:
                    if (settings != null && settings.SkipReadOnly && !isRead)
                        return DialogueAdvancePlan.StopSkip();
                    return DialogueAdvancePlan.Advance(0f);

                case DialoguePlaybackMode.Auto:
                    return DialogueAdvancePlan.Advance(settings != null ? settings.AutoAdvanceDelay : 0f);

                default:
                    return DialogueAdvancePlan.Wait();
            }
        }
    }
}
