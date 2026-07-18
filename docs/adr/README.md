# Architecture Decision Records

Architecture Decision Records (ADRs) capture durable choices and their
trade-offs. They explain why the repository has its current shape; they are not
a substitute for implementation documentation.

## Index

| ADR | Status | Decision |
| --- | --- | --- |
| [0001](0001-runtime-architecture-boundaries.md) | Accepted | Runtime architecture boundaries |

`0000-template.md` is reserved as the copyable template and is not a decision.

## When an ADR is required

Create an ADR when a proposed change affects at least one of these:

- dependency direction or ownership between modules;
- a public API, dialogue schema, save-data format, or migration strategy;
- scene lifecycle, composition, persistence, or cross-scene state;
- a new package, service, framework, or build/deployment mechanism;
- a cross-cutting quality attribute such as security, performance, reliability,
  observability, or accessibility;
- a choice that is expensive to reverse or likely to be questioned again.

Do not create an ADR for a local implementation detail, routine bug fix,
reversible refactor, or a choice already covered by an accepted ADR.

## Lifecycle

1. Open an Issue with the problem, constraints, options, and acceptance criteria.
2. Copy `0000-template.md` to the next zero-padded number.
3. Set the status to `Proposed` and link the Issue.
4. Compare credible alternatives and record validation evidence.
5. Change the status to `Accepted` before depending on the decision.
6. Treat accepted ADRs as immutable history. To change a decision, add a new ADR
   and mark the old one `Superseded`.

Allowed statuses are `Proposed`, `Accepted`, `Rejected`, `Deprecated`, and
`Superseded`.

## Writing rules

- State one decision per ADR.
- Describe forces and constraints, not only the chosen tool.
- Include negative consequences and migration cost.
- Use repository-relative links.
- Keep volatile task details in the linked Issue.
- Use `YYYY-MM-DD` dates and lowercase kebab-case filenames.

Validate the index, required sections, links, and numbering with:

```powershell
python .\scripts\validate_governance.py --root .
```
