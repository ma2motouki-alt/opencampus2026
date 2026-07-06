# Milestone 2: Bar Surface Rule Refinement

## Goal

棒プロップの挙動を「中心線や仮想レーンを歩く」モデルから、「濃い水色の実物四角形の長辺を歩く」モデルへ修正する。

小人は画面縁から棒の縁側端付近へ乗り移り、実物矩形の長辺に沿って中心側先端まで歩く。先端付近に別の棒の歩行可能 surface が近接している場合は、地面へ落下せず、そのまま次の物体へ乗り移る。近接した接続先がない場合のみ、その先端角付近から落下する。`WalkableSurface` は引き続き domain object として使うが、表示上の別足場や中心線ではなく、`BarProp` の実物矩形から生成される長辺そのものとして扱う。

## Target Scope

- `BarProp` 由来の `WalkableSurface`
- `LittlePerson` の `TransferToSurface`, `SurfaceWalk`, `RideSurface`, `Falling`
- `WalkableSurfaceMaster`
- `BarProp` 由来の通行ブロック判定
- 棒先端から別の棒 surface への直接乗り移り
- debug 表示上の実物辺確認線
- `domain-objects.md`, `master-data.md`, `experience.md` の仕様同期

対象外:

- 丸プロップやブロックの外周歩行
- RealSense 輪郭からの surface 生成
- 本番キャラクター素材
- 物体発光表現
- 棒先端を回り込んで反対側へ降りる演出

## Required Features

### 実物四角形の辺から WalkableSurface を生成する

- `WalkableSurface` は「薄い水色の別道」でも「棒の中心線」でもなく、「実物四角形の長辺」を表す。
- `axis = (cos(angle), sin(angle))` とする。
- `normal = (-axis.y, axis.x)` とする。
- `halfLength = size.x * 0.5f` とする。
- `halfHeight = size.y * 0.5f` とする。
- Unity の描画は横長のワールド座標で回転するため、surface 生成も Game View の `displayAspect = worldWidth / worldHeight` を使って実物矩形の四隅を逆算する。
- Unity の棒表示は `BarVisualScale` ぶん拡大されるため、surface 生成には `visualSize = size * BarVisualScale` を使う。現状の見た目は `InteractionObjectView` の `1.08` 倍表示と、`RuntimeSpriteFactory.Square` の sprite bounds `4` Unity units の積で決まる。
- 長さ方向の正規化座標 offset は `axisOffset = (cos(angle) * visualSize.x * 0.5, sin(angle) * visualSize.x * displayAspect * 0.5)` とする。
- 幅方向の正規化座標 offset は `sideOffset = (sin(angle) * visualSize.y * 0.5 / displayAspect, -cos(angle) * visualSize.y * 0.5)` とする。
- 棒中心線の両端を求める。
  - `firstEnd = position - axisOffset`
  - `secondEnd = position + axisOffset`
- 画面中心から遠い中心線端を `farCenter`、近い中心線端を `nearCenter` とする。
- 実物矩形の長辺は以下の2本とする。
  - `farCenter + sideOffset` -> `nearCenter + sideOffset`
  - `farCenter - sideOffset` -> `nearCenter - sideOffset`
- `WalkableSurface.Start` は縁側、画面中心から遠い側の実物角にする。
- `WalkableSurface.End` は中心側、画面中心に近い側の実物角にする。
- `PathEndPoint` は `End` と同じ、または `ExitProgressInset` だけ手前にした実物辺上の点にする。
- `ExitPoint` は Milestone 2 では `PathEndPoint` と同じ意味にする。
- `PhysicalTipPoint` は歩いている長辺の中心側先端角として保持する。

### 小人は実物長辺に沿って歩く

- `SurfaceWalk` は `AttachProgress -> ExitProgress` の一方向移動にする。
- `PositionAt(progress)` は実物矩形の長辺上の点を返す。
- 小人は濃い水色の四角形の長辺に沿って上るように見える。
- `AttachPoint` は縁側端に近い位置とし、`AttachProgressInset` は小さく保つ。
- `ExitProgressInset` は `0.0f` を基本にし、棒の途中で落ちないようにする。
- `SurfaceExitDwellSeconds` は、先端に到達したことを一瞬見せてから落下するために維持する。
- `MinWalkDistance` / `MaxWalkDistance` による途中離脱は Milestone 2 では使わない。

