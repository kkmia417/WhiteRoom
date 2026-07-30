# Save thumbnail

English canonical file: [English](save-thumbnails.md)

WhiteRoomはmanual、quick、autosaveのthumbnail captureを既定で有効にします。Save payloadを
先にcommitし、thumbnailはfailure-isolated sidecarとして扱うため、画像の成否はpayloadの
load可否を決めません。

## Capture contract

- 出力はcenter cropした`320 x 180`のlossless PNG
- `512 KiB`を超えるPNGは上限なしで保存せずreject
- game stage、dialogue window、command barは画像へ含める
- Save/Load overlayと一時的なsave notificationはcapture中だけ非表示にして後で復元
- capture jobは同時に1件だけとし、完了まで別saveをrejectしてslot取り違えを防止
- interactive playerは描画frame終端でcaptureし、描画frameがないheadless環境はcapture providerを利用可能

Save payload成功後、新しい非同期captureを始める前に古いthumbnailを削除します。Capture、encode、
size validation、sidecar保存のどこかが失敗しても新しいsaveは有効なままで、UIは古い画像でなく
missing placeholderを表示します。Slot削除はpayloadとsidecarの両方を削除します。File pathは
integer slot規約から`slot_<index>.png`として生成します。

## Load UIとmemory lifecycle

Load pageはAuto、Quick、manual slotの順で表示し、Save pageはmanual slotだけを表示します。
各rowは`Image`、`Missing`、`Corrupt`、`Empty`、`Unavailable`のいずれかです。画像の欠損や
破損だけでは、load可能なsaveを無効化しません。

`NovelSaveService`はslot view modelとthumbnail byteをcacheし、overlayの再open/refreshごとの
sidecar同期再読込を避けます。表示rowは同じbyte arrayに対して1組の`Texture2D`/`Sprite`だけを
decodeして再利用します。画像置換時は古いUnity objectを解放し、controller Dispose時には残る
row画像をすべて解放します。
