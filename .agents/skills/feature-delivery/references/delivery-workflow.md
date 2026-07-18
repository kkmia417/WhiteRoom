# Delivery Workflow

Use this reference for feature implementation tasks.

## Issue Contract

Treat the primary Issue as the scope boundary. Extract:

- desired outcome
- explicit non-goals
- acceptance criteria
- affected surfaces and constraints
- validation plan
- architecture impact

If the user has not supplied an Issue, inspect connected Issue context when
available. Otherwise write an Issue-ready scope in the work summary and state
that repository traceability is incomplete; do not invent an Issue number.

## Planning Heuristics

Write a plan before editing when:

- more than two modules are touched
- a data contract changes
- user-facing behavior changes
- tests or migrations are needed
- the implementation path is ambiguous

Skip a formal plan for narrow one-file fixes, but still inspect context first.

## Audit Checklist

- primary Issue and related ADRs
- current git status
- relevant instructions
- existing tests and commands
- nearby implementation patterns
- public contracts and callers
- docs or examples that mention the behavior

## Implementation Order

1. Confirm or propose the ADR when architecture is affected.
2. Update shared contracts or types.
3. Update producers.
4. Update consumers.
5. Update focused tests.
6. Update docs or examples.
7. Map acceptance criteria to evidence.
8. Run validation.

For UI work, verify responsive behavior and text fit when feasible.

## Validation Selection

- touched unit logic: run targeted tests
- changed type contracts: run type checker
- changed formatting-sensitive files: run formatter or lint
- changed frontend UI: run build and visual smoke checks when available
- changed scripts: run the script on a representative sample
- changed docs commands: execute or clearly mark as unverified
- changed Unity source or assets: run the repository's Unity batch-mode check
  when available
- changed governance artifacts: run
  `python scripts/validate_governance.py --root .`

## Delivery Trace

Before handoff, provide this compact trace:

| Issue acceptance criterion | Implementation | Evidence |
| --- | --- | --- |
| observable criterion | changed file or behavior | test or manual check |

Link the PR with `Closes #<number>` when it completes the Issue. Use
`Refs #<number>` only for partial work or a spike and explain why the Issue
remains open.

## Handoff to Integration QA

Use `$integration-qa` when a change crosses boundaries:

- API to client
- database schema to query code
- script behavior to README command
- route file to navigation link
- config file to runtime loader

Pass the changed files, expected behavior, and validation already run.
