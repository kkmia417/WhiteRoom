# WhiteRoom test・route coverage契約

English canonical file: [English](testing.md)

WhiteRoom固有testは再利用可能なTalk System package testから分離します。明示的な
`WhiteRoom.EditModeTests`と`WhiteRoom.PlayModeTests` assemblyがscenario contractと製品統合を担当し、
package testは`Packages/com.kkmia.talksystem`配下に残します。

## Local command

Unity `6000.3.7f1`のinstallとlicense認証が必要です。WindowsではUnity CI runnerと同じrepository
scriptで両suiteを実行します。

```powershell
.\scripts\run_unity_tests.ps1
```

Editorが既定外の場所にある場合は`UNITY_EDITOR_PATH`または`-UnityPath`を指定します。scriptは既定で
`TestResults/`へ`editmode-results.xml`、`editmode-editor.log`、
`playmode-results.xml`、`playmode-editor.log`を出力します。suite失敗またはXML未生成時はnon-zeroで終了します。

repository levelの検証は次のままです。

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

## Scenario・journey coverage

- `WhiteRoomScenarioContractTests`は公開CSV 10,648行をparseし、ID一意性、全`NextId`/choice target、
  1 turn 40文字の上限、chapter marker 14件、choice node 2件、condition flag 0件、固有EndingKey 4件を
  検証する。レイ単独chapter境界の立ち絵状態もsimulationし、直前のナギが残らないことを確認する
- `Assets/Tests/Fixtures/r00_ending_routes.json`をreview対象のroute matrixとする。各entryはdialogue ID 1000001から
  4つの固有Endingまでのchoice targetを保持し、testはchoice間の通常`NextId`を追跡する。target欠損、循環、
  未使用choice、想定外Endingをfailureにする
- 冒頭の話者回帰セットは、職員/ユイ、教官/ユイ、レイ/ユイ/アサヒ、レイ/ナギの会話を
  公開row ID単位で検証し、途中の地の文によるnameplateのずれを防ぐ
- `scripts/tests/test_import_white_room_novel.py`は発話tagの方向、発話と人物動作の境界、文末優先で
  読点終わりを作らない分割、review済みの台詞調整、fragment IDの安定性、40文字上限を検証する。
  repository内のledgerと監査は引用7,250件を未解決0件で覆い、
  source mapは原稿14,156 paragraphを未追跡0件で覆う
- `WhiteRoomProductJourneyPlayModeTests`は実Title/Main sceneをloadし、製品scenarioをEndingまで進め、
  memory save復元、overlay中automation停止、Title帰還、manager/canvas/event-system重複を検証する
- `WhiteRoomPlayModeStartupSmokeTests`は本番`NewGameButton`をclickし、dialogue ID 1000001の開始前に
  `Main`がloadされること、unexpected startup logやruntime UI重複がないことを検証する。さらに
  [会話motion契約](dialogue-motion-spec.ja.md)として、左右の話者focus、地の文のneutral復帰、
  placeholder 2体の表示、choice reveal完了を確認する。stage transitionではcut完了、restoreによる
  章transition cancel、overlayがinputを遮らない設定を確認する。章題UIではsafe area右上配置、章番号・章題分離、
  通常window抑制、次行での復帰、restore cancelを確認する
- `WhiteRoomBoundaryNavigationPlayModeTests`はbranch timelineへ到達し、前後のscene/choice復元、
  line eventを再実行しないpresentation/Backlog一貫性、save slotからの到達target再読込を検証する
- Auto、Skip、Rollback、Backlog、manual/Quick/Auto Save、thumbnail、Config、collection、Ending、
  お気に入りVoiceのReplay・一覧lifecycle、Screenshotの詳細は既存のfocused PlayMode testが
  継続して担当する。お気に入りVoiceのstable identity、ordering、migration、deduplication、
  missing-data方針はEditMode testが別途担当する

Save fixtureは必ずmemory storageまたは一意なtemporary directoryを注入し、開発者の実save slotを
読み書きしません。

## CI artifact

`.github/workflows/unity-tests.yml`はlicense認証済みで`unity-6000.3.7f1` labelを持つWindows
self-hosted runner上で同じPowerShell commandを実行します。repository operatorは
`UNITY_CI_ENABLED=true`を設定し、`UNITY_EDITOR_PATH`へEditor executableを指定します。Unity accountや
license payloadをrepository secretへ保存せず、Unity Personal licenseはUnity Hubだけで認証する方針です。

workflowはsuite failure時も2つのNUnit XMLと2つのEditor logを1 artifactとしてuploadします。
license済みrunnerを登録するまでは`UNITY_CI_ENABLED`を未設定とし、その間はlocal validationを必須とします。
