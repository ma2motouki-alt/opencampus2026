# 虹から雲へのジャンプ仕様

## Goal

虹の上を歩いている小人が近くの雲を見つけると、虹から雲へジャンプする。小人が雲に触れることで雨が降り、その後、小人はジャンプ元の虹へ戻る。

この機能の目的は、虹を単なる歩行ルートではなく、雲と雨のリアクションへつながる特別な遊び場にすることである。

## Target Scope

対象に含めるもの:

- 虹を歩行中の小人による雲候補の検索
- 虹から雲への放物線状ジャンプ
- 移動中の雲への追従
- 雲への短時間の接触
- 既存の雲接触処理を利用した雨の発生
- 雲からジャンプ元の虹への帰還
- 同じ雲へ複数の小人が同時にジャンプしすぎないための予約
- デバッグ表示と強制ジャンプ機能

対象外:

- 雲の上を自由に歩く処理
- 雲から別の雲へ連続ジャンプする処理
- ジャンプ専用の新規スプライト制作
- ジャンプ中の物理エンジンによる完全な重力計算
- 妖精や植物によるジャンプ

## Experience Flow

1. 小人が虹の上を歩く。
2. 小人の進行方向付近に、ジャンプ可能距離内の雲が入る。
3. 同じ雲へ向かう小人がいなければ、小人が雲をジャンプ対象として予約する。
4. 小人が虹を蹴り、弧を描いて雲へ向かう。
5. 小人が雲の下側へ触れ、短時間その位置に留まる。
6. 既存の雲接触判定によって雨が降る。
7. 小人が雲から離れ、ジャンプ元の虹へ向かって落下する。
8. 虹へ着地後は、着地点から `SurfaceWalk` を再開する。

## Trigger Requirements

ジャンプを開始できるのは、以下をすべて満たす場合とする。

- 小人の状態が `SurfaceWalk` である。
- `ActiveSurfaceKind == Rainbow` である。
- 虹が `Active` 状態であり、消滅直前の `Fading` ではない。
- 候補が `AmbientObjectKind.Cloud` である。
- 小人から雲の接触点までの距離が `CloudJumpSearchDistance` 以下である。
- 雲が小人より極端に下側にない。
- 小人がジャンプ再実行クールダウン中ではない。
- 対象の雲がほかの小人に予約されていない。
- 初期仕様では、すでに雨を降らせている雲は対象外とする。

複数の雲が候補になる場合は、小人から接触点までの距離が最も短い雲を選ぶ。

複数の小人が同じフレームで同じ雲を候補にした場合は、雲に最も近い小人を優先する。一つの雲に対する同時ジャンプ人数は初期値 `1` とする。

## Cloud Contact Point

ジャンプ先は雲の中心ではなく、雲の下側に設定する。

正規化座標は左上原点であるため、下側は `y` が増える方向になる。

```text
contactPoint.x = cloud.Position.x
contactPoint.y = cloud.Position.y + cloud.Size.y * CloudContactOffsetRatio
```

初期値は `CloudContactOffsetRatio = 0.35` とする。最終的な接触成立判定には、既存の `AmbientObject.ContactRadius` を利用する。

## Movement States

`LittlePersonBehaviorKind` に以下を追加する。

| State | Responsibility |
|---|---|
| `JumpToCloud` | 虹上の開始位置から雲の接触点まで移動する |
| `TouchingCloud` | 雲に触れた位置へ短時間留まり、雨の発火を待つ |
| `ReturnToRainbow` | 雲からジャンプ元の虹へ向かって落下する |

状態遷移:

```text
SurfaceWalk
  -> JumpToCloud
  -> TouchingCloud
  -> ReturnToRainbow
  -> SurfaceWalk
```

例外時:

```text
JumpToCloud
  -> target cloud missing
  -> Falling
  -> EdgeWalk
```

ジャンプ開始後に虹が消えても、雲までのジャンプは継続する。ただし帰還先の虹が存在しない場合は、安全策として画面下の地面へ落下する。

## Jump Trajectory

ジャンプ軌道は、正規化座標上の二次Bezier曲線として扱う。

