# Milestone 7: UDP Input Boundary

## Goal

RealSense 連携前に、Unity 側の入力差し替え口を完成させる。

## Target Scope

- `IInteractionInputProvider`
- Mouse input provider
- 新規 `UdpRealSenseInputProvider`
- `SensorFrame`
- UDP JSON parser

対象外:

- Python 側の RealSense 認識
- 深度処理
- 本番キャリブレーション

## Work Items

- `IInteractionInputProvider` を維持する。
- `UdpRealSenseInputProvider` を追加する。
- UDP JSON を `SensorFrame` / `InteractionObject[]` に変換する。
- Mouse provider と UDP provider を Unity Inspector または bootstrap で切り替えられるようにする。
- 不正 JSON、未受信、古い frame を受けても Unity が止まらないようにする。

## Acceptance Check

- Mouse provider で今まで通り動く
- UDP provider に切り替えても `World`, `LittlePerson`, `WalkableSurface` を変更しない
- JSON の座標は `0.0..1.0` の左上原点として扱われる
- 不正な JSON や一時的な未受信でも Unity が止まらない

## Handoff Notes

この milestone は Unity 側だけを対象にする。Python / RealSense 側は Milestone 8 で扱う。
