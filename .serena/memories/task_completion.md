# Task Completion

- For Unity package/source edits, verify with Unity batchmode when available: `Unity -batchmode -quit -projectPath . -logFile Logs/codex-unity.log` or the locally installed Unity executable path.
- If Unity is already open or no executable is on PATH, at minimum run focused text checks (`rg`) and inspect changed files; report that Unity compilation/playmode was not run.
- Check worktree with `git status --short` before final response.
- Serena memory references can be checked by running `serena memories check` from the project root.