```text
P0 = jumpStart
P1 = jumpControl
P2 = currentCloudContactPoint
position(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
```

要件:

- `P0` はジャンプ開始時の虹上の位置で固定する。
- `P2` は雲の移動に合わせて毎フレーム更新する。
- `P1` は開始位置と最初の接触点の中間を基準に、画面上方向へ `JumpArcHeight` だけ持ち上げる。
- ジャンプ時間は距離に応じて決め、`MinJumpDurationSeconds..MaxJumpDurationSeconds` に収める。
- `t` には `SmoothStep` を使い、開始と到着を急に見せない。
- 到着判定は `t >= 1.0` または接触点までの距離が `CloudArrivalDistance` 以下になった時とする。

ジャンプ中は手入力による虹からの落下判定を受け付けない。入力と競合させず、開始したジャンプを最後まで見せる。

## Cloud Touch And Rain

雲へ到着した小人は `TouchingCloud` へ遷移する。

- 小人の位置を、移動中の雲の接触点へ追従させる。
- `CloudTouchDwellSeconds` の間だけ留まる。
- `World.UpdateAmbientReactions()` は、`Falling` ではない小人と雲の距離を既存の `ContactRadius` で判定する。
- `TouchingCloud` 中の小人は既存判定の対象になるため、`AmbientObject.MarkCloudTouched(RainLingerSeconds)` が呼ばれる。
- 雨は既存の `RainColumn`、雨音、植物成長、雨遮蔽へそのまま流す。

新しい雨エフェクトを別に作らず、既存の雲リアクションを再利用する。

## Return To Rainbow

接触時間が終了したら、雲の現在位置からジャンプ元の虹へ戻る `ReturnToRainbow` に入る。

- 帰還先は、ジャンプ元と同じ `WalkableSurface` とする。
- 帰還位置は、雲の接触点から元の虹パスへ下ろした最近点を基準にする。
- 小人が進行方向を大きく戻らないよう、帰還progressはジャンプ開始時の `surfaceProgress` 以上に制限する。
- 帰還progressは `ExitProgress` 以下に制限する。
- 雲から虹までの軌道も二次Bezier曲線で描く。
- 虹へ到達したら `surfaceId` と `surfaceProgress` を復元し、その地点から `SurfaceWalk` を再開する。
- 再び同じ雲へすぐジャンプしないよう、`CloudJumpReconnectCooldownSeconds` を適用する。
- 帰還中は手入力による落下判定を受け付けない。

帰還先の虹が消滅していた場合だけ、現在位置から画面下の地面へ `Falling` する。最寄りの上辺や横辺へ落とさない。

帰還先の計算には、`WalkableSurface` に「任意の点からpolyline上の最近点とprogressを取得する」APIが必要になる。

## Runtime Data

`LittlePerson` に追加する実行時状態:

- `targetCloudId`
- `jumpStart`
- `jumpControl`
- `jumpTimer`
- `jumpDurationSeconds`
- `cloudTouchTimer`
- `cloudJumpCooldownTimer`
- `jumpSourceSurfaceId`
- `jumpSourceObjectId`
- `jumpSourceProgress`
- `returnStart`
- `returnControl`
- `returnTargetProgress`
- `returnTimer`
- `returnDurationSeconds`

`AmbientObject` または `World` に追加する予約状態:

- 雲IDごとの予約中小人ID
- 予約の解除処理

予約は以下の時点で解除する。

- 小人が `TouchingCloud` を終了して雲から離れた時
- 対象の雲が見つからなくなった時
- 小人が例外的に `Falling` へ移った時
- world再生成時

## Domain API Changes

`LittlePerson.Advance()` が雲を参照できるよう、`IReadOnlyList<AmbientObject>` を渡す。

想定形:

```csharp
person.Advance(
    deltaTime,
    interactionFields,
    walkableSurfaces,
    ambientObjects,
    littlePeople,
    masters,
    tuning);
```

ただし、同一雲への予約競合は個々の小人に任せず、`World` が候補を集めて解決する方式を優先する。

責務:

