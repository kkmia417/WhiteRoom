# Packaging

Talk System is distributed as a root UPM package. The repository can also be opened directly as a Unity project for package development and test execution.

## Current Layout

The current package keeps runtime, editor, and tests under `Assets/kkmia/TalkSystem`:

- `Assets/kkmia/TalkSystem/Scripts` contains runtime code and `kkmia.TalkSystem.Runtime.asmdef`.
- `Assets/kkmia/TalkSystem/Editor` contains editor tools and `kkmia.TalkSystem.Editor.asmdef`.
- `Assets/kkmia/TalkSystem/Tests/Editor` and `Assets/kkmia/TalkSystem/Tests/PlayMode` contain test assemblies.
- `Samples~` contains importable Package Manager samples.
- `Documentation~` contains package documentation.

This layout preserves existing `.meta` GUIDs for users who previously copied the folder into a project. The target long-term layout is the UPM-standard `Runtime/`, `Editor/`, `Tests/`, `Samples~`, and `Documentation~` shape.

## Migration Plan

For the next package-breaking release, move source folders in this order:

1. Move `Assets/kkmia/TalkSystem/Scripts` to `Runtime`.
2. Move `Assets/kkmia/TalkSystem/Editor` to `Editor`.
3. Move test assemblies to `Tests/Editor` and `Tests/PlayMode`.
4. Keep `Samples~` and `Documentation~` at the package root.
5. Preserve `.meta` files during moves where Unity allows it; if GUID preservation is not reliable, document the GUID break in the release notes.

Do not mix this folder migration with unrelated runtime API changes. Consumer projects should be validated before and after the move.

## Optional Dependencies

The package declares only required Unity packages in `package.json`. `com.unity.ugui` is required because runtime UI components reference UGUI and TextMeshPro assemblies.

Optional integrations stay isolated:

- Input handling supports legacy input and Input System modes without requiring `com.unity.inputsystem`.
- Live2D and Spine adapters compile only when their scripting defines and SDK assemblies are present.
- Addressables are intentionally not referenced by runtime code; projects can load `TextAsset` data through their own Addressables code and pass it to the repository loader APIs.

Run `Tools/Validate-Package.ps1` to verify package metadata and assembly boundaries. Run `Tools/Validate-ConsumerInstall.ps1` or `Tools/Invoke-ReleaseChecks.ps1 -ImportSamples` before tagging a release.
