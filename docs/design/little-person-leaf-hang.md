# Little Person Leaf Hang Design

## Summary

植物の近くで見上げている小人が、物体認識による入力に触れられたとき、植物の葉っぱにぶら下がる演出を追加する。



体験としては以下を目指す。

- 小人が植物を見上げる
- 来場者が手や検出物体でその周辺に触れる
- 小人が葉っぱにぴょんと移動し、一定時間ぶら下がる
- ぶら下がっている小人にもう一度触れると、葉っぱから落ちる
- ぶら下がり後は元の縁歩きへ戻る

## Current State

現在の関連実装は以下。

- `LittlePersonView`
  - 歩行スプライトを表示する
  - 植物が近いとランダムで `*_up_left` / `*_up_right` に切り替える
  - `IsLookingAtPlant` で見上げ中かどうかを外部へ返せる

- `LittlePeopleWorldController`
  - 小人ごとに植物が近いか判定し、`LittlePersonView.Render()` に渡す
  - 見上げ中の小人IDを `World.SetMovementPausedLittlePeople()` に渡す

- `World` / `LittlePerson`
  - `HoldPosition()` により、見上げ中の小人をその場に止められる

- `WorldSpaceMaskAnimationController`
  - 植物の `PlantModel` を持つ
  - `TryGetNearestPlantLookTarget()` で、小人の近くにある植物の花方向を返せる
  - 葉っぱは `PlantViewRuntime.RenderLeaves()` 内で描画時に計算されている
  - 葉っぱ位置は現時点では `PlantModel` のデータとして保存されていない

## Design Decision

MVPでは「葉っぱにぶら下がる」を採用する。




## Required Assets

ぶら下がり画像は、歩行画像・見上げ画像と同じく `Assets/Resources/LittlePeople/` から `Resources.Load` できる名前にする。

画像名:

```text
blue_hang1_left.png
blue_hang2_left.png

green_hang1.png
green_hang2.png

red_hang1.png
red_hang2.png

yellow_hang1.png
yellow_hang2.png

```

画像仕様:

- 背景は透明
- 小人が両手を上げていることが分かる
- 2枚の差分で足や体が少し揺れる


## Leaf Target Model

現状、葉っぱ位置は `PlantViewRuntime.RenderLeaves()` の中で以下のように計算されている。

```csharp
var ratio = count == 1 ? 0.5f : Mathf.Lerp(start, end, (float)i / (count - 1));
var side = i % 2 == 0 ? -1f : 1f;
var localPosition = bloomLocalPosition * ratio;
var leafDirection = Quaternion.AngleAxis(leafAngleDegrees * side, Vector3.forward) * stemDirection;
```

この計算を描画専用に閉じ込めず、共通化する。

追加方針:

- `WorldSpaceMaskAnimationController` に葉っぱ候補位置を計算するメソッドを追加する
- 描画とぶら下がり判定が同じ計算を使えるようにする
- MVPでは葉っぱの根元または葉っぱ中央付近をぶら下がり先にする

候補メソッド例:

```csharp
bool TryGetNearestLeafHangTarget(
    Vector3 worldPosition,
    float radiusWorld,
    out Vector3 hangWorldPosition,
    out bool hangLeft);
```

戻り値:

- `hangWorldPosition`
  - 小人をぶら下げるワールド座標
- `hangLeft`
  - `true` なら left 画像
  - `false` なら right 画像

## Trigger Rule

MVPの発火条件:

1. 小人が植物を見上げている
2. 物体認識による入力で小人に触れる（デバッグ用にクリックでも反応するようにしたい）

入力判定は、最初は厳密な輪郭接触ではなく、既存のマスク/入力物体から簡易判定する。

候補:

- `RecognitionMask` の白領域が葉っぱ付近にある
- または `InteractionObject` の中心/輪郭が葉っぱ付近にある

MVPでは `RecognitionMask` を優先する。

理由:

- 手の輪郭も物体の輪郭も白マスク化されている
- 「触れている感じ」を出しやすい
- Unity側の演出と統合しやすい

## Little Person State