- `World`: 候補収集、雲予約、予約解除、フレーム進行
- `LittlePerson`: ジャンプ状態、軌道、接触待機、虹への帰還、例外時の落下
- `AmbientObject`: 雲の位置、接触半径、雨リアクション
- `RainbowCloudJumpMaster`: ジャンプ条件と演出時間の不変値
- `LittlePersonView`: 状態に応じたスプライト表示と向き

## Master Data

`RainbowMaster` に詰め込まず、虹と雲をまたぐルールとして `RainbowCloudJumpMaster` を追加する。

推奨初期値:

| Parameter | Initial Value | Meaning |
|---|---:|---|
| `CloudJumpSearchDistance` | `0.16` | 小人から雲へジャンプ可能な最大距離 |
| `MaxCloudBelowOffset` | `0.03` | 雲が小人より下にあっても許容する距離 |
| `CloudContactOffsetRatio` | `0.35` | 雲中心から下側接触点までの比率 |
| `CloudArrivalDistance` | `0.025` | 雲への到着判定距離 |
| `JumpArcHeight` | `0.07` | ジャンプ軌道を画面上方向へ持ち上げる量 |
| `MinJumpDurationSeconds` | `0.35` | 最短ジャンプ時間 |
| `MaxJumpDurationSeconds` | `0.80` | 最長ジャンプ時間 |
| `CloudTouchDwellSeconds` | `0.22` | 雲に触れて留まる時間 |
| `ReturnArcHeight` | `0.025` | 雲から虹へ戻る軌道の膨らみ |
| `MinReturnDurationSeconds` | `0.30` | 虹へ戻る最短時間 |
| `MaxReturnDurationSeconds` | `0.70` | 虹へ戻る最長時間 |
| `CloudJumpReconnectCooldownSeconds` | `1.5` | 同じ連鎖を繰り返さないための待ち時間 |
| `MaxConcurrentJumpersPerCloud` | `1` | 一つの雲へ同時に飛べる人数 |
| `AllowJumpToRainingCloud` | `false` | すでに雨を降らせている雲を候補にするか |

距離は正規化座標、時間は秒で扱う。

## View Requirements

MVPでは新規画像を要求しない。

- `JumpToCloud`: 既存の見上げ画像 `*_up_left` / `*_up_right` を使用する。
- 左右画像はジャンプ開始時の雲との `x` 差で一度だけ決め、毎フレーム切り替えない。
- スプライト位置はdomainの `LittlePerson.Position` を使用する。
- 必要なら速度方向へ少しだけ回転させるが、足元補正は虹歩行時だけに適用する。
- `TouchingCloud`: 到着時の見上げ画像を維持する。
- `ReturnToRainbow`: 見上げ画像を維持し、速度方向へ必要最小限だけ傾ける。
- 帰還先の虹がない場合の `Falling`: 現在の落下表示を維持する。

小人が雲より手前に見えるよう、通常の小人sorting orderを維持する。

## Debug Requirements

`D` デバッグ表示中に以下を確認できるようにする。

- ジャンプ候補距離
- 選択された雲ID
- `P0`, `P1`, `P2` を結ぶジャンプ軌道
- 雲の接触点
- 雲ごとの予約状態
- `JumpToCloud` / `TouchingCloud` / `ReturnToRainbow` の小人数

開発用キーとして `U` を用意し、虹上を歩いている小人一人を最寄りの雲へ強制ジャンプさせる。強制実行では距離条件だけ無視し、虹歩行中・雲存在・予約上限は維持する。

## Edge Cases

- ジャンプ中に虹が消える: 雲までのジャンプを継続し、接触後は地面へ落下する。
- ジャンプ中に雲が動く: 接触点を毎フレーム更新する。
- 対象雲が消える: 現在位置から地面へ落下する。
- 虹への帰還中に虹が消える: 現在位置から地面へ落下する。
- 帰還先が虹の終端に近い: `ExitProgress` へ着地し、既存の終端処理へ進む。
- 雲が雨を降らせ始めた: 開始済みジャンプは継続する。
- 虹歩行中に手で触れられた: ジャンプ開始前なら既存どおり落下する。
- ジャンプ開始後に手で触れられた: ジャンプ中は無視する。
- 虹が `Fading` に入った: 新規ジャンプを開始しない。
- 同じ雲に複数の小人が近づいた: 最も近い一人だけが予約する。
- 雲に触れた直後に雨が遮蔽された: 雨発生と遮蔽は別責務なので既存仕様を維持する。

