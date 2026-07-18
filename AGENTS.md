# WhiteRoom repository instructions

These instructions apply to the entire repository.

## Sources of truth

- Product intent and acceptance criteria live in a GitHub Issue.
- Durable architecture decisions live in canonical English files under
  `docs/adr/`; paired `.ja.md` files are the human-facing Japanese translations.
- Current component boundaries live in `docs/architecture/README.md`.
- Executable behavior and tests outrank prose when documentation describes the
  current implementation.

## Required workflow

1. Start non-trivial work from one bounded Issue.
2. Restate the outcome, non-goals, acceptance criteria, and validation plan
   before editing.
3. Add a proposed English ADR and its Japanese `.ja.md` counterpart before
   implementation when the change meets the criteria in `docs/adr/README.md`.
4. Keep the implementation scoped to the Issue. File follow-up concerns as
   separate Issues instead of expanding the change.
5. Add or update focused tests for behavior changes.
6. Run validation that matches every touched surface.
7. Open a PR that links the Issue with `Closes #<number>` or, for a non-closing
   spike, `Refs #<number>`.

Use `.github/ISSUE_TEMPLATE/` and `.github/pull_request_template.md` as the
required artifact contracts. The detailed lifecycle is documented in
`docs/development/issue-driven-development.md`.

## Architecture constraints

- `NovelGameBootstrap` is the application composition root and scene-lifecycle
  adapter. Keep business rules out of it.
- `Assets/Scripts/Setup` owns object creation and Talk System wiring.
- `Assets/Scripts/Services` owns application use cases and durable state policy.
- `Assets/Scripts/UI` owns presentation controllers and runtime UI construction.
- `Packages/com.kkmia.talksystem` is reusable infrastructure and must not depend
  on `WhiteRoom.Novel`.
- Keep reflection-based compatibility code inside `Assets/Scripts/Setup`.

See `docs/adr/0001-talk-system-boundary.md` for package ownership,
`docs/adr/0002-runtime-responsibility-split.md` for application responsibilities,
and `docs/adr/0003-issue-driven-bilingual-adrs.md` for the ADR/delivery protocol.

## Validation

Always run:

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

For Unity source, package, scene, prefab, or project-setting changes, also run
Unity `6000.3.7f1` in batch mode when the editor is available:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath . -logFile Logs\codex-unity.log
```

Preserve unrelated worktree changes and never hand-edit generated project files.
