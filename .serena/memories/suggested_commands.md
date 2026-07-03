# Suggested Commands

- List files quickly: `rg --files` from project root.
- Search code/assets: `rg "pattern" Assets Packages ProjectSettings`.
- Git status: `git status --short`.
- Read manifest: `Get-Content -Raw Packages/manifest.json`.
- Read Unity version: `Get-Content -Raw ProjectSettings/ProjectVersion.txt`.
- On Windows PowerShell, prefer `Get-ChildItem`/`Get-Content` and avoid unix-only command forms.
