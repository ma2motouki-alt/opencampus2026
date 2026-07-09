# Milestone 5: Visual Replacement Layer

## Goal

仮表示と手続き的な演出を、後から素材・アニメーションに差し替えやすい構造にする。

## Target Scope

- `LittlePersonView`
- `InteractionObjectView`
- `AmbientObjectView`
- `VisualEffectView`
- `WorldSpaceMaskAnimationController`
- `RuntimeSpriteFactory`

## Current Implementation

- Little people use procedural sprite-like renderers.
- Interaction contour fill uses generated mesh.
- Rain uses procedural teardrop-like sprites.
- Plants and flowers are generated at runtime.
- Flower burst and particle effects are procedural.

## Acceptance Check

- Contour fill and outline can be styled without changing domain logic.
- Rain and star effects remain behind `VisualEffectMaster`.
- Plant rendering can be tuned in `WorldSpaceMaskAnimationController`.
- Future sprite/Animator replacement can happen in views.

## Handoff Notes

Do not put asset-specific rules into `DomainModels.cs`. Keep replacement boundaries in Unity view classes.
