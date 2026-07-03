# Installation

## Unity Package Manager

Use **Window > Package Manager > Add package from git URL**:

```text
https://github.com/kkmia417/TalkSystem.git
```

This repository is structured as a root UPM package and can also be opened as a Unity project for development.

For production projects, pin a release tag or exact commit instead of tracking `main`:

```text
https://github.com/kkmia417/TalkSystem.git#v0.2.0
https://github.com/kkmia417/TalkSystem.git#<commit-sha>
```

## Minimum Unity Version

Talk System targets Unity `6000.0` or newer.

## Package Dependencies

The package declares the Unity package dependencies required by its runtime assembly:

| Package | Version | Why |
| --- | --- | --- |
| `com.unity.ugui` | `2.0.0` | Provides `UnityEngine.UI` and `Unity.TextMeshPro` on Unity 6000.x. |

Optional integrations are not installed automatically:

- Live2D Cubism adapters compile only when `TALKSYSTEM_LIVE2D` is defined and the Cubism SDK is installed.
- Spine adapters compile only when `TALKSYSTEM_SPINE` is defined and spine-unity is installed.

## Consumer Install Validation

Run the local consumer-project check before tagging a package release:

```powershell
powershell -ExecutionPolicy Bypass -File ./Tools/Validate-ConsumerInstall.ps1
```

The script creates a temporary clean Unity project, installs this package through UPM, and runs Unity in batch mode to verify compile/import resolution. To validate the package as a Git dependency, pass a pinned source:

```powershell
powershell -ExecutionPolicy Bypass -File ./Tools/Validate-ConsumerInstall.ps1 -PackageSource "https://github.com/kkmia417/TalkSystem.git#v0.2.0"
```

To also import and compile the Feature Tour sample, add `-ImportSamples`.

For the complete pre-release gate, run:

```powershell
powershell -ExecutionPolicy Bypass -File ./Tools/Invoke-ReleaseChecks.ps1 -ImportSamples
```

This wraps package validation, documentation link checks, Unity EditMode/PlayMode tests, clean consumer install validation, and optional sample import validation. See [Release Checklist](release-checklist.md).

## Samples

After installing through UPM, open the package in Package Manager and import the **Feature Tour** sample.
