# Contributing to WhiteRoom

WhiteRoom uses an Issue-driven delivery flow. A change is complete only when its
intent, decision, implementation, and verification can be traced.

## Before coding

1. Create or select one Issue using the appropriate form.
2. Make the Issue independently testable. Split unrelated outcomes.
3. Confirm the acceptance criteria and validation plan.
4. Check whether an ADR is required by
   [the ADR policy](docs/adr/README.md).
5. Branch from `main` with a traceable name such as
   `feature/123-ending-list` or `fix/123-save-slot`.

Small typo fixes may use a lightweight task Issue, but they still need a linked
Issue when delivered through a PR.

## While coding

- Prefer the smallest coherent change that satisfies the Issue.
- Preserve the dependency direction in
  [the architecture guide](docs/architecture/README.md).
- Put generic dialogue-engine behavior in Talk System and WhiteRoom-specific
  policy in `Assets/Scripts`.
- Add focused tests before broad refactors.
- Do not bundle cleanup that is not required by the acceptance criteria.

If new information changes the outcome or architecture, update the Issue and,
when applicable, the proposed ADR before continuing.

## Pull requests

Use the PR template and include `Closes #<number>`. Use `Refs #<number>` only
when the PR intentionally does not complete the Issue.

A reviewer must be able to verify:

- the Issue acceptance criteria map to code or tests;
- architecture-impacting choices are recorded;
- validation results are reproducible;
- Unity assets include their `.meta` files;
- unrelated worktree changes are absent.

## Local checks

From the repository root:

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

Run Unity batch mode for changes to C#, packages, scenes, prefabs, or project
settings. The expected editor version is recorded in
`ProjectSettings/ProjectVersion.txt`.

## Definition of done

- The linked Issue's acceptance criteria are satisfied.
- Tests and relevant manual checks pass.
- Documentation and ADRs match the delivered behavior.
- The PR explains remaining risk and follow-up Issues.
- The PR is small enough to review as one decision.
