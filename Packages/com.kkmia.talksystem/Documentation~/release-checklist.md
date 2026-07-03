# Release Checklist

Run the full release check before tagging:

```powershell
powershell -ExecutionPolicy Bypass -File ./Tools/Invoke-ReleaseChecks.ps1 -ImportSamples
```

Use `-UnityPath` when Unity is not discoverable from `UNITY_EDITOR` or Unity Hub:

```powershell
powershell -ExecutionPolicy Bypass -File ./Tools/Invoke-ReleaseChecks.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.0.40f1\Editor\Unity.exe" -ImportSamples
```

The command runs:

- package metadata validation
- documentation link checks
- Unity EditMode tests
- Unity PlayMode smoke tests
- clean consumer-project install validation
- Feature Tour sample import validation when `-ImportSamples` is set

Before dispatching the GitHub release workflow:

1. Update `package.json` and `CHANGELOG.md` to the same version.
2. Run the full command above locally or in a release validation machine.
3. Confirm the `Unity Tests` and `Package Validation` workflows are green for the commit.
4. Dispatch the `Release` workflow with the exact package version.
