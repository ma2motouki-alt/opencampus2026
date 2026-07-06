# Milestone 5: Visual Replacement Layer

## Goal

仮の丸い小人や procedural effect を、将来の絵素材・アニメーションへ差し替え可能にする。

## Target Scope

- `LittlePersonView`
- `InteractionObjectView`
- `AmbientObjectView`
- `VisualEffectView`
- `VisualEffectMaster`

対象外:

- 実際の本番アニメーション素材制作
- FMOD 導入
- UI 追加

## Work Items

- `LittlePersonView` を状態別表示に整理する。
- 状態名を `EdgeWalk`, `TransferToSurface`, `SurfaceWalk`, `RideSurface`, `Falling`, `Calm`, `Curious`, `Startled` として扱えるようにする。
- 将来 Animator / SpriteAnimation に差し替えるための状態名を design docs に定義する。
- `VisualEffectView` を `VisualEffectMaster.RenderMode` に沿って分岐できる構造にする。
- MVP の procedural 表示は維持する。

## Acceptance Check

- 小人の状態に応じて見た目が変わる
- 本番アニメーション素材を追加するとき、domain object を変更せずに済む
- 雨・星バーストを prefab 化する道筋がある
- procedural 表示だけでも MVP として動作する

## Handoff Notes

素材差し替え境界を作る milestone であり、見た目を豪華にする milestone ではない。domain には描画都合を入れない。
