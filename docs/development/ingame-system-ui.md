# In-game system UI

Japanese counterpart: [日本語版](ingame-system-ui.ja.md)

WhiteRoom exposes Config, message-window visibility, and Return to Title from the
in-game command bar. These operations reuse existing settings and scene boundaries;
they do not create another persistent settings object or cross-scene singleton.

## Config overlay

The in-game and Title Config screens are the same `ConfigScreenController` instance,
backed by the `DialogueSettings` owned by `DialoguePlaybackController` and the same
`VersionedDialogueSettingsStore`. Changes persist immediately. Text speed is applied
by the playback controller, and BGM/SE/Voice volumes are applied by the bound audio
player through the shared settings change event.

Opening Config closes other overlays, suspends Back Skip and the current Auto/Skip
mode, and blocks dialogue and command-bar input. Closing or cancelling restores the
playback mode that was active before Config opened. Gameplay input is restored on the
next frame so the closing key/click cannot also advance dialogue.

## Message-window visibility

Hide Message removes the dialogue window, speaker name, body text, choices, and the
command bar. The background and character stage remain visible. The operation does
not clear or advance the current dialogue state.

While hidden, a dedicated recovery input accepts `Space`, `Enter`, `Escape`, left
click, or right click. That input restores the message and command UI only. Dialogue
keyboard input is enabled one frame later, which prevents the recovery action from
also completing or advancing the current line. Auto/Skip is suspended while hidden
and restored afterward.

## Return to Title

`TitleReturnService` marks progress dirty whenever a dialogue line starts and marks it
clean after a successful manual, Quick, or Autosave, or after Load restoration. A dirty
request opens confirmation; cancel restores the exact prior playback/input state. A
clean request, or an explicit confirmation, starts one guarded transition. Repeated
requests are rejected until scene load completes.

Before Title is loaded, WhiteRoom resets playback automation, Backlog, Save/Load,
Config, Collection, Quit and Title-confirmation overlays, message visibility, dialogue
text/choices, stage characters/background, presentation audio, command input, and UI
focus. `NovelGameBootstrap` remains the scene-lifecycle adapter and does not add a new
persistent runtime object.
