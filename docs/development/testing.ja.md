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

- `WhiteRoomScenarioContractTests`は公開CSV 9,904行をparseし、ID一意性、全`NextId`/choice target、
  chapter marker 14件、choice node 2件、condition flag 0件、固有EndingKey 4件を検証する
- `Assets/Tests/Fixtures/r00_ending_routes.json`をreview対象のroute matrixとする。各entryはdialogue ID 1000001から
  4つの固有Endingまでのchoice targetを保持し、testはchoice間の通常`NextId`を追跡する。target欠損、循環、
  未使用choice、想定外Endingをfailureにする
- `scripts/tests/test_import_white_room_novel.py`はreview済みfixtureを使い、発話tagと人物動作を
  取り違えない境界を含む保守的な話者推定を検証する。Importerは原稿の全引用段落7,250件を対象に、
  source index付きの決定的な`docs/development/white-room-speaker-audit.json`を出力できる
- `WhiteRoomProductJourneyPlayModeTests`は実Title/Main sceneをloadし、製品scenarioをEndingまで進め、
  memory save復元、overlay中automation停止、Title帰還、manager/canvas/event-system重複を検証する
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
