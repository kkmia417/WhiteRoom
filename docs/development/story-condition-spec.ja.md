# R00 story condition仕様

ステータス: Proposed — CSV実装前にstory ownerの承認が必要<br>
関連: [Issue #22](https://github.com/kkmia417/WhiteRoom/issues/22)<br>
English canonical file: [English](story-condition-spec.md)

## 現状棚卸し

`r00_escape_talksystem.csv`はdialogue 199行、choice node 20件、uniqueな
`EventKey` 68件、unique ending 14件を持ち、`ConditionKey`は0件である。既存eventは
4つの候補state familyをすでに表現している。

| Family | 既存fact | 現在のauthored consequence |
| --- | --- | --- |
| Trust | `trust_need_help`, `trust_match_speed`, `save_nagi_emotional`, `save_nagi_hold_door` | 選択したscene textとroute markerは変わるが、後続choiceをfilterしない。 |
| Name | `name_rei`, `name_id`, `name_silent`, `name_fake` | 名前sceneは変わるが、後続TRUE choiceはどの回答でも選べる。 |
| Battery | `battery_found`, `battery_nagi`, `battery_rei`, `battery_destroyed`, `battery_protected`, `battery_handed_to_nagi` | Battery routeと直後の結果は変わるが、battery ending 3件をすべて選べる。 |
| Exposure | `exposure_low`, `exposure_medium`, `exposure_high`, `exposure_fast_run`, `exposure_controlled`, `exposure_max` | High/fast/max choiceは既にauthored BAD ENDへ直接分岐し、累積exposure scoreは存在しない。 |

これらrun factの永続境界は
[ADR-0012](../adr/0012-run-scoped-story-facts.ja.md)で提案する。

## 承認対象のcondition表案

Choice entryだけをfilterする。Dialogue rowをskipしないため、利用不能choiceからending途中へ
誤って進まない。影響を受けるすべてのnodeにunconditional fallbackを残す。

| Choice row | Choice target | condition案 | Story上の意味 | Unconditional fallback |
| --- | --- | --- | --- | --- |
| 400 | 430「さっきの手を覚えている」 | `event:name_rei` | Emotional saveと後続の名前を使うTRUE pathは、レイが本名を伝えた場合だけ選べる。 | 410、420、440は常に利用可能。 |
| 654 | 710「ナギの手を自分から掴む」 | `event:save_nagi_emotional` | TRUE ENDは最後のclickだけでなく、以前のemotional decisionを必要とする。 | 700は常に利用可能で正規ENDへ進む。 |
| 855 | 860「その子のところへ行く」 | `event:battery_protected` | HIDDEN ENDはレイ自身がbatteryを守った場合だけ選べる。 | 870は常に利用可能。 |
| 855 | 880「名前を呼ばせる」 | `event:save_nagi_emotional` | TRUE END+はcondition付きtarget 430を通じて本名条件を継承し、emotional save factを必要とする。 | 870は常に利用可能。 |

Conditionは既存choice syntaxを使う。例:
`ナギの手を自分から掴む->710?event:save_nagi_emotional`。新condition grammarや
Talk System schemaは提案しない。

## 明示的な採否

- Trust: TRUE ending用のreview済みtrust factとして`save_nagi_emotional`を採用する。
  それ以前のtrust風choiceはproseへ影響するが前提条件にはしない。
- Name: emotional-save choiceの前提として`name_rei`を採用し、その結果として両TRUE endingの
  前提にする。
- Battery: hidden-child endingの前提として`battery_protected`を採用する。Batteryをナギへ
  渡した場合はregular/TRUE+ outcomeを維持する。
- Exposure: conditionを追加しない。High/fast/max decisionは既に即時のauthored consequenceを
  持ち、score/thresholdの発明は本Issue外の新story mechanicになる。

## Canonical route更新

14-ending fixtureはunique endingごとに1 routeを維持する。承認後に変更するcanonical choice
target列は次の3件だけである。

| Ending | 現在の関連target | 変更案 | 理由 |
| --- | --- | --- | --- |
| `end_true_name` | `... 60, 360, 440, 540, 620, 710` | `... 60, 360, 430, 540, 620, 710` | Condition付き710より前に`save_nagi_emotional`を成立させる。 |
| `end_hidden_underground_child` | `... 440, 580, 590, 830, 860` | `... 440, 580, 590, 820, 860` | Condition付き860より前に`battery_protected`を成立させる。 |
| `end_true_name_plus` | `... 440, 580, 590, 830, 880` | `... 430, 580, 590, 830, 880` | Review済みtrust factを成立させる。Target 430自体を`name_rei`でgateする。 |

他のcanonical routeは変更しない。Unconditional target 700/870によりcondition-negative stateも
進行できる。

## Runtime・validation契約

- New Gameはrun factをclearし、global chapter/route/ending unlockは維持する
- Save/Loadと到達boundary snapshotはrun fact set全体をcapture/replaceする。Payloadなしの
  既存saveはempty setをrestoreする
- Rollbackはrestore済みhistoryからfactを再構築し、eventを再実行しない
- Unknown condition namespaceはfail closed。`!event:<key>`は継続supportする
- Testは4つのconditional choiceのpositive/negative stateを列挙し、影響nodeにvisible choiceが
  残ること、canonical ending 14件へ到達できることを証明する
- Unity EditMode/PlayMode/batch compile、governance、Python unit gateを通す

## 承認前simulation evidence

変更していない出荷CSVへ4つのfilter案をmemory上だけで適用し、全経路をread-onlyで探索した。
Unique ending 14/14へ到達し、visible choice 0件の到達可能nodeはなく、上記3 substitutionだけを
反映したcanonical route fixture 14件もすべて成立した。この結果は表内部の一貫性を証明するが、
story-owner承認の代替にはしない。

## 承認記録

承認欄は意図的に空欄である。CSVの`ConditionKey`を変更する前に、story ownerがIssue #22
またはimplementation PRで本表を承認するか、修正要求を列挙する必要がある。承認は4つの表row、
exposure scoringの明示的な不採用、3つのroute fixture変更を受け入れることを意味する。