### 先端到達後の分岐

- 小人は `PathEndPoint`、つまり歩いている実物長辺の中心側先端まで歩く。
- `surfaceProgress >= ExitProgress` になったら、`Position = surface.PathEndPoint` に固定する。
- `SurfaceExitDwellSeconds` の短い滞在後、まず近接する別 surface へ乗り移れるかを判定する。
- 乗り移れる別 surface がある場合は `Falling` に入らず、`TransferToSurface` と同じ遷移演出で次の surface へ移動する。
- 乗り移れる別 surface がない場合のみ、`surface.PathEndPoint` から `Falling` に入る。
- `PathEndPoint -> ExitPoint` の回り込み補間は呼ばない。
- 棒削除、surface 消失、急移動などの異常離脱では現在位置から落下する。

### Surface-to-Surface 接続

- 棒同士が近い場合、`WalkableSurface` 同士の directional connection を生成または判定する。
- 接続元は、現在歩いている surface の `PathEndPoint` とする。
- 接続先は、別 surface 上で接続元 `PathEndPoint` に最も近い点を基本とする。
- 接続先の候補は `sourceObjectId` が異なる surface に限定する。同じ棒の反対側 surface へ即座に移る挙動は Milestone 2 では扱わない。
- 接続距離は `SurfaceConnectionDistance` 以下とする。初期値は `0.08f` 程度から調整する。
- 接続先 surface は `InteractionObjectState.Placed` のときだけ新規乗り移り可能にする。ドラッグ中の棒へは乗り移らない。
- 接続先 surface への侵入方向も、最近点と `WalkableNormal` を使って判定し、物体の裏側から貫通して乗るような接続は除外する。
- 複数候補がある場合は、`PathEndPoint` から target surface 最近点までの距離が最も短い surface を選ぶ。
- 距離が同程度の場合は、現在 surface の進行方向と、接続先最近点への方向が近い候補を優先する。
- 接続が成立したら、`transferStart = current.PathEndPoint`、`transferEnd = targetClosestPoint`、`surfaceProgress = targetClosestProgress` として、次の surface の `SurfaceWalk` へつなぐ。
- 乗り移り中に接続先 surface が削除された、急移動した、または `DetachDistance` を超えて離れた場合は、現在位置から `Falling` に入る。
- 接続完了後は、直前の surface へ即座に戻らないよう `SurfaceConnectionCooldownSeconds` を設ける。
- 連続した棒が階段や橋のように近接している場合、小人は `SurfaceWalk -> SurfaceToSurfaceTransfer -> SurfaceWalk` を繰り返せる。
- 接続先がない場合は従来どおり落下するため、単独の棒の挙動は壊さない。

### 片側乗り制限

- 棒の角度を `0..180` 度へ正規化する。
- `90度 ± TwoSidedVerticalToleranceDegrees` の範囲なら、両側の長辺を `WalkableSurface` として生成する。
- それ以外の傾きでは、正規化座標で画面上側に見える長辺だけを歩行可能にする。
- 生成しない長辺は debug 表示にも出さず、乗り移り候補にもならない。
- `TryStartSurfaceTransfer` は `AttachPoint` との距離に加えて、`WalkableNormal` 側から近づいているかを判定する。
- dot が `-AttachSideTolerance` より小さい場合は、物体の反対側にいるため乗り移り不可とする。

### 棒の実体による通行ブロック

- `PropObstacle` は維持する。
- 小人が乗れない側から棒に当たった場合、棒を貫通して縁歩きを続けない。
- `EdgeWalk` 中に次位置が棒の rectangular obstacle と交差する場合、先に `TryStartSurfaceTransfer` を試す。
- 乗り移れない場合は `edgeDirection` を反転し、`EdgeBlockBackoffDistance` だけ手前へ戻す。
- `EdgeBlockCooldownSeconds` により、同じ棒で細かく震え続けることを防ぐ。

### Debug 表示

- Debug ON 時の surface 線は、濃い水色の実物四角形の長辺に重なる。
- 黄色点は歩いている長辺の中心側先端角付近に出る。
- 棒から離れた薄い水色の道は表示しない。
- 中心線を歩いているように見える debug 表示を出さない。

