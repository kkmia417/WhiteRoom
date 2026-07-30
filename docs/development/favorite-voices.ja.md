# お気に入りVoice再生と永続化

English canonical file: [English](favorite-voices.md)

現在行のVoice Replayとお気に入りVoiceの製品方針はWhiteRoomが所有する。
再利用可能なTalk SystemはVoice cue、clip解決、単一Voice再生channelを引き続き
所有し、お気に入りの存在は認識しない。

## プレイヤー動作

- `VOICE`と`+FAV`は、現在行のVoice keyをactiveな`AudioDatabase`で解決できる
  場合だけ有効になる。
- 現在行Replayと一覧再生は要求したclipを開始する前にVoice channelを停止する。
  連打しても複数のvoice sourceが重ならない。
- `FAV`は有効なお気に入りが1件以上ある場合だけ開く。一覧には安定した登録順
  番号、speaker、現在localeの台詞text、dialogue IDを表示する。Play、Stop、
  Remove、Backは通常の選択可能buttonで、Escapeは閉じる、Sは停止を行う。
- 一覧を開くとAuto、Skip、背面のdialogue入力を停止する。閉じる、Load、scene
  遷移ではお気に入り再生を停止してから本編へ戻る。
- 最後のentryを削除した場合、開いている一覧はEmpty Stateを表示する。command bar
  の生成・更新時に0件なら`FAV`はdisabledになる。

## 永続データ

`FavoriteVoiceService`はPlayerPrefs key
`WhiteRoom.FavoriteVoices.Json`へversion付きJSON documentを保存する。identityは
`DialogueId + VoiceKey`であり、表示textやasset pathを永続化しない。一覧を構築する
たびにactiveなlocalized dialogue repositoryからspeakerとtextを解決し直す。

schema version 1は安定した登録順を保存する。version 0はsource順を登録順として
migrateする。重複recordは統合し、未知dialogue IDと現在のVoice keyが一致しない
recordは除外する。既知recordのclipだけが一時的に欠損した場合は一覧に残し、Playを
disabledにして削除可能にする。破損、未来version、読込不能、書込不能なdataは警告を
出すがdialogue起動を妨げない。

これは製品所有のversion付きpreferenceであり、Talk Systemのsave contributorや
save-slot sectionではない。[ADR-0008](../adr/0008-versioned-save-compatibility.ja.md)
のmigrationとsafe-failure原則に従い、package APIとsave-envelope schemaは変更しない。

## 現行releaseのVoice方針

[現行Voice方針](../assets/voice.md)により、出荷scenarioにはVoice cueがなく、出荷
audio databaseにもvoice clipはない。そのため現行content buildでは3つのReplay・
お気に入りcommandをすべてdisabledにする。実装とfixture testは、承認済みrecordingを
導入する将来releaseに備えて維持する。
