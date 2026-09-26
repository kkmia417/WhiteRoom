# シナリオCSVの編集と読み込み

原稿は [Assets/Dialogue/Chapters](../../Assets/Dialogue/Chapters) の
`chapter_01.csv` ～ `chapter_14.csv` を編集する。全10,648行を章別の337～1,243行に分割した。
CSVはUTF-8で保存する。表計算ソフトではセル内改行・引用符・文字列の自動変換に注意する。

読み込み順は [r00_escape_talksystem.dialogue](../../Assets/Resources/Dialogue/r00_escape_talksystem.dialogue)
の `sources` 配列で定義する。新しい章を追加するときは、同じヘッダーのCSVを作ってこの配列に追加する。
ファイルの移動・名称変更時もパスを更新する。既存の `Id` を振り直さず、章をまたぐ
`NextId` と `Choices` は従来どおり相手のIDを指定する。

Unityは原稿の変更を検知すると、Talk Systemの既存CSV Codec・Validatorで全章を結合して検証し、
実行用の `CompiledDialogueAsset` を自動生成する。生成物はLibraryに置かれ、手編集やコミットは不要。
実行時は同じResourcesキー `Dialogue/r00_escape_talksystem` から解析済みデータを読み、
ID・トリガーの索引を作る。全文のCSV解析とグラフ検証は起動時に行わない。
全行は引き続きメモリに保持するため、章の遅延ロードではない。

空のマニフェスト、欠落ファイル、ヘッダー不一致、重複ID、参照先不在はエラーになる。
Consoleに表示されたエラーを原稿側で修正する。不正なインポートで古い台詞を実行し続けることはできず、
ビルド前にも全マニフェストを検証する。演出アセットの参照検証は既存の
`WhiteRoomDialogueValidationProfile` が担当する。

## 編集後の確認

1. 章のCSVを編集して保存し、Unityのインポートを待つ。
2. `Tools > WhiteRoom > Validate and Measure Compiled Dialogue` を実行する。
   全ソースと解析済みデータの全列・行順・拡張列の一致、演出参照、Repository構築の測定結果をConsoleで確認できる。
3. 分岐を変更したらEditModeの `WhiteRoomScenarioContractTests` で全4エンディングの経路も確認する。

全章をTalk Systemの既存PreviewやGraph Editorで確認したい場合は、Projectで `.dialogue` を選択し、
`Assets > Talk System > Export Combined CSV for Preview` を使う。
`Assets/Editor/DialoguePreview/` に一時CSVが生成されるので、既存の
`Tools > kkmia > Dialogue Preview` / `Dialogue Validator` に指定する。
この出力はGit管理・プレイヤービルドの対象外で、原稿を更新したら再出力する。
変更は必ず章のCSVへ反映する。Graph Editorの書き戻しは独自演出列を失う可能性があるため、
原稿への上書きには使わない。

バッチ検証:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath . `
  -executeMethod WhiteRoom.Novel.Editor.WhiteRoomDialogueCompilationValidation.ValidateFromCommandLine `
  -logFile Logs\dialogue-compilation.log
```

測定値はウォーム状態のRepository構築5回の平均で、Unityのファイル読み込み・デシリアライズや
ゲーム全体の起動時間を含まない。セーブ形式、全ID、台詞、分岐、演出列は移行前から維持している。
設計は [ADR-0012](../adr/0012-compiled-dialogue-authoring.ja.md)、実装範囲は
[Issue #85](https://github.com/kkmia417/WhiteRoom/issues/85) を参照。

## 移行時の検証記録

2026-09-26、Unity 6000.3.7f1のEditorバッチ実行で全10,648行の一致と演出参照を確認した。
Repository構築5回の平均は次のとおり。比較対象のCSVは同じ章別ソースを結合したもの。

| 処理 | 平均時間 |
| --- | ---: |
| CSV解析・グラフ検証・Repository構築 | 75.623 ms |
| 解析済みアセットからの索引構築 | 0.683 ms |

起動全体の測定ではなく、環境やキャッシュ状態で値は変わる。
このUnityランタイムでは割り当てバイト数のカウンターが値を返さなかったため、メモリ削減率は未計測。
EditMode 273件、PlayMode 23件、Python 51件とgovernance・cross-platform検証が通過した。
