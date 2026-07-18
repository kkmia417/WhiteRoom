# Issue-driven development

The repository uses one traceable delivery chain:

`Issue -> ADR when needed -> branch -> implementation and tests -> PR -> merge`

The Issue owns the outcome. The ADR owns a durable technical decision. The PR
owns the reviewable implementation and evidence.

## 1. Shape the Issue

Select the smallest Issue form that fits the work:

- Feature: a user or developer outcome.
- Bug: a reproducible gap between expected and actual behavior.
- Architecture decision: a durable choice that needs alternatives and review.
- Task: bounded maintenance without new product behavior.

An implementation-ready Issue has:

- a problem or outcome, not only a proposed solution;
- explicit non-goals;
- observable acceptance criteria;
- affected surfaces and known constraints;
- a validation plan;
- an architecture-impact answer.

Split the Issue when it has independently valuable outcomes, unrelated
acceptance criteria, or different rollback paths.

## 2. Decide before building

Use `docs/adr/README.md` to decide whether an ADR is required. A proposed ADR
must link the Issue and compare credible alternatives. Accept or reject it
before implementation depends on the choice.

Time-box uncertain work as a spike. A spike produces evidence and a follow-up
decision; it does not silently become production code.

## 3. Implement vertically

Prefer a thin end-to-end slice over isolated layers that cannot be verified.
Keep commits and the final PR scoped to the Issue. When a separate defect or
refactor appears, record it as a follow-up Issue.

Branch names include the Issue number:

```text
feature/123-ending-list
fix/123-corrupt-save
docs/123-adr-index
```

## 4. Validate from acceptance criteria

Map each acceptance criterion to at least one of:

- an automated test;
- a deterministic repository check;
- a documented manual Unity check with expected evidence.

Run the narrowest useful tests first, then boundary and Unity validation. Do not
report a command as passed unless it was executed.

## 5. Deliver through a linked PR

The PR begins with `Closes #<number>` when it completes the Issue. Use
`Refs #<number>` for a spike or partial change and explain why the Issue stays
open.

The PR body must include:

- outcome and scope;
- acceptance-criterion evidence;
- architecture impact and ADR link;
- exact validation commands and results;
- risks, rollback notes, and follow-up Issues.

The governance workflow rejects PRs without an Issue reference.

## Triage policy

Use GitHub's existing `bug`, `enhancement`, and `documentation` labels for type.
During triage, add one priority marker in the Issue title or create repository
labels before automating priority:

- P0: release or data-loss blocker;
- P1: main workflow broken, no practical workaround;
- P2: planned product work or meaningful defect;
- P3: improvement, cleanup, or exploration.

Do not put unknown label names in Issue forms: GitHub only applies labels that
already exist in the repository.

## Review contract

Review in this order:

1. Does the change satisfy the linked Issue without hidden scope?
2. Is the dependency direction consistent with the accepted ADR?
3. Can the behavior be understood and tested without incidental complexity?
4. Are failure paths, save compatibility, and Unity lifecycle handled?
5. Are tests and documentation sufficient to prevent regression?

Style feedback is secondary to correctness, clarity, and architecture.
