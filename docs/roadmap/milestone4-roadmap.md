# Milestone 4: Ambient Reaction Completion

## Goal

雲・星・雨を展示の発見要素として完成させる。

## Target Scope

- `AmbientObject`
- `VisualEffectInstance`
- `VisualEffectView`
- `WorldSpaceMaskAnimationController`
- Cloud / star master values

## Current Implementation

- Clouds and stars spawn as ambient world objects.
- Cloud touch by little people triggers `RainColumn`.
- Cloud touch by particles also triggers rain.
- Cloud movement is constrained by `MovementEdgePadding` and `MaxCenterY`.
- Rain can create and grow plants.
- Stars trigger `StarBurst` with cooldown.

## Acceptance Check

- 雲に小人が触れると雨が降る。
- 小さい粒が雲に触れても雨が降る。
- 雲が通常の小人通路を横切りすぎず、勝手に雨が降りにくい。
- 雨が植物生成につながる。
- 星に触れると光が弾ける。
- 雲・星が手や棒の入力と干渉しすぎない。

## Handoff Notes

Cloud tuning is split across `AmbientObjectTypeMaster`, `TuningParameterMaster`, and `WorldSpaceMaskAnimationController`.
