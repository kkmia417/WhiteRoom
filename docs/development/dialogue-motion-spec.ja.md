# 会話presentation motion仕様

ステータス: [Issue #71](https://github.com/kkmia417/WhiteRoom/issues/71) として実装<br>
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
- Next表示中だけ、小さな非blocking pulseを行う

## Cancelとrestore

新しい行ごとにgeneration tokenを更新し、前の行のcoroutineを停止する。会話終了、view disable、destroy、
load完了時はwindow、nameplate、choice、背景、transition veil、立ち絵transformを基準値へ戻す。load後は
durable stage cueやtransitionを再生せず、現在行の最終focus状態だけを適用する。

motion完了は会話を進めず、save stateにも書き込まない。全時間計算に`Time.unscaledDeltaTime`を使い、
AutoやSkipで停止中のtweenを残さない。

## Validation

- `NovelDialogueMotionControllerTests`はactive slotとtransition mood規則、本番/fallback factory配線、
  重複追加防止、非blocking overlay設定、pointer/controller choice feedbackの同等性を検証する
- `WhiteRoomPlayModeStartupSmokeTests`はレイから少女へのfocus切替、地の文のneutral復帰、placeholder 2体、
  choice reveal完了、unexpected log 0件を検証する
- 実画面captureはレイfocus、少女focus、placeholder 2体、choice、寒色の章切替、警報章切替を対象にする

本実装はADR-0009に従う。将来Timeline、Live2D、Spine、Cinemachine、shader、post-processingを追加する場合は
別Issueとし、同じcancel/restore契約を維持する。
