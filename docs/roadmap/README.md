# Little People World Roadmap

## Goal

水平に置いた52インチTV上で、小人、粒、植物、雲、星、手や物体が反応するインタラクティブ展示作品を完成させる。

スコアやミッションではなく、手や物体をかざすと世界の形が変わり、小人や粒が反応し、雨や植物などの演出が生まれる体験を中心にする。

## Current Scope

対象に含めるもの:

- Unity 2D による展示ワールド
- 画面縁を生活圏とする小人の移動
- 虹への乗り移り、歩行、落下
- RealSense D435 + Python による depth contour input
- UDP JSON による Unity 入力
- 手や物体の contour fill 表示
- 小人の手・物体反応
- 雲・星の環境リアクション
- 小さい粒のマスク追従
- 粒が雲に触れた時の雨
- 雨から植物が育つ演出
- 粒が植物を登り、花に集まり、手で弾ける演出
- master data と domain object の分離
- UseCase を経由した world 作成、入力適用、フレーム進行

対象外または後回し:

- 完全な物理シミュレーション
- 高精度な手の3D復元
- 指一本ごとの認識
- スコア、勝敗、ステージ制
- SQLite / MasterMemory などの外部マスタ基盤
- 商用品質の素材パイプライン

## Current State

実装済み:

- `CreateWorldUseCase`
- `ApplyInteractionObjectsUseCase`
- `AdvanceWorldUseCase`
- `LittlePeopleWorldOrchestrator`
- Mouse input provider
- UDP RealSense input provider
- Python RealSense contour detection
- EdgeWalk
- Rainbow 由来の曲線 `WalkableSurface`
- Surface transfer / surface walk / falling
- Hand contour reaction
- Contour mesh fill
- Ambient cloud / star
- Particle cloud rain
- RainColumn / StarBurst
- Rain-to-plant animation
- Particle plant climbing
- Flower burst
- Debug display

## Milestones

| Milestone | File | Current Meaning |
|---|---|---|
| 1 | [milestone1-roadmap.md](milestone1-roadmap.md) | Documentation and architecture sync |
| 2 | [milestone2-roadmap.md](milestone2-roadmap.md) | Retired bar-surface prototype |
| 3 | [milestone3-roadmap.md](milestone3-roadmap.md) | UseCase and domain boundary |
| 4 | [milestone4-roadmap.md](milestone4-roadmap.md) | Cloud / star / rain reactions |
| 5 | [milestone5-roadmap.md](milestone5-roadmap.md) | Visual replacement and animation layer |
| 6 | [milestone6-roadmap.md](milestone6-roadmap.md) | Tuning pass |
| 7 | [milestone7-roadmap.md](milestone7-roadmap.md) | UDP input boundary |
| 8 | [milestone8-roadmap.md](milestone8-roadmap.md) | RealSense Python detection |
| 9 | [milestone9-roadmap.md](milestone9-roadmap.md) | Exhibition readiness |
| Pending | [milestone-pending.md](milestone-pending.md) | Future or unresolved work |

## Final Acceptance

完成とみなす条件:

- 起動後すぐ展示ワールドが動く
- RealSenseから手や物体の輪郭がUnityに届く
- 検出領域がUnity上で塗りつぶされる
- 小人が手や物体に反応する
- 棒状物体では小人が外周を歩ける
- 粒が輪郭領域、植物、雲に反応する
- 雲に触れると雨が降る
- 雨から植物が育つ
- 植物の花に粒が集まり、手で弾ける
- 10分以上動かして大きく破綻しない
- デバッグなしでも展示物として見られる
