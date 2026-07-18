# ADR-0007: Separate narrative localization from product UI localization

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8)<br>
Japanese counterpart: [日本語版](0007-localization-source-contract.ja.md)

## Context and problem statement

WhiteRoom's story is authored through Talk System CSV data. Talk System already
supports stable dialogue IDs, translation CSV import/export, runtime text resolvers,
fallback languages, placeholder validation, and preview. A commercial title also
contains menu text, controller prompts, legal text, images, fonts, and audio variants
that are not dialogue rows. Unity Localization provides String Tables, Asset Tables,
pseudo-localization, and standard interchange formats for those product surfaces.

Putting every string into either system would weaken the other workflow. Allowing the
same string to be independently owned by both would create translation drift.

## Decision drivers

- Preserve writer-friendly branching and preview in Talk System.
- Support professional translation, linguistic QA, context, and stable IDs.
- Localize UI and non-dialogue assets with Unity-supported tooling.
- Validate placeholders, fonts, line fit, input prompts, and fallback behavior.
- Avoid two writable sources for one player-visible string.
- Permit language packs to follow the content-delivery architecture.

## Decision outcome

Japanese is the narrative source language. Talk System translation tables are the
authority for dialogue-row speaker names and text. Unity Localization tables are the
authority for product UI strings and non-dialogue localized assets. A string or asset
has exactly one owning system.

### Keep scenario structure and narrative translation keyed by dialogue ID

Branching, conditions, events, progress markers, and source Japanese text stay in
scenario CSV. Localized narrative text is stored in translation CSV units keyed by
the immutable Talk System dialogue `Id`.

**Rationale**: Translators can change wording without changing route topology, and
Talk System can preview and validate the complete row context.
**Impact**: Published dialogue IDs are never renumbered for convenience. Export/import
preserves variables, markup, speaker context, and translator notes. Required locales,
fallback locale, and translation severity are set in validation profiles.

### Use Unity Localization for the product shell and localized assets

Menus, settings, system messages, tutorials, accessibility labels, legal text, input
prompts, localized textures, fonts, and non-dialogue audio use String or Asset Tables.

**Rationale**: These assets follow UI and platform workflows rather than narrative
route topology.
**Impact**: UI code references stable table entries, never literal shipping text.
Locale-specific assets are resolved through the content service. Dialogue text is not
copied into String Tables.

### Make locale selection one product-owned state

A localization service owns the selected locale, supported-locale matrix, fallback
policy, and locale-change transaction. It configures both Talk System's
`IDialogueTextResolver` and Unity Localization.

**Rationale**: Independent locale state would show mixed-language screens.
**Impact**: Locale changes either update all visible surfaces consistently or require
a controlled screen/story reload. Saves record locale preference separately from
narrative progress. Platform locale is an initial suggestion, not hidden authority.

### Block required-locale gaps before release

CI and release builds fail on missing required dialogue/UI entries, placeholder
mismatches, invalid markup, missing fonts/glyphs, or missing required localized
assets. Pseudo-locales and representative long strings run before translation lock.

**Rationale**: Missing text and clipped choices are shipping defects, not warnings.
**Impact**: Development builds visibly mark permitted fallback text. Production
fallback to Japanese is allowed only when the release locale matrix explicitly marks
that surface optional; otherwise the build is rejected.

### Treat language content as versioned content packs

Voice, movies, textures, fonts, and large translation units may ship as locale packs
under ADR-0006 while locale metadata and recovery text remain local.

**Rationale**: Full voice and media can dominate install size.
**Impact**: A locale cannot be selected until its required pack and font coverage are
available. Pack compatibility is declared in the content manifest and validated
against the player build.

## Benefits

- Narrative and UI teams each use tooling suited to their content.
- Stable ownership prevents duplicate translations and drift.
- Professional localization can proceed without editing route logic.
- Language packs reduce mandatory install size.
- Fallback, glyph, and layout defects become release-gated.

## Trade-offs

- Two localization systems must change locale together.
  → One product localization service coordinates both behind one transaction.
- Translators work across narrative and UI exports.
  → Export one release manifest with stable keys, ownership, context, and status.
- Japanese-source fallback may not be acceptable in every market.
  → Required-locale matrices block release where fallback is prohibited.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Put all text in the scenario CSV | Menus, legal text, platform prompts, and localized assets do not belong to narrative topology. |
| Put all dialogue in Unity String Tables | It discards Talk System's row-aware preview, translation validation, and narrative import/export workflow. |
| Allow either system to override the same key | It creates ambiguous authority and locale-dependent drift. |
| Localize only after content lock | It discovers expansion, glyph, grammar, and voice-pack problems too late. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) makes Talk System the narrative runtime.
- [ADR-0006](0006-addressable-content-delivery.md) delivers locale assets and packs.
- [ADR-0009](0009-deterministic-presentation-runtime.md) coordinates subtitle, voice,
  and presentation changes.

## Development rule integration

- Generate and validate a key-ownership manifest across Talk System and Unity tables.
- Run Talk System localization validation with every production scenario profile.
- Run pseudo-locale, glyph coverage, text expansion, subtitle timing, and
  controller-navigation checks on representative screens.
- Require translator context and prohibit renumbering published dialogue IDs.

## Notes

- Unity documents String Tables, Asset Tables, pseudo-localization, and import/export
  in the [Unity 6.3 Localization package](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.localization.html).
- The initial supported-locale list is a product decision and is not fixed here.
- Reconsider the split only if one system can demonstrably replace both workflows
  without losing narrative validation or UI/asset localization.
