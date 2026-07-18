# WhiteRoom

WhiteRoom is a Unity visual-novel and escape-game project built on the embedded
Talk System package.

## Development

Work starts from a GitHub Issue and ends with a pull request that links the
Issue. Read these documents before making a non-trivial change:

- [Contributing guide](CONTRIBUTING.md)
- [Issue-driven development](docs/development/issue-driven-development.md)
- [Architecture](docs/architecture/README.md)
- [Architecture Decision Records](docs/adr/README.md)

Run the repository governance checks locally with:

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

## Codex Harness

This repository includes Harness for Codex skills under `.agents/skills`.
Use these prompts when working with Codex in this project:

- `Use $codex-harness to design or evolve reusable Codex workflows for this repository.`
- `Use $feature-delivery to implement a bounded feature end to end.`
- `Use $whiteroom-novel-dev to implement WhiteRoom dialogue features with Talk System.`
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