## Master Data

`WalkableSurfaceMaster` で扱う値:

- `AttachDistance`
- `DetachDistance`
- `SurfaceWalkSpeed`
- `RideVelocityLimit`
- `SurfaceWidth`: debug 線の太さ、または内部判定幅として扱う。実物辺の位置決定には使わない。
- `BarVisualScale = 4.32f`: Unity 側の棒表示サイズに合わせる。現在の棒は `InteractionObjectView` の `1.08` 倍表示に加えて、`RuntimeSpriteFactory.Square` の sprite bounds が `4` Unity units あるため、domain 側の実物矩形計算では `1.08 * 4 = 4.32` を使う。緑線と黄色点を見えている水色矩形の辺・先端へ合わせるための値。
- `TransferDurationSeconds`
- `AttachProgressInset = 0.03f`
- `ExitProgressInset = 0.0f`
- `SurfaceExitDwellSeconds = 0.2f`
- `SurfaceConnectionDistance = 0.08f`: 現在 surface の `PathEndPoint` から、別 surface 上の最近点へ直接乗り移れる最大距離。
- `SurfaceConnectionTransferDurationSeconds = 0.16f`: surface 間を直接移動するときの遷移時間。最初は `TransferDurationSeconds` より少し短くして、地面へ降りない連続感を出す。
- `SurfaceConnectionCooldownSeconds = 0.25f`: 接続完了後、直前の surface へ即座に戻ることを防ぐ cooldown。
- `TwoSidedVerticalToleranceDegrees = 15f`
- `AttachSideTolerance = 0.01f`
- `BarObstaclePadding = 0.01f`
- `EdgeBlockBackoffDistance = 0.015f`
- `EdgeBlockCooldownSeconds = 0.25f`

Milestone 2 では未使用化する値:

- `TipCrossDurationSeconds`
- `ExitOppositeSidePadding`
- `MinWalkDistance`
- `MaxWalkDistance`

## Work Items

### Surface 生成

- `WalkableSurface.AddFromInteractionObject` で、`axisOffset` と `sideOffset` から実物矩形の長辺2本を計算する。
- `Start` / `End` は中心線ではなく、実物矩形の長辺上の角にする。
- `SurfaceWidth` や lane offset による外側オフセットは行わない。
- `ExitPoint` は `PathEndPoint` と同じ点として扱う。
- 垂直判定と片側判定により、生成する長辺 surface 数を決める。
- `WalkableNormal` は乗れる側の判定に使うため保持する。

### 乗り移り

- `TryStartSurfaceTransfer` の候補距離は `surface.AttachPoint` から計算する。
- `surface.CanAttachFrom(person.Position, AttachSideTolerance)` により、反対側からの乗り移りを除外する。
- 選択した surface の `AttachProgress` を `surfaceProgress` に入れる。
- `transferEnd` は実物長辺上の `surface.AttachPoint` にする。

### 移動と落下

- `AdvanceSurfaceMotion` は `SurfaceWalkSpeed` で `surfaceProgress` を増やす。
- `surfaceProgress` が `ExitProgress` に到達したら、`Position = surface.PathEndPoint` に固定する。
- `SurfaceExitDwellSeconds` 経過後、まず `TryStartSurfaceToSurfaceTransfer` を試す。
- 近接した別 surface がある場合は、`surface.PathEndPoint` から target surface 上の最近点へ直接遷移する。
- 近接した別 surface がない場合だけ、`surface.PathEndPoint` から落下する。
- `PathEndPoint -> ExitPoint` の回り込み補間は呼ばない。
- drag 中は progress を進めず `RideSurface` として運ばれる。
- fast move / delete / surface lost / detach distance exceeded は現在位置から落下する。

### Surface-to-Surface 接続

- 必要なら `SurfaceConnection` domain object を追加する。
  - `sourceSurfaceId`
  - `targetSurfaceId`
  - `sourceObjectId`
  - `targetObjectId`
  - `sourcePoint`
  - `targetPoint`
  - `distance`
  - `isValid`
