# WhiteRoom

## Codex Harness

This repository includes Harness for Codex skills under `.agents/skills`.
Use these prompts when working with Codex in this project:

- `Use $codex-harness to design or evolve reusable Codex workflows for this repository.`
- `Use $feature-delivery to implement a bounded feature end to end.`
- `Use $repo-review to review the current diff.`
- `Use $integration-qa to check cross-boundary risks after changes.`
- `Use $docs-maintenance to update or verify repository documentation.`
- `Use $release-readiness to check whether a branch is ready to ship.`

After editing harness files, install the Harness for Codex CLI and validate them
with:

```powershell
python -m pip install git+https://github.com/kkmia417/HarnessforCodex.git
python -m codex_harness.cli validate .
```
