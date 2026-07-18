# ADR-0007: Narrative localizationとproduct UI localizationを分離する

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8)<br>
English canonical file: [英語正本](0007-localization-source-contract.md)

## コンテキストと問題提起

WhiteRoomのstoryはTalk System CSVでauthoringされる。Talk Systemはstable dialogue ID、
translation CSV import/export、runtime text resolver、fallback language、placeholder validation、
previewを既に持つ。商用titleにはdialogue row以外にもmenu text、controller prompt、legal text、
image、font、audio variantが存在する。Unity LocalizationはそれらにString Table、Asset Table、
pseudo-localization、標準interchange formatを提供する。

全stringをどちらか一方へ置くと、もう一方のworkflowが弱くなる。同じstringを両方が独立所有すると
translation driftが発生する。

## 決定要因

- Talk Systemのwriter-friendly branching/previewを維持すること
- professional translation、linguistic QA、context、stable IDを支えること
- UI/non-dialogue assetをUnity-supported toolingでlocalizeすること
- placeholder、font、line fit、input prompt、fallback behaviorを検証すること
- 1つのplayer-visible stringに2つのwritable sourceを持たないこと
- language packをcontent-delivery architectureへ適合させること

## 決定結果

日本語をnarrative source languageとする。Talk System translation tableをdialogue-row speaker名と
textのauthorityにする。Unity Localization tableをproduct UI stringとnon-dialogue localized
assetのauthorityにする。1つのstring/assetは必ず1つのsystemだけが所有する。

### scenario構造とnarrative translationをdialogue IDで結ぶ

Branch、condition、event、progress marker、source日本語textをscenario CSVへ置く。Localized
narrative textは不変Talk System dialogue `Id` をkeyとするtranslation CSV unitへ置く。

**根拠**: Translatorはroute topologyを変えず文言を変更でき、Talk Systemはrow context全体を
preview/validateできる。
**影響**: Publish済みdialogue IDを都合でrenumberしない。Export/importでvariable、markup、
speaker context、translator noteを維持する。Required locale、fallback locale、translation
severityをvalidation profileへ設定する。

### product shellとlocalized assetにUnity Localizationを使う

Menu、settings、system message、tutorial、accessibility label、legal text、input prompt、
localized texture、font、non-dialogue audioはString/Asset Tableを使う。

**根拠**: これらのassetはnarrative route topologyでなくUI/platform workflowに従う。
**影響**: UI codeはliteral shipping textでなくstable table entryを参照する。Locale-specific
assetはcontent serviceから解決する。Dialogue textをString Tableへcopyしない。

### locale selectionを1つのproduct所有stateにする

Localization serviceがselected locale、supported-locale matrix、fallback policy、
locale-change transactionを所有し、Talk Systemの `IDialogueTextResolver` とUnity Localizationを
両方設定する。

**根拠**: 独立locale stateはmixed-language screenを発生させる。
**影響**: Locale変更は全visible surfaceを一貫更新するか、controlled screen/story reloadを
要求する。Saveにはlocale preferenceをnarrative progressと別に記録する。Platform localeは
初期suggestionであり、hidden authorityにしない。

### required-locale gapをrelease前にblockする

CI/release buildでrequired dialogue/UI entry欠損、placeholder mismatch、invalid markup、
font/glyph欠損、required localized asset欠損をfailさせる。Translation lock前にpseudo-localeと
代表long stringを実行する。

**根拠**: Missing textとclipped choiceはwarningではなくshipping defectである。
**影響**: Development buildでは許可fallback textを可視markerで示す。Productionで日本語へ
fallbackできるのはrelease locale matrixがそのsurfaceをoptionalと明示した場合だけとし、
それ以外はbuildを拒否する。

### language contentをversioned content packとして扱う

Voice、movie、texture、font、大きなtranslation unitはADR-0006に従うlocale packとして
配信できる。Locale metadataとrecovery textはlocalに保持する。

**根拠**: Full voice/mediaはinstall sizeの大部分を占め得る。
**影響**: Required pack/font coverageが利用可能になるまでlocaleを選択不可にする。Pack
compatibilityをcontent manifestへ記録しplayer buildに対してvalidateする。

## 利点

- narrative/UI teamが各contentに適したtoolingを使える。
- Stable ownershipによりduplicate translationとdriftを防げる。
- route logicを編集せずprofessional localizationを進められる。
- Language packでmandatory install sizeを削減できる。
- fallback、glyph、layout defectがrelease gate対象になる。

## トレードオフ

- 2つのlocalization systemを同時変更する必要がある。
  → 1つのproduct localization serviceが同一transactionで調停する。
- Translatorがnarrative/UIの複数exportを扱う。
  → stable key、ownership、context、statusを含む1つのrelease manifestをexportする。
- 日本語source fallbackが許されないmarketがある。
  → fallback禁止marketはrequired-locale matrixでreleaseをblockする。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| 全textをscenario CSVへ置く | menu、legal text、platform prompt、localized assetはnarrative topologyに属さない。 |
| 全dialogueをUnity String Tableへ置く | Talk Systemのrow-aware preview、translation validation、narrative import/exportを失う。 |
| 同じkeyをどちらのsystemからもoverride可能にする | authorityが曖昧になりlocale依存driftを生む。 |
| content lock後にlocalizationを始める | expansion、glyph、grammar、voice-pack問題の発見が遅すぎる。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — Talk Systemをnarrative runtimeにする
- [ADR-0006](0006-addressable-content-delivery.ja.md) — locale asset/pack配信
- [ADR-0009](0009-deterministic-presentation-runtime.ja.md) — subtitle/voice/presentation変更

## 開発ルール連携

- Talk SystemとUnity tableを跨ぐkey-ownership manifestを生成・検証する。
- 全production scenario profileでTalk System localization validationを実行する。
- 代表screenでpseudo-locale、glyph coverage、text expansion、subtitle timing、
  controller-navigation checkを行う。
- Translator contextを必須にしpublish済みdialogue IDのrenumberを禁止する。

## 注記

- UnityのString/Asset Table、pseudo-localization、import/exportは
  [Unity 6.3 Localization package](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.localization.html)を参照する。
- 初期support locale listはproduct判断であり本ADRでは固定しない。
- narrative validationまたはUI/asset localizationを失わず1systemで両workflowを置き換えられると
  実証された場合だけ分離を再検討する。
