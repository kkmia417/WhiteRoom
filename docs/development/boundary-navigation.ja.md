# 到達済みscene/choice navigation

English canonical file: [English](boundary-navigation.md)

WhiteRoomのcommand barは4つの到達済みboundary操作を提供します。

- `<C`: 前の到達済みchoice
- `<S`: 前の到達済みscene
- `S>`: 次の到達済みscene
- `C>`: 次の到達済みchoice

Scene boundaryは`ChapterKey`を持つdialogue rowで、identityは
`scene:<chapter-key>:<dialogue-id>`です。Choice boundaryはchoicesを持つrowで、identityは
`choice:<dialogue-id>`です。IdentityはTalk System scenario dataに由来し、command buttonや
表示済みchoice indexをnavigation identityとして使いません。

## 到達範囲の動作

Applicationは到達した各boundaryでcoherent in-memory snapshotを記録します。Previous/nextは
要求kindで最も近い記録済みboundaryだけを選びます。Next commandは未読contentへ入らず、
choice復元後はoptionを選択せずpendingのままです。

Backward jump後、通常dialogue進行を再開するまでは後方checkpointを利用できます。以前の位置から
advance/choiceすると、new checkpoint記録前に古いforward tailを切り捨てるため、非互換branch
stateは混在しません。

一致targetがない4 buttonはdisabledになります。Tooltipは前後の到達済みscene/choiceなし、
navigation busy、runtime未準備、dialogue input blockのいずれかを示します。

## 一貫した復元とfailure

`DialogueBoundaryNavigationService`は
`DialogueSaveSystem.CaptureState(excludedContributor)`と`RestoreState(...)`を使います。
Snapshotにはdialogue choice、seen line、progress、Backlog、stage、BGM、voice contributorを
含みます。Restoreはrowを開始し直さないため、line event、one-shot SE、durable unlock side
effectを再実行しません。

Jump実行はAuto、Skip、backward skipを停止し、競合overlayを閉じ、restore完了までdialogue、
command bar、save inputをblockします。Missing row、condition failure、repeated cycle、invalid
snapshot、contributor failure、targetなしは分類済みresultを返し、現在dialogueを利用可能なまま
残します。

## Save compatibility

Reached checkpoint、cursor位置、cycle markerをversioned JSONとして
`DialogueSaveData.ExtraState`の`whiteroom.boundary-navigation.v1`へ保存します。Nested checkpointは
navigation contributor自身を除外します。このkeyを持たない既存saveも有効で、復元した現在
boundaryからnew timelineを開始します。Malformed/future payloadはwarning付きで無視し、基礎saveを
rejectしません。

永続architecture契約は[ADR-0011](../adr/0011-reached-boundary-navigation.ja.md)を参照してください。
