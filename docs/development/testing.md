# WhiteRoom test and route-coverage contract

Japanese counterpart: [日本語版](testing.ja.md)

WhiteRoom keeps product tests separate from the reusable Talk System package. The
explicit `WhiteRoom.EditModeTests` and `WhiteRoom.PlayModeTests` assemblies cover the
scenario contract and product integration; package tests remain under
`Packages/com.kkmia.talksystem`.

## Local command

Unity `6000.3.7f1` must be installed and licensed. On Windows, run both suites with the
same repository script used by the Unity CI runner:

```powershell
.\scripts\run_unity_tests.ps1
```

Set `UNITY_EDITOR_PATH` or pass `-UnityPath` when the Editor is installed elsewhere.
The script writes `editmode-results.xml`, `editmode-editor.log`,
`playmode-results.xml`, and `playmode-editor.log` under `TestResults/` by default and
returns a non-zero exit code when either suite fails or omits its XML result.

The repository-level validation remains:

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

## Scenario and journey coverage

- `WhiteRoomScenarioContractTests` parses all 199 published CSV rows and verifies
  unique IDs, every `NextId` and choice target, the 20 choice-node baseline, and all
  14 unique ending keys.
- `Assets/Tests/Fixtures/r00_ending_routes.json` is the reviewed route matrix. Each
  entry records choice targets from dialogue ID 1 to one unique ending. The test
  follows normal `NextId` edges between choices and fails on a missing target, a
  cycle, an unused choice, or an unexpected ending.
- `WhiteRoomProductJourneyPlayModeTests` loads the real Title and Main scenes, advances
  the shipped scenario through an ending, restores an in-memory save, validates
  overlay automation suspension, returns to Title, and detects duplicate manager,
  canvas, or event-system instances.
- `WhiteRoomBoundaryNavigationPlayModeTests` reaches a branching timeline, exercises
  previous/next scene and choice restore, verifies presentation and Backlog coherence
  without replaying line events, and reloads reached targets from a save slot.
- Focused PlayMode tests continue to own Auto, Skip, Rollback, Backlog, manual/Quick/
  Auto Save, thumbnail, Config, collection, ending, and screenshot behavior.

Save fixtures always inject memory storage or a unique temporary directory. They must
not read from or write to a developer's real save slots.

## CI artifacts

`.github/workflows/unity-tests.yml` runs the same PowerShell command on a licensed
Windows self-hosted runner carrying the `unity-6000.3.7f1` label. Repository operators
enable it with `UNITY_CI_ENABLED=true` and set `UNITY_EDITOR_PATH` to the Editor
executable. This avoids putting a Unity account or license payload in repository
secrets; Unity Personal licenses can only be activated through Unity Hub.

The workflow uploads both NUnit XML files and both Editor logs as one artifact even
when a suite fails. `UNITY_CI_ENABLED` remains unset until such a licensed runner is
registered; local validation is mandatory in that state.
