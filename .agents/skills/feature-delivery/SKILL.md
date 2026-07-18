---
name: feature-delivery
description: Deliver Issue-driven repository features across code, tests, docs, ADRs, and integration boundaries. Use when asked to implement a GitHub Issue, build a feature, make a product change, update behavior end to end, or continue bounded feature work with validation.
---

# Feature Delivery

Deliver one bounded Issue end to end while preserving the repository's
architecture, decision history, and validation flow.

## Workflow

1. Identify the primary Issue and extract its outcome, non-goals, acceptance
   criteria, and validation plan. If no Issue exists, prepare an Issue-ready
   scope before implementation and make the missing traceability explicit.
2. Audit the current git state, project instructions, and relevant conventions.
3. Identify affected modules, contracts, tests, documentation, and ADRs.
4. Check `docs/adr/README.md`; create or update a proposed ADR before code when
   the change meets its decision criteria.
5. Produce a short implementation plan when the change spans multiple files or
   behaviors.
6. Implement the smallest vertical slice that satisfies the Issue.
7. Add or update focused tests and map them to acceptance criteria.
8. Run validation commands that match every touched surface.
9. Use `$integration-qa` when frontend/backend, docs/scripts, config/runtime, or
   schema/query boundaries changed.
10. Summarize the Issue mapping, files changed, validation results, ADR impact,
    remaining risk, and follow-up Issues.

Read `references/delivery-workflow.md` for planning and validation details.

## Constraints

- Prefer existing project patterns over new abstractions.
- Keep unrelated refactors out of scope.
- Preserve user edits in the worktree.
- Make validation failures explicit if they cannot be fixed in scope.
- Do not silently expand acceptance criteria. Record separate work as follow-up
  Issues.
- Do not claim an Issue is closed or a PR is linked unless external state
  confirms it.

## Output

Return:

- what changed
- primary Issue and acceptance-criterion mapping
- validation run
- ADR impact
- user-facing behavior impact
- follow-up Issues if the feature exposed separate work
