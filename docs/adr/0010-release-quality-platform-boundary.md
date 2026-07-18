# ADR-0010: Gate releases with automated quality evidence and platform adapters

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #23](https://github.com/kkmia417/WhiteRoom/issues/23)<br>
Japanese counterpart: [日本語版](0010-release-quality-platform-boundary.ja.md)

## Context and problem statement

An AAA commercial visual novel is a large data product as much as a Unity executable.
A build can compile while containing an unreachable ending, missing voice, wrong
subtitle language, broken controller focus, corrupt content catalog, incompatible
save, memory spike, or platform-policy violation. Manual playthrough cannot cover all
routes, locales, channels, content packs, and supported hardware on every change.

Platform SDKs for cloud saves, achievements, entitlements, input, user identity,
store overlays, telemetry, and crash reporting also have different lifecycles and
privacy obligations. Direct SDK calls in narrative or UI code would multiply test
matrices and make offline or unsupported capabilities fail unpredictably.

## Decision drivers

- Produce repeatable evidence for every release candidate.
- Catch narrative/data defects before expensive device certification.
- Keep platform and observability vendors outside product rules.
- Define performance, memory, loading, and download budgets per target.
- Diagnose field failures by build and content version without collecting story text.
- Support offline play, consent, and least-data privacy behavior.

## Decision outcome

WhiteRoom uses a layered quality pipeline and immutable release promotion. Platform,
telemetry, and crash capabilities are product-owned ports with target-specific
adapters. A release candidate advances only when its declared automated and manual
evidence satisfies the target profile.

### Build a layered automated test portfolio

The portfolio contains pure policy tests, module integration/EditMode tests, Talk
System and content validation, PlayMode flow tests, headless route simulation,
save-fixture compatibility, representative visual/input tests, and target-device
smoke/soak tests.

**Rationale**: Each layer detects different failures at an appropriate cost.
**Impact**: Every acceptance criterion maps to the lowest reliable layer. Route
simulation covers every trigger, choice target, condition outcome, ending, and content
reference without rendering. Device tests focus on rendering, input, lifecycle,
storage, and SDK behavior rather than replaying every text line.

### Make build and content validation blocking

Release profiles fail on compiler/test errors, dialogue graph errors, missing or
duplicate content IDs, localization gaps, unsupported shaders, Addressables analysis
violations, incompatible save fixtures, missing licenses, or exceeded budgets.

**Rationale**: Warnings that always require release judgment become normalized and
eventually ignored.
**Impact**: A waiver is time-bounded, owned, linked to an Issue, and recorded in the
release manifest. Required Talk System validation profiles run through the command-line
build gate and emit machine-readable reports.

### Produce immutable, traceable release artifacts

Each player/content build records source commit, Unity and package lock, platform
toolchain, build profile, product channel, build ID, content manifest/version,
validation reports, symbols, notices/licenses, and checksums.

**Rationale**: A shipped defect can be diagnosed or rolled back only if its exact
inputs and outputs are known.
**Impact**: Development, QA, certification, and production promote the same immutable
artifact where the platform allows. Rebuilding from the same branch is not considered
promotion. Credentials remain outside source and artifacts.

### Isolate platform capabilities behind product ports

Cloud saves, achievements, entitlements, platform users, rich presence, overlays,
controller ownership, and platform lifecycle use capability-based interfaces.

**Rationale**: The narrative application needs outcomes, not SDK types or platform
names.
**Impact**: Adapters normalize unavailable, offline, cancelled, retryable, and fatal
results. Unsupported optional capabilities degrade explicitly. Entitlement failure
never deletes local progress, and platform-user changes trigger a controlled session
transition.

### Treat observability and privacy as architecture

Structured diagnostics include build ID, content version, product channel, platform
class, operation, duration, result, and non-sensitive correlation IDs. Crash symbols
and breadcrumbs are retained under a documented policy.

**Rationale**: Content and asynchronous failures need field evidence, but narrative
text, choices, names, and save payloads can be sensitive.
**Impact**: Shipping telemetry is allowlisted, schema-versioned, consent/region aware,
buffered safely offline, rate-limited, and disabled when required. Dialogue text,
player-entered names, raw saves, tokens, and filesystem paths are never emitted.

### Enforce target-specific performance and resilience budgets

Every release target declares frame-time percentile, peak/steady memory, startup,
chapter transition, save/load, content download, install size, and long-session
stability budgets.

**Rationale**: "Runs well" is not a testable AAA quality attribute.
**Impact**: CI records trends on stable reference hardware; release candidates run
device smoke and soak suites. Budget regressions fail or require an Issue-linked,
expiring waiver.

## Benefits

- Route, content, save, platform, and performance evidence is repeatable.
- Certification finds fewer preventable defects.
- Vendor SDKs can change without rewriting narrative or UI policy.
- Field diagnostics identify the exact player/content artifact involved.
- Privacy constraints are enforced by schema rather than reviewer memory.

## Trade-offs

- Test infrastructure and reference hardware require sustained investment.
  → Prioritize automation by defect cost and reuse target profiles across releases.
- Strict gates can block a schedule.
  → Use explicit, expiring waivers instead of weakening the baseline silently.
- Adapter normalization hides some vendor detail.
  → Preserve vendor codes inside redacted diagnostics, not product branching.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Rely on manual full playthroughs | Route, locale, content-pack, save-version, and platform combinations exceed feasible manual coverage. |
| Call platform SDKs directly from features | It couples product rules to target lifecycles, error types, and test environments. |
| Rebuild separately for QA and production | It invalidates the evidence gathered on the tested artifact. |
| Collect broad logs and filter later | Sensitive narrative and player data can leave the device before filtering or consent. |

## Related ADRs

- [ADR-0003](0003-issue-driven-bilingual-adrs.md) links evidence and waivers to Issues.
- [ADR-0004](0004-modular-monolith-boundaries.md) defines platform ports.
- [ADR-0005](0005-unity-urp-runtime-baseline.md) defines target profiles and toolchain.
- [ADR-0006](0006-addressable-content-delivery.md) defines immutable content artifacts.
- [ADR-0008](0008-versioned-save-compatibility.md) defines compatibility fixtures.

## Development rule integration

- Require acceptance-criterion-to-evidence mapping in every feature PR.
- Run governance and fast tests on PRs; run content, route, build, and selected device
  suites according to changed paths and release stage.
- Store machine-readable reports and artifact manifests with retention matching the
  product support policy.
- Review telemetry schemas, SDK permissions, data retention, and regional behavior
  before enabling a new event or vendor.

## Notes

- [Unity 6.3 Test Framework](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)
  supports EditMode and PlayMode tests; target-device support and exact CI
  orchestration are separate implementation decisions.
- No build farm, analytics, crash, cloud-save, storefront, or certification vendor is
  selected here.
- Reconsider a gate only from measured false-positive cost and escaped-defect evidence,
  never only because a deadline is near.
