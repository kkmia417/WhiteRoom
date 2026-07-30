# ゲーム中System UI

English canonical file: [English](ingame-system-ui.md)

WhiteRoomはゲーム中command barからConfig、message window表示切替、Title帰還を提供します。
既存settingsとscene境界を再利用し、別の永続settings objectやcross-scene singletonは追加しません。

## Config overlay

ゲーム中とTitleのConfigは同じ`ConfigScreenController` instanceです。
`DialoguePlaybackController`が所有する`DialogueSettings`と、同じ
`VersionedDialogueSettingsStore`を使います。変更は即時保存され、text speedはplayback
controller、BGM/SE/Voice volumeはsettings change eventを購読するaudio playerへ即時反映されます。

Configを開くと他overlayを閉じ、Back Skipと現在のAuto/Skip modeをsuspendし、dialogueと
command barの背面入力をblockします。Close/cancel時はConfigを開く前のplayback modeを復元します。
閉じたkey/clickでdialogueも進まないよう、gameplay inputは次frameで復元します。

## Message window表示

Hide Messageはdialogue window、speaker name、body text、choice、command barを非表示にします。
Backgroundとcharacter stageは表示したままです。現在のdialogue stateをclearまたはadvanceしません。

非表示中は専用recovery inputが`Space`、`Enter`、`Escape`、左click、右clickを受け付けます。
この入力はmessageとcommand UIの復帰だけを行います。Dialogue keyboard inputは1 frame後に有効化し、
復帰操作と同時に現在行をcomplete/advanceしないようにします。非表示中はAuto/Skipをsuspendし、
復帰後に元のmodeへ戻します。

## Title帰還

`TitleReturnService`はdialogue line開始時に進行をdirtyとし、manual/Quick/Autosave成功時または
Load復元後にcleanとします。Dirty状態の要求は確認を開き、cancel時は直前のplayback/input状態へ
戻ります。Clean状態または明示confirmではguard付きtransitionを1回だけ開始し、scene load完了まで
連続要求をrejectします。

Title load前にplayback automation、Backlog、Save/Load、Config、Collection、Quit、Title確認、
message表示、dialogue text/choice、stage character/background、presentation audio、command input、
UI focusをresetします。`NovelGameBootstrap`はscene-lifecycle adapterのままで、新しいpersistent
runtime objectは追加しません。
