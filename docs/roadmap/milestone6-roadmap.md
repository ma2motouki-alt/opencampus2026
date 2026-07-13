# Milestone 6: Interaction Tuning Pass

## Goal

展示として気持ちよく見えるように、反応距離、速度、雲位置、雨、植物、粒の動きを調整する。

## Target Scope

- `Master/MasterDatabase.cs`
- `Master/*Masters.cs`
- `Master/TuningParameterMaster.cs`
- `WorldSpaceMaskAnimationController.cs`
- Python RealSense `config.py`
- Unity inspector values

## Current Tuning Focus

- RealSense contour noise reduction.
- Cloud contact radius and movement band.
- Particle-to-cloud rain trigger radius.
- Rain duration and visual size.
- Plant growth speed and flower attraction.
- Hand contour reaction padding.

## Acceptance Check

- 手をかざすと見た目と反応が一致して見える。
- 小さいノイズがUnity上で過剰に表示されない。
- 雲が勝手に雨を降らせすぎない。
- 粒を雲へ動かした時は雨が降る。
- 雨から植物が自然に育つ。
- 粒が植物や花へ分かりやすく向かう。

## Handoff Notes

調整値を変更したら `parameter-manual.md` も更新する。
