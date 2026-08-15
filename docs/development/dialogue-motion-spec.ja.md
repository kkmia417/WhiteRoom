# 会話presentation motion仕様

ステータス: [Issue #71](https://github.com/kkmia417/WhiteRoom/issues/71) として実装、[Issue #73](https://github.com/kkmia417/WhiteRoom/issues/73) と [Issue #75](https://github.com/kkmia417/WhiteRoom/issues/75) で拡張<br>
English canonical file: [英語正本](dialogue-motion-spec.md)

## 成果とownership

WhiteRoomはTalk System既存の会話、stage、choice動作の上に、読みやすさを崩さないcinematic motionを
追加する。`NovelDialogueMotionController`が製品固有の一時演出を所有し、行送り、choice、stage state、
save data、restoreのauthorityはTalk Systemに残す。

`DialogueMotionFactory`は本番`DialogueView` prefabとruntime fallback viewの両方へ同じcontrollerを
追加する。scenario column、package public API、save field、route ruleは追加しない。

## Motion契約

- 各行の開始時に0.22秒のunscaled easingでwindowを短くslide/fadeし、nameplateを軽く強調する。
  inputとtypewriter進行は待たせない
- 発話中の立ち絵はfull color、scale 1.025、上方向8 pixelとする。表示中の聞き手はscale 0.985と
  寒色系dim tintにし、地の文では全立ち絵をneutral tintと基準transformへ戻す
- 発話slotは`Speaker`とparse済み`Characters` directiveから決める。レイ、ナギ、研究員は正規stage key、
  未作画話者は最初の表示対象placeholderを使う。左右のplaceholderは別identityのため同時表示とfocusが可能
- active choiceは0.20秒、0.055秒間隔のstaggerで表示する。pointer hover、keyboard/controller select、
  pressは同じscale feedbackを使う
- 解決済み背景は安全scale 1.08と範囲を制限した低速unscaled driftを使う。対象16:9画面で端を露出せず、
  ultrawideの意図的letterboxはstage viewの既存規則を維持する
- 背景または章キューでは、stageの上・会話UIの下に入力を妨げないveilを表示する。章fadeは0.72〜1.40秒、
  通常fadeは指定時間を0.25〜1.40秒に収め、cutは0.16秒、章境界のcutは0.48秒とする。夜・屋外は濃紺、
  白い部屋は淡い氷青、警報は抑えた深紅pulseにする。veilはpointerやnavigation inputを遮らない
- `ChapterKey`を持つ行は、先頭の章番号（例: `第一章`）と残りの章題を分離し、safe area右上の
  `NovelChapterTitleView`へ表示する。その行では通常window画像、話者名、本文を隠して章題の重複を防ぐ。
  既存のNext、typewriter完了、Auto、Skip、keyboard、controller経路は維持する。0.10秒待ってから右方向より
  0.48秒で表示し、0.18秒で退出する。accent色はstage transitionと同じ寒色、無菌、警報、neutral moodに従う。
  overlay自体はraycastを受け取らない
- Next表示中だけ、小さな非blocking pulseを行う
- 任意のcustom CSV列`ScreenEffect`は、合成可能なsemantic cueとして`shake_soft`、`shake_impact`、
  `flash_white`、`flash_alarm`、`zoom_in`を解決する。未知tokenはdiagnosticを出さず無視し、認識できる
  tokenがない行は従来のpresentationを維持する。複数tokenは`|`で区切る
- 揺れとzoomはstage rootだけへ作用し、会話window、話者名、本文、選択肢、Nextを固定する。揺れは
  deterministicな複数周波数の波形へattackとcubic dampingを適用し、softは6 px・0.32秒、impactは
  18 px・0.42秒を上限とする。zoomは1.035倍・0.50秒を上限とし、短いovershoot後に基準値へ完全復帰する
- 白flashはalpha 0.72・0.26秒、警報flashはalpha 0.48・0.34秒を上限とする。どちらも一度の急な
  attackと一度のdecayだけで連続点滅させず、overscanしたoverlayはraycastを受け取らない

## Cancelとrestore

新しい行ごとにgeneration tokenを更新し、前の行のcoroutineを停止する。会話終了、view disable、destroy、
load完了時はwindow、nameplate、choice、stage transform、screen-effect overlay、背景、transition veil、
章題、立ち絵transformを基準値へ戻す。
load後はdurable stage cueやtransitionを再生せず、現在行の最終focus・章題状態だけを適用する。

motion完了は会話を進めず、save stateにも書き込まない。全時間計算に`Time.unscaledDeltaTime`を使い、
AutoやSkipで停止中のtweenを残さない。

## Validation

- `NovelDialogueMotionControllerTests`はactive slot、transition mood、章題分離、typed screen-effect解決と安全上限、本番/fallback factory配線、
  重複追加防止、非blocking overlay設定、safe area右上anchor、pointer/controller choice feedbackを検証する
- `WhiteRoomPlayModeStartupSmokeTests`はレイから少女へのfocus切替、地の文のneutral復帰、placeholder 2体、
  choice reveal完了、章題表示時の通常window抑制と復帰、screen-effect再生とcancel、unexpected log 0件を検証する
- 実画面captureはレイfocus、少女focus、placeholder 2体、choice、寒色の章切替、警報章切替、
  衝撃flash/揺れ、警報flash、短いstage zoomを対象にする

本実装はADR-0009に従う。将来Timeline、Live2D、Spine、Cinemachine、shader、post-processingを追加する場合は
別Issueとし、同じcancel/restore契約を維持する。