- `World.SetInteractionObjects` で `WalkableSurface` を生成した後、surface 間の接続候補を再生成する。
- 最初の実装では domain object を追加せず、`LittlePerson` が `surfaces` から候補を直接探索してもよい。ただし debug 表示やテストを考えると、後で `SurfaceConnection[]` として分離できる形にする。
- `TryStartSurfaceToSurfaceTransfer` を `LittlePerson` に追加する。
- `TryStartSurfaceToSurfaceTransfer` は、現在 surface の `PathEndPoint` から候補 surface 上の最近点までの距離、接続先 state、接続先 side 判定、cooldown を確認する。
- 接続成立時は `TransferToSurface` の状態を再利用し、`transferStart` と `transferEnd` を surface 間の点に設定する。
- 接続先の `surfaceProgress` は `targetClosestProgress` にする。
- 接続候補がない場合は既存の `StartFalling(tuning, surface.PathEndPoint)` に進む。
- 直前に来た surface / source object へ戻るループを避けるため、接続完了時に `surfaceConnectionCooldownSourceObjectId` または `surfaceConnectionCooldownSurfaceId` を保持する。

### Debug / Docs

- Debug ON 時の surface 表示は、実物矩形の長辺に重なる細い線と endpoint だけにする。
- Debug ON 時、surface-to-surface 接続候補がある場合は、`PathEndPoint` から target surface 最近点へ細い補助線を表示できるようにする。
- 接続候補線は実際の足場ではなく、乗り移り可能判定の可視化として扱う。
- Debug 表示で別の足場や中心線が存在するように見せない。
- `domain-objects.md`, `master-data.md`, `experience.md` を実物四角形の辺モデルと surface-to-surface 接続モデルに更新する。

## Acceptance Check

- Debug ON で、歩行線が濃い水色の四角形の長辺と一致する。
- 小人が棒の中心線ではなく、濃い水色の四角形の長辺に沿って上る。
- 小人が実物矩形の中心側先端、つまり角付近まで到達する。
- 黄色点が実物四角形の先端角付近に表示される。
- 単独の棒では、小人が黄色点から落下する。
- 黄色点付近に別の棒の長辺が近い場合、小人は地面に降りず次の棒へ直接乗り移る。
- 直接乗り移った小人は、接続先の棒の最近点から `ExitProgress` 方向に歩き始める。
- 接続先がドラッグ中の場合、小人はその棒へ直接乗り移らず、従来どおり落下または現在位置から離脱する。
- 乗り移り中に接続先の棒が削除された場合、小人は現在位置から落下する。
- 複数の接続候補がある場合、一番近い surface 最近点を持つ棒へ移動する。
- 2本の棒が近い場合、小人が `棒Aを歩く -> 棒Bへ直接移動 -> 棒Bを歩く` という流れになる。
- 3本以上の棒を近接配置した場合、小人が地面に戻らず連続して渡れる。
- 直前に来た棒へ即座に戻る往復ループが発生しない。
- `ExitProgressInset` を上げると、落下開始位置が辺の先端から少し手前に移る。
- 傾いた棒では片側の長辺だけに乗れる。
- 乗れない側から来た小人は棒を貫通せず反対方向へ戻る。
- 垂直に近い棒では両側の長辺から乗れる。
- 棒をゆっくりドラッグすると小人は運ばれる。
- 棒を速く動かす、削除する、surface が消える場合は現在位置から落下する。
- `1/2/3`, 削除、ドラッグ、リサイズ、回転、雲/星リアクションが壊れない。
- Unity 6000.4.10f1 相当の C# コンパイルが通る。

## Assumptions

- 「濃い水色の四角形の辺」は、`InteractionObjectView` が描画している `BarProp` の実物矩形の長辺を指す。
- 小人は棒の中心線ではなく、実物矩形の長辺上を歩く。
- 落下開始位置は、歩いている長辺の中心側先端を基本にする。
- ただし先端付近に別の棒の長辺がある場合は、落下ではなく surface-to-surface transfer を優先する。
- Milestone 2 の surface-to-surface transfer は `BarProp` 同士に限定する。
- 接続先は別 surface 上の最近点とし、棒同士が端で揃っていなくても近い辺へ飛び乗れるようにする。
- 同じ棒の反対側 surface へ先端から回り込む挙動は採用しない。
- 手動調整はまず `ExitProgressInset` で行う。
- Milestone 2 では `BarProp` のみ対象にし、丸・ブロック・RealSense 輪郭の外周歩行は後続 milestone に残す。
