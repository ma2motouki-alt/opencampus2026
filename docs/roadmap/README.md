# Little People World Roadmap

## Goal

水平に置いた長方形ディスプレイ上で、小人たちが自律的に暮らし、手や物体、雲、星に反応するインタラクティブ展示作品を完成させる。

完成時の体験は、スコアやミッションではなく、小人が物体の外周を歩き、物体に乗り、落下し、雲や星に触れて環境演出を起こす様子を観察・発見することを中心にする。

## Target Scope

対象に含めるもの:

- Unity 2D による小人ワールド
- 画面縁を生活圏とする小人の移動
- マウス入力による手・丸・棒プロップの仮配置
- 棒プロップ外周への乗り移り、外周歩行、落下
- 近接した棒同士の surface-to-surface 直接乗り移り
- 雲・星の自然発生と接触リアクション
- master data と domain object の分離
- UseCase を経由した world 作成、入力適用、フレーム進行
- 将来の RealSense / UDP 入力へ差し替え可能な入力境界
- 本番素材・アニメーション差し替えを想定した表示境界

対象外または後回し:

- RealSense 認識の高精度化
- 商用品質のキャラクター絵素材
- 完全な物理シミュレーション
- スコア、勝敗、ステージ制
- SQLite / MasterMemory などの外部マスタ基盤
- 複雑な経路探索

## Iteration Policy

このロードマップは、milestone ごとの反復で完成度を上げるための作業単位である。

- 各 milestone は単独で実装依頼できる粒度にする。
- 各 milestone は Unity 上で動作確認できる acceptance check を持つ。
- 実装後は必要に応じて design docs と master data docs を更新する。
- 次の milestone に進む前に、前 milestone の確認項目を満たす。
- 実装で新しい判断が必要になった場合は、該当 milestone ファイルを更新してから作業する。

## Current State

実装済み:

- `CreateWorldUseCase`
- `ApplyInteractionObjectsUseCase`
- `AdvanceWorldUseCase`
- `LittlePeopleWorldOrchestrator`
- Mouse input provider
- EdgeWalk
- BarProp 由来の実物長辺 `WalkableSurface`
- Surface transfer / surface walk / ride / falling
- Surface-to-surface transfer
- 傾いた棒の片側乗り制限
- `BarVisualScale = 4.32` による表示棒と domain geometry の同期
- 丸 cap のない矩形 `PropObstacle`
- Ambient cloud / star
- RainColumn / StarBurst procedural visual effect
- Debug display

最新同期済み:

- `docs/design/domain-objects.md` は `WalkableSurface` と `PropObstacle` の最新モデルを反映済み
- `docs/design/master-data.md` は `WalkableSurfaceMaster` の `BarVisualScale`, `ExitProgressInset`, surface connection, `BarObstaclePadding` の意味を反映済み
- `docs/design/system-design.md` と `docs/design/input-protocol.md` は derived runtime data と入力境界を反映済み

## Milestones

| Milestone | File | Goal |
|---|---|---|
| 1 | [milestone1-roadmap.md](milestone1-roadmap.md) | roadmap / design / master data / UseCase の責務を揃える |
| 2 | [milestone2-roadmap.md](milestone2-roadmap.md) | 棒を端乗り・端降り・傾き別片側歩行に更新する |
| 3 | [milestone3-roadmap.md](milestone3-roadmap.md) | UseCase 境界を整理してテストしやすくする |
| 4 | [milestone4-roadmap.md](milestone4-roadmap.md) | 雲・星の環境リアクションを展示体験として完成させる |
| 5 | [milestone5-roadmap.md](milestone5-roadmap.md) | 小人と演出を本番素材へ差し替え可能にする |
| 6 | [milestone6-roadmap.md](milestone6-roadmap.md) | 展示として気持ちよく見えるように調整する |
| 7 | [milestone7-roadmap.md](milestone7-roadmap.md) | Unity 側の UDP 入力境界を作る |
| 8 | [milestone8-roadmap.md](milestone8-roadmap.md) | RealSense D435 + Python 認識プロトタイプを作る |
| 9 | [milestone9-roadmap.md](milestone9-roadmap.md) | 展示環境で安定して動かせる状態にする |
| Pending | [milestone-pending.md](milestone-pending.md) | 未確定・後続候補を一時的に置く |

## Final Acceptance

完成とみなす条件:

- 小人が通常時は画面縁を歩く
- 棒を置くと、小人が縁側端から外周に乗る
- 小人が棒の外周を歩き、近接した棒があれば直接乗り移り、なければ中心側端から落下する
- 棒の傾きにより、歩行可能な側が自然に制限される
- 雲に触れると雨が降る
- 星に触れると光が弾ける
- 入力元をマウスから RealSense / UDP に差し替えられる
- 見た目の素材やアニメーションを後から差し替えられる
- スコアや説明UIなしでも、触ると世界が変わることが伝わる
