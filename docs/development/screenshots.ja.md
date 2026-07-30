# プレイヤーScreenshot

English canonical file: [English](screenshots.md)

WhiteRoomは`SHOT` commandまたは割当可能な`NovelGameBootstrap.screenshotShortcut`
（既定`F12`）からfull-resolution PNGを1枚撮影します。このuse caseはsave thumbnailと分離され、
resize、crop、thumbnail byte上限、save-slot sidecarを利用しません。

## Capture/UI contract

- gameplay backgroundとcharacter stageは常に含める
- dialogue/name/text/choiceは現在表示されている場合だけ含める。Hide Message中にshortcutで
  撮影した場合、その非表示状態を維持して撮影する
- command bar、tooltip、一時notificationはcapture frameだけ除外し、完了後に復元する
- Auto、Skip、dialogue visibility、focus、`Time.timeScale`を変更しない。Command bar controlが
  focusを持っていた場合、有効なままなら復元する
- 同時captureは1件だけ。連打は非致命のBusy resultとなり、完了後にcommand availabilityが戻る
- failure pathを含め、encode後にsource `Texture2D`を必ず破棄する

## Storage/platform policy

`FileScreenshotStorage`は`Application.persistentDataPath/Screenshots`へ保存します。File nameは
UTC millisecondを使い、例は`WhiteRoom_20260730_120102_345Z.png`です。既存fileは上書きせず、
衝突時は`_001`、`_002`の順でsuffixを追加し、adapterもcreate-new semanticsで書き込みます。
File nameはpolicyが生成し、player-entered pathは受け付けません。

Desktop/native playerはfile adapterを使います。WebGL player buildはunsupportedとして理由付きで
commandをdisableします。成功notificationは生成file nameと保存directoryの両方を含みます。
Unsupported、Busy、capture、encoding、storage failureを分類して非致命resultとして表示し、
dialogue/save stateは変更しません。
