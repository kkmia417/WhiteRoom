using System;
using System.Collections.Generic;

namespace WhiteRoom.Novel
{
    public enum NovelCommandId
    {
        ToolbarLock,
        Save,
        DirectSave,
        Load,
        QuickSave,
        QuickLoad,
        SystemConfig,
        PreviousChoice,
        PreviousScene,
        BackSkip,
        PreviousText,
        Backlog,
        Auto,
        Skip,
        NextScene,
        NextChoice,
        Flowchart,
        FavoriteVoiceList,
        VoiceReplay,
        FavoriteVoiceAdd,
        Screenshot,
        HideMessage,
        ReturnTitle
    }

    public enum NovelCommandGroup
    {
        Toolbar,
        SaveLoad,
        Settings,
        BackwardNavigation,
        Playback,
        ForwardNavigation,
        Flowchart,
        Voice,
        System
    }

    public sealed class NovelCommandDefinition
    {
        public NovelCommandDefinition(
            NovelCommandId id,
            NovelCommandGroup group,
            string label,
            string tooltip,
            Action execute,
            Func<bool> isAvailable = null,
            Func<bool> isActive = null,
            string unavailableTooltip = "Not available yet",
            Func<string> unavailableTooltipProvider = null)
        {
            Id = id;
            Group = group;
            Label = label;
            Tooltip = tooltip;
            Execute = execute;
            IsAvailable = isAvailable;
            IsActive = isActive;
            _unavailableTooltip = unavailableTooltip;
            _unavailableTooltipProvider = unavailableTooltipProvider;
        }

        public NovelCommandId Id { get; }
        public NovelCommandGroup Group { get; }
        public string Label { get; }
        public string Tooltip { get; }
        public Action Execute { get; }
        public Func<bool> IsAvailable { get; }
        public Func<bool> IsActive { get; }
        private readonly string _unavailableTooltip;
        private readonly Func<string> _unavailableTooltipProvider;
        public string UnavailableTooltip
        {
            get
            {
                var provided = _unavailableTooltipProvider?.Invoke();
                return string.IsNullOrWhiteSpace(provided) ? _unavailableTooltip : provided;
            }
        }

        public bool CanExecute()
        {
            return Execute != null && (IsAvailable == null || IsAvailable());
        }

        public bool IsSelected()
        {
            return IsActive != null && IsActive();
        }
    }

    public sealed class NovelCommandBarBindings
    {
        public Action OpenSave { get; set; }
        public Action DirectSave { get; set; }
        public Action OpenLoad { get; set; }
        public Action QuickSave { get; set; }
        public Action QuickLoad { get; set; }
        public Action OpenSystemConfig { get; set; }
        public Action PreviousChoice { get; set; }
        public Action PreviousScene { get; set; }
        public Action BackSkip { get; set; }
        public Action PreviousText { get; set; }
        public Action ToggleBacklog { get; set; }
        public Action ToggleAuto { get; set; }
        public Action ToggleSkip { get; set; }
        public Action NextScene { get; set; }
        public Action NextChoice { get; set; }
        public Action OpenFlowchart { get; set; }
        public Action OpenFavoriteVoices { get; set; }
        public Action ReplayVoice { get; set; }
        public Action AddFavoriteVoice { get; set; }
        public Action CaptureScreenshot { get; set; }
        public Action HideMessage { get; set; }
        public Action ReturnTitle { get; set; }

        public Func<bool> CanSave { get; set; }
        public Func<bool> CanQuickLoad { get; set; }
        public Func<bool> CanBackSkip { get; set; }
        public Func<bool> CanPreviousChoice { get; set; }
        public Func<bool> CanPreviousScene { get; set; }
        public Func<bool> CanNextScene { get; set; }
        public Func<bool> CanNextChoice { get; set; }
        public Func<bool> CanOpenFavoriteVoices { get; set; }
        public Func<bool> CanReplayVoice { get; set; }
        public Func<bool> CanAddFavoriteVoice { get; set; }
        public Func<bool> CanOpenSystemConfig { get; set; }
        public Func<bool> CanCaptureScreenshot { get; set; }
        public Func<bool> CanHideMessage { get; set; }
        public Func<bool> CanReturnTitle { get; set; }
        public string ScreenshotUnavailableReason { get; set; }
        public Func<string> PreviousChoiceUnavailableReason { get; set; }
        public Func<string> PreviousSceneUnavailableReason { get; set; }
        public Func<string> NextSceneUnavailableReason { get; set; }
        public Func<string> NextChoiceUnavailableReason { get; set; }
        public Func<string> FavoriteVoiceListUnavailableReason { get; set; }
        public Func<string> VoiceReplayUnavailableReason { get; set; }
        public Func<string> FavoriteVoiceAddUnavailableReason { get; set; }
        public Func<bool> HasDialogue { get; set; }
        public Func<bool> IsBacklogOpen { get; set; }
        public Func<bool> IsBackSkipActive { get; set; }
        public Func<bool> IsAutoActive { get; set; }
        public Func<bool> IsSkipActive { get; set; }
    }

