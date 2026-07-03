# Tech Stack

- Unity `6000.3.7f1` (`ProjectSettings/ProjectVersion.txt`).
- C# runtime scripts compile through Unity-generated `Assembly-CSharp.csproj` unless new asmdefs are added.
- UPM manifest uses `com.unity.inputsystem` `1.18.0`, `com.unity.ugui` `2.0.0`, and Unity built-in modules.
- UniVRM packages are embedded under `Packages/`: `com.vrmc.gltf`, `com.vrmc.univrm`, `com.vrmc.vrm` from package-lock `file:` dependencies.
- URP assets live under `Assets/Settings`; project has `PC_RPAsset`, `Mobile_RPAsset`, and global URP settings.
