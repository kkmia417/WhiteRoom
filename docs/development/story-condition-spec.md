# R00 story-condition specification

Status: Proposed — story-owner approval required before CSV implementation<br>
Related: [Issue #22](https://github.com/kkmia417/WhiteRoom/issues/22)<br>
Japanese counterpart: [日本語版](story-condition-spec.ja.md)

## Current inventory

`r00_escape_talksystem.csv` contains 199 dialogue rows, 20 choice nodes, 68
unique `EventKey` values, 14 unique endings, and no `ConditionKey` values. The
existing events already express four candidate state families:

| Family | Existing facts | Current authored consequence |
| --- | --- | --- |
| Trust | `trust_need_help`, `trust_match_speed`, `save_nagi_emotional`, `save_nagi_hold_door` | Changes the chosen scene text and route marker, but does not filter later choices. |
| Name | `name_rei`, `name_id`, `name_silent`, `name_fake` | Changes the naming scene, but later TRUE choices remain available for every answer. |
| Battery | `battery_found`, `battery_nagi`, `battery_rei`, `battery_destroyed`, `battery_protected`, `battery_handed_to_nagi` | Selects the battery route and immediate consequences, but all three battery endings remain available. |
| Exposure | `exposure_low`, `exposure_medium`, `exposure_high`, `exposure_fast_run`, `exposure_controlled`, `exposure_max` | High/fast/max choices already lead directly to authored BAD END branches; there is no accumulated exposure score. |

The persistence boundary proposed for these run facts is
[ADR-0012](../adr/0012-run-scoped-story-facts.md).

## Proposed reviewed condition table

Only choice entries are filtered. No dialogue row is skipped, so an unavailable
choice cannot accidentally traverse into the middle of an ending. Every affected
node retains an unconditional fallback.

| Choice row | Choice target | Proposed condition | Story meaning | Unconditional fallback |
| --- | --- | --- | --- | --- |
| 400 | 430, “remember the hand” | `event:name_rei` | The emotional save and later name-based TRUE path require Rei to have offered her real name. | 410, 420, and 440 remain available. |
| 654 | 710, “take Nagi's hand” | `event:save_nagi_emotional` | TRUE END requires the earlier emotional decision rather than only the final click. | 700 remains available and reaches the regular ending. |
| 855 | 860, “go to that child” | `event:battery_protected` | HIDDEN END requires Rei to have protected the battery herself. | 870 remains available. |
| 855 | 880, “ask her to say the name” | `event:save_nagi_emotional` | TRUE END+ inherits the real-name requirement through conditional target 430 and requires the emotional save fact. | 870 remains available. |

The conditions use the existing choice syntax, for example
`ナギの手を自分から掴む->710?event:save_nagi_emotional`. No new condition
grammar or Talk System schema is proposed.

## Explicit adoption decisions

- Trust: adopt `save_nagi_emotional` as the reviewed trust fact for TRUE endings.
  Earlier trust-flavored choices still affect prose but are not prerequisites.
- Name: adopt `name_rei` as the prerequisite for the emotional-save choice and
  therefore both TRUE endings.
- Battery: adopt `battery_protected` as the prerequisite for the hidden-child
  ending. Handing the battery to Nagi retains regular/TRUE+ outcomes.
- Exposure: do not add a condition. Its high/fast/max decisions already have
  immediate authored consequences, and inventing a score or threshold would be
  a new story mechanic outside this Issue.

## Canonical route updates

The 14-ending fixture remains one route per unique ending. Only these canonical
choice target sequences change after approval:

| Ending | Current relevant targets | Proposed relevant targets | Reason |
| --- | --- | --- | --- |
| `end_true_name` | `... 60, 360, 440, 540, 620, 710` | `... 60, 360, 430, 540, 620, 710` | Establish `save_nagi_emotional` before conditioned target 710. |
| `end_hidden_underground_child` | `... 440, 580, 590, 830, 860` | `... 440, 580, 590, 820, 860` | Establish `battery_protected` before conditioned target 860. |
| `end_true_name_plus` | `... 440, 580, 590, 830, 880` | `... 430, 580, 590, 830, 880` | Establish the reviewed trust fact; target 430 is itself gated by `name_rei`. |

All other canonical routes remain unchanged. The regular targets 700 and 870
ensure condition-negative states still progress.

## Runtime and validation contract

- New Game clears run facts; global chapter/route/ending unlocks remain.
- Save/Load and reached-boundary snapshots capture and replace the complete run
  fact set. Existing saves without the payload restore an empty set.
- Rollback rebuilds facts from restored history and does not replay events.
- Unknown condition namespaces fail closed; `!event:<key>` remains supported.
- Tests enumerate the positive and negative state of all four conditional
  choices, prove each affected node retains a visible choice, and simulate all
  14 canonical endings.
- Unity EditMode, PlayMode, batch compilation, governance, and Python unit gates
  must pass.

## Pre-approval simulation evidence

A read-only exhaustive simulation applied the four proposed filters in memory to
the unchanged shipped CSV. It reached all 14 unique endings, found no reachable
choice node with zero visible choices, and replayed all 14 canonical route
fixtures after only the three substitutions listed above. This proves the table
is internally coherent; it does not replace story-owner approval.

## Approval record

Approval is intentionally blank. A story owner must approve this table or list
requested amendments in Issue #22 or its implementation PR before any CSV
`ConditionKey` is changed. Approval means accepting the four table rows, the
explicit non-adoption of exposure scoring, and the three route-fixture changes.