ぶら下がりは Domain の本格状態として作るより、MVPでは `LittlePersonView` と `LittlePeopleWorldController` 側の一時演出として扱う。

理由:

- ぶら下がりは展示演出寄り
- 小人の移動ルール本体を大きく変えずに済む
- 既存の `HoldPosition()` を使って位置を止められる

追加する View 状態:

```text
PlantLook
  -> LeafHang
  -> LeafDrop
  -> PlantLook or EdgeWalk
```

`LittlePersonView` に持たせる候補フィールド:

```csharp
float leafHangTimer;
float leafHangCooldownTimer;
bool leafHangLeft;
Vector3 leafHangWorldPosition;
float leafDropTimer;
Vector3 leafDropStartWorldPosition;
Vector3 leafDropEndWorldPosition;
```

外部公開:

```csharp
public bool IsHangingFromLeaf => leafHangTimer > 0f;
public bool IsDroppingFromLeaf => leafDropTimer > 0f;
public bool IsPlantInteractionLocked => IsLookingAtPlant || IsHangingFromLeaf || IsDroppingFromLeaf;
```

`LittlePeopleWorldController` は `IsPlantInteractionLocked` の小人IDを `World.SetMovementPausedLittlePeople()` に渡す。

## Animation Rule

ぶら下がり中:

- 小人の表示位置は `leafHangWorldPosition` に固定する
- スプライトは `hang1` / `hang2` を交互に表示する
- 通常の歩行アニメーションは止める
- 小人の回転は基本 `Quaternion.identity`
- 必要なら葉っぱ方向に合わせて少し傾ける

アニメーション速度:

```csharp
leafHangFrameSeconds = 0.18f;
```

ぶら下がり時間:

```csharp
leafHangDurationSeconds = new Vector2(1.2f, 2.2f);
```

再発火待ち:

```csharp
leafHangCooldownSeconds = 2.5f;
```

表示優先度:

```text
LeafDrop > LeafHang > PlantLook > Walk
```

## Drop Rule

ぶら下がり中に、物体認識入力またはデバッグ用クリックで小人にもう一度触れた場合、小人は葉っぱから落下する。

MVPでは落下専用画像をまだ用意しないため、落下中の見た目は以下のどちらかにする。

- ぶら下がり画像をそのまま使う
- 既存の小人スプライトを使い、少し回転させる

最初は「ぶら下がり画像をそのまま使う」を採用する。

落下は Domain の `Falling` へ統合せず、MVPでは `LittlePersonView` 側の一時演出として扱う。

理由:

- 葉っぱぶら下がり自体が表示演出寄りである
- 既存の棒落下や縁歩きの Domain ルールを壊しにくい
- 落下画像が未確定でも実装しやすい

落下挙動:

```text
LeafHang
  -> second touch
  -> LeafDrop
  -> EdgeWalk
```

`LeafDrop` 中:

- `leafDropStartWorldPosition` から `leafDropEndWorldPosition` へ移動する
- `leafDropEndWorldPosition` は小人の元の縁歩き位置、または現在の `LittlePerson.Position` から得たワールド座標にする
- 落下時間は短めにする
- 落下中も小人の Domain 移動は止める
- 落下完了後、`leafHangCooldownSeconds` を入れてすぐ再ぶら下がりしないようにする

調整値候補:

```csharp
leafDropDurationSeconds = 0.45f;
leafDropArcHeightWorld = 0.35f;
```

落下軌道:

- MVPでは直線補間でもよい
- 見た目を良くする場合、放物線風に少し弧を描かせる
- 落下画像が追加されたら、`LeafDrop` 中だけ専用画像へ差し替える

## Second Touch Detection

「もう一度触れる」の判定は、最初は厳密な輪郭衝突ではなく簡易判定でよい。

MVP判定:

- `IsHangingFromLeaf` の小人だけ対象にする
- 小人の現在表示位置、または `leafHangWorldPosition` の近くに入力マスクがあるかを見る
- デバッグ用にはクリック位置が小人に近い場合も落下させる

判定距離候補:

```csharp
leafDropTouchRadiusWorld = 0.45f;
```

