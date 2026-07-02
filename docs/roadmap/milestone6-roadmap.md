# Milestone 6: Interaction Tuning Pass

## Goal

展示として気持ちよく見えるように、小人の速度、反応距離、落下軌道、オブジェクトサイズを調整する。

## Target Scope

- `WorldPresetMaster`
- `LittlePersonArchetypeMaster`
- `BehaviorProfileMaster`
- `InteractionObjectTypeMaster`
- `WalkableSurfaceMaster`
- `TuningParameterMaster`
- debug 表示

対象外:

- 新機能追加
- RealSense 入力
- 本番素材制作

## Work Items

- 小人の数、速度、サイズ、色を調整する。
- 画面縁歩きの速度と折り返し頻度を調整する。
- 棒への乗りやすさ、歩行速度、落下クールダウンを調整する。
- 落下軌道を自然に見える範囲に調整する。
- オブジェクトサイズとリサイズ範囲を展示向けに調整する。

## Acceptance Check

- 何も置かなくても小人の生活感がある
- 棒を置いたとき、近くの小人が自然に気づいて乗る
- 棒に乗る頻度が高すぎず低すぎない
- 落下が真下ではなく自然な弧に見える
- 複数オブジェクトを置いても破綻しない

## Handoff Notes

この milestone は数値調整中心にする。構造変更が必要になった場合は、別 milestone として切り出す。