## Implementation Order

1. `RainbowCloudJumpMaster` とmaster tableを追加する。
2. `LittlePersonBehaviorKind` に `JumpToCloud`、`TouchingCloud`、`ReturnToRainbow` を追加する。
3. `WalkableSurface` にpolyline上の最近点とprogressを取得するAPIを追加する。
4. `World` にジャンプ候補選択と雲予約を追加する。
5. `LittlePerson` に雲へのBezierジャンプと雲追従を追加する。
6. 雲への接触待機と既存雨リアクションの接続を追加する。
7. 雲から元の虹へ戻り、`SurfaceWalk` を再開する処理を追加する。
8. 虹を失った場合だけ地面へ落ちるフォールバックを追加する。
9. `LittlePersonView` に既存見上げ画像の状態割当を追加する。
10. デバッグ軌道と `U` 強制ジャンプを追加する。
11. `rainbow.md`、`domain-objects.md`、`master-data.md` を実装内容に同期する。

## Test Plan

- 虹上の小人から `CloudJumpSearchDistance` より遠い雲にはジャンプしない。
- 距離内に雲が入ると、小人が虹から雲へ弧を描いて移動する。
- 動いている雲に対しても小人が接触点へ到着する。
- 小人が雲に触れると既存の雨が発生する。
- 雨音、植物成長、雨遮蔽が既存どおり動く。
- 雲に触れた後、小人が元の虹へ弧を描いて戻る。
- 小人が虹の線上へ正確に着地し、空中や虹の内側へずれない。
- 着地後、小人が帰還位置から `SurfaceWalk` を再開する。
- 帰還時にジャンプ開始位置より大きく後戻りしない。
- 同じ雲へ複数の小人が同時にジャンプしない。
- 複数の雲が近い場合、最も近い雲が選ばれる。
- 雲へのジャンプ中に虹が消えた場合、雲へ触れた後は画面下の地面へ落ちる。
- 虹への帰還中に虹が消えた場合、現在位置から画面下の地面へ落ちる。
- 虹が消えかけている時は新規ジャンプを開始しない。
- ジャンプから虹へ戻るまでの間に手輪郭が重なっても、移動が途中で中断されない。
- 対象雲を失った場合、小人が地面へ安全に戻る。
- `U` キーで候補選択とジャンプを単独確認できる。
- 既存の虹歩行、虹上で触れた時の落下、葉へのぶら下がり、小人の見上げ、雨、植物、妖精、音声が壊れない。
- Unity `6000.4.10f1` でコンパイルが通る。

## Acceptance Criteria

- 虹を歩いている小人だけが雲へジャンプする。
- 小人が虹から雲へ移動したことが、見た目だけで理解できる。
- ジャンプ軌道が直線ではなく自然な弧に見える。
- 雲が動いても接触が成立する。
- 雲への接触によって既存の雨が確実に発生する。
- 雲への接触後、小人がジャンプ元の虹へ戻る。
- 帰還後、小人が虹上の歩行を自然に再開する。
- 元の虹がない場合だけ、上辺や横辺ではなく画面下の地面へ落ちる。
- 一つの雲に小人が重なりすぎない。
- ジャンプ機能を無効化しても既存の虹歩行が変わらない構造になっている。

## Assumptions

- 座標は既存どおり左上原点の正規化座標を使用する。
- 雲はworld所有の `AmbientObject` であり、RealSense入力ではない。
- 虹と雲は同時に最大数が少ないため、MVPでは全候補走査で十分である。
- 雨の生成ロジックは変更せず、既存の雲接触リアクションを利用する。
- 初期実装では一つの雲に同時に一人だけジャンプできる。
- 雲に触れた小人は、原則としてジャンプ元の虹へ戻る。
- ジャンプ元の虹が存在しない場合だけ、画面下の地面へ落下する。
