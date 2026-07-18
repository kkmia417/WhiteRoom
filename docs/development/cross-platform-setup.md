# Windows and macOS development setup

WhiteRoom keeps repository text, Unity serialization, and file names portable so
that Windows and macOS developers can collaborate without platform-only diffs.

## Required tools

- Git
- Python 3.13 (the version used by repository CI)
- Unity `6000.3.7f1`, installed with Unity Hub

Clone the repository into a reasonably short path, open a terminal in the
repository root, and run:

```text
python scripts/check_cross_platform.py --root .
python scripts/validate_governance.py --root .
python -m unittest discover -s scripts/tests -p "test_*.py"
```

These commands use forward-slash paths and work unchanged in PowerShell, Command
Prompt, zsh, and bash.

## Repository-owned defaults

- `.gitattributes` stores text as LF and checks it out as LF on both platforms.
  Windows `.bat` and `.cmd` files are the only CRLF exception.
- Known binary assets are marked as binary so Git never performs line-ending
  conversion or a text merge.
- `.editorconfig` fixes UTF-8, final newlines, indentation, and line endings in
  editors that support EditorConfig.
- Unity uses **Force Text** serialization and **Visible Meta Files**. The
  preflight checks both settings.

These repository rules override `core.autocrlf` for tracked paths. Do not require
contributors to change their global Git configuration for this project.

## File naming contract

All committed paths must:

- be unique after case folding, because common Windows and macOS volumes are
  case-insensitive;
- use Unicode NFC normalization;
- avoid Windows reserved names, forbidden characters, and trailing spaces or
  dots.

Run the preflight before committing a rename. On a case-insensitive filesystem,
perform a case-only rename through a temporary name:

```text
git mv Assets/OldName Assets/temporary-name
git mv Assets/temporary-name Assets/NewName
```

Commit Unity assets together with their matching `.meta` files. Close Unity
before switching branches when it has unsaved imports or scene changes.

## Existing clones and troubleshooting

After pulling `.gitattributes`, Git should keep the working tree clean because
the repository already stores text with LF endings. If every line of a file
appears changed:

1. confirm the editor is using the repository `.editorconfig`;
2. inspect the effective rule with `git check-attr text eol -- path/to/file`;
3. restore or convert only the affected file; do not run a repository-wide
   formatter in a feature branch.

If a checkout fails because of path length on Windows, clone closer to the drive
root. If a filename fails the preflight, rename it on the platform where it is
accessible and commit the rename as a dedicated change.
