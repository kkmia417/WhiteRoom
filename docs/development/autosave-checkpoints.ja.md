# Autosave checkpointとContinue選択

English canonical file: [English](autosave-checkpoints.md)

Thumbnail captureの詳細: [Save thumbnail](save-thumbnails.ja.md)

WhiteRoomはTalk Systemのslot `0`を単一のAutosave専用slotとして使います。Autosave成功時は
常にこのslotを置き換えます。複数世代のrotationは対象外です。

## Checkpoint

Autosaveを要求するstory eventは次の3種類だけです。

- 新しいchapterへ初めて到達した行が表示完了したとき
- choice確定後、遷移先の最初の行が表示完了したとき
- ending unlock永続化後、最終行をplayerが確定したとき

Chapterとchoice後の要求は、text表示中またはchoice UI表示中は保留し、遷移先行が
`WaitingForInput`になった時点で1回だけ書き込みます。Endingは最終text確定後、
`DialogueEnded`が現在行をclearする前の`LineCompleted`で書き込みます。Capture中は
Auto/SkipをNormalへ一時的に戻します。Load後もNormalから再開しますが、playerが保存した
text/audio/Auto/Skip設定値は変更しません。

各checkpointはTalk Systemのnarrative stateと全registered save contributorを同じsnapshotへ
captureします。Choice record、progress marker、stage、audioを含みます。Restore時の再描画は
line eventやprogress markerを再発火しないため、確定済みchoiceやending unlockを二重適用しません。

Autosave failureはUI通知とwarningへ変換し、その要求を消費してdialogueを継続します。
Frameごとの再試行は行いません。後続の別checkpointでは再び保存を試行できます。

## Continue候補

Continueはload可能なmanual、quick、autosaveをすべて比較し、timestampが最新のものを選びます。
Categoryは新しいtimestampを上書きしません。同一秒の場合は既存Talk Systemのslot順に従い、
manual（最大のslot index）、autosave `0`、quick save `-1`の順です。Corrupt、非互換、その他
load不能なslotは候補外です。

TitleのContinue有効判定と実際のloadは同じ規則を使います。