    public static class NovelCommandCatalog
    {
        public static IReadOnlyList<NovelCommandDefinition> Create(NovelCommandBarBindings bindings)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            return new[]
            {
                Command(NovelCommandId.ToolbarLock, NovelCommandGroup.Toolbar, "LOCK", "Lock command bar"),
                Command(NovelCommandId.Save, NovelCommandGroup.SaveLoad, "SAVE", "Open save slots", bindings.OpenSave),
                Command(NovelCommandId.DirectSave, NovelCommandGroup.SaveLoad, "D.S", "Direct save", bindings.DirectSave, bindings.CanSave),
                Command(NovelCommandId.Load, NovelCommandGroup.SaveLoad, "LOAD", "Open load slots", bindings.OpenLoad),
                Command(NovelCommandId.QuickSave, NovelCommandGroup.SaveLoad, "Q.S", "Quick save", bindings.QuickSave, bindings.CanSave),
                Command(NovelCommandId.QuickLoad, NovelCommandGroup.SaveLoad, "Q.L", "Quick load", bindings.QuickLoad, bindings.CanQuickLoad, null, "No quick save data"),
                Command(NovelCommandId.SystemConfig, NovelCommandGroup.Settings, "CFG", "System configuration", bindings.OpenSystemConfig, bindings.CanOpenSystemConfig),
                Command(NovelCommandId.PreviousChoice, NovelCommandGroup.BackwardNavigation, "<C", "Previous choice", bindings.PreviousChoice, bindings.CanPreviousChoice, null, "No previous reached choice", bindings.PreviousChoiceUnavailableReason),
                Command(NovelCommandId.PreviousScene, NovelCommandGroup.BackwardNavigation, "<S", "Previous scene", bindings.PreviousScene, bindings.CanPreviousScene, null, "No previous reached scene", bindings.PreviousSceneUnavailableReason),
                Command(NovelCommandId.BackSkip, NovelCommandGroup.BackwardNavigation, "B.SK", "Back skip", bindings.BackSkip, bindings.CanBackSkip, bindings.IsBackSkipActive),
                Command(NovelCommandId.PreviousText, NovelCommandGroup.BackwardNavigation, "<TXT", "Previous text", bindings.PreviousText, bindings.HasDialogue),
                Command(NovelCommandId.Backlog, NovelCommandGroup.Playback, "LOG", "Backlog", bindings.ToggleBacklog, null, bindings.IsBacklogOpen),
                Command(NovelCommandId.Auto, NovelCommandGroup.Playback, "AUTO", "Auto mode", bindings.ToggleAuto, bindings.HasDialogue, bindings.IsAutoActive),
                Command(NovelCommandId.Skip, NovelCommandGroup.Playback, "SKIP", "Skip read text", bindings.ToggleSkip, bindings.HasDialogue, bindings.IsSkipActive),
                Command(NovelCommandId.NextScene, NovelCommandGroup.ForwardNavigation, "S>", "Next scene", bindings.NextScene, bindings.CanNextScene, null, "No next reached scene", bindings.NextSceneUnavailableReason),
                Command(NovelCommandId.NextChoice, NovelCommandGroup.ForwardNavigation, "C>", "Next choice", bindings.NextChoice, bindings.CanNextChoice, null, "No next reached choice", bindings.NextChoiceUnavailableReason),
                Command(NovelCommandId.Flowchart, NovelCommandGroup.Flowchart, "FLOW", "Flowchart", bindings.OpenFlowchart),
                Command(NovelCommandId.FavoriteVoiceList, NovelCommandGroup.Voice, "FAV", "Favorite voices", bindings.OpenFavoriteVoices, bindings.CanOpenFavoriteVoices, null, "No favorite voices", bindings.FavoriteVoiceListUnavailableReason),
                Command(NovelCommandId.VoiceReplay, NovelCommandGroup.Voice, "VOICE", "Replay current voice", bindings.ReplayVoice, bindings.CanReplayVoice, null, "Current voice is unavailable", bindings.VoiceReplayUnavailableReason),
                Command(NovelCommandId.FavoriteVoiceAdd, NovelCommandGroup.Voice, "+FAV", "Add favorite voice", bindings.AddFavoriteVoice, bindings.CanAddFavoriteVoice, null, "Current voice is unavailable", bindings.FavoriteVoiceAddUnavailableReason),
                Command(
                    NovelCommandId.Screenshot,
                    NovelCommandGroup.System,
                    "SHOT",
                    "Capture screenshot",
                    bindings.CaptureScreenshot,
                    bindings.CanCaptureScreenshot,
                    null,
                    string.IsNullOrWhiteSpace(bindings.ScreenshotUnavailableReason)
                        ? "Screenshot capture is unavailable"
                        : bindings.ScreenshotUnavailableReason),
                Command(NovelCommandId.HideMessage, NovelCommandGroup.System, "HIDE", "Hide message window", bindings.HideMessage, bindings.CanHideMessage),
                Command(NovelCommandId.ReturnTitle, NovelCommandGroup.System, "TITLE", "Return to title", bindings.ReturnTitle, bindings.CanReturnTitle)
            };
        }

        private static NovelCommandDefinition Command(
            NovelCommandId id,
            NovelCommandGroup group,
            string label,
            string tooltip,
            Action execute = null,
            Func<bool> isAvailable = null,
            Func<bool> isActive = null,
            string unavailableTooltip = "Not available yet",
            Func<string> unavailableTooltipProvider = null)
        {
            return new NovelCommandDefinition(
                id,
                group,
                label,
                tooltip,
                execute,
                isAvailable,
                isActive,
                unavailableTooltip,
                unavailableTooltipProvider);
        }
    }
}
