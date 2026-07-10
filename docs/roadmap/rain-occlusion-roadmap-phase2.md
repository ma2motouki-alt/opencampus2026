# Rain Occlusion Roadmap Phase 2: Clip Rain Visuals At Occlusion Height

## Goal

Phase 1 で作った雨遮蔽判定を、雨の見た目にも反映する。

検出領域に雨が当たった場合、雨粒が地面まで描画されるのではなく、手や物体の位置で止まって見える状態を作る。

## Target Scope

含めるもの:

- RainColumn の表示高さ調整
- 雨遮蔽位置の保持
- 雨の見た目と植物成長判定の整合
- 複数 RainColumn の遮蔽対応
- 表示ちらつきの軽減

含めないもの:

- 水しぶきや波紋
- 雨粒ごとの完全な物理挙動
- 検出物体の材質ごとの雨表現
- Python / RealSense 側の変更

## Dependency

Phase 2 は Phase 1 の完了後に行う。

前提:

- `IsRainBlockedByMask()` 相当の判定がある
- 雨が遮蔽された場合に地面へ届かないロジックがある
- 遮蔽位置、または少なくとも遮蔽された高さを取得できる

## Current Implementation

雨の描画は `VisualEffectView` の `RainColumnEffectRenderer` が担当している。

```text
VisualEffectInstance
  -> VisualEffectView
  -> RainColumnEffectRenderer.Render()
      -> effect.Size.y の高さで雨粒を描画
```

Phase 1 では `WorldSpaceMaskAnimationController` が遮蔽判定を持つ想定なので、Phase 2 ではその情報を描画側へどう渡すかを決める必要がある。

## Implementation Options

### Option A: Animation Layer Stores Occlusion State

`WorldSpaceMaskAnimationController` が RainColumn ごとの遮蔽情報を保持する。

```text
rainOcclusionByEffectKey:
  source id or effect id
  -> blockedY / visibleHeight
```

`LittlePeopleWorldController` 経由で `VisualEffectView` に渡すか、`VisualEffectView` が参照できる表示用データを用意する。

メリット:

- Phase 1 の判定結果をそのまま使える
- 植物成長と見た目のズレを減らせる

デメリット:

- Controller と View の同期処理が少し増える

### Option B: VisualEffectView Also Samples Mask

`VisualEffectView` が直接マスクを見て雨を短くする。

メリット:

- 表示の中で完結する

デメリット:

- `VisualEffectView` が `WorldSpaceMaskAnimationController` の内部マスクを知ることになる
- 責務が混ざりやすい

推奨は **Option A**。

## Required Features

### Occlusion Height

Phase 1 の `IsRainBlockedByMask()` を拡張し、遮蔽された最初の y 座標を返せるようにする。

想定:

```text
TryFindRainOcclusionY(rainOriginPx, landingPx, out blockedYPx)
```

### Visual Height Conversion

遮蔽位置を正規化座標またはワールド座標へ変換し、`RainColumn` の見た目の高さを短くする。

```text
visibleHeight = blockedY - rainOriginY
```

低解像度マスク座標からの変換には既存の変換を使う。

- `MaskPxToNormalized`
- `MaskToWorld`

### Stabilization

検出マスクのノイズで雨の高さが細かく揺れる可能性がある。

必要なら以下を入れる。

```text
rainOcclusionVisualSmoothingSeconds = 0.12
rainOcclusionMinVisibleHeightPx = 4
```

## Proposed Parameters

```text
enableRainVisualOcclusion = true
rainOcclusionVisualSmoothingSeconds = 0.12
rainOcclusionMinVisibleHeightPx = 4
```

## Work Items

- Phase 1 の遮蔽判定を「遮蔽位置を返す」形へ拡張する
- RainColumn ごとの遮蔽高さを保持する
- `VisualEffectView` またはその呼び出し前で雨表示高さを調整する
- 遮蔽がない場合は従来通りの表示に戻す
- 表示のちらつきが強い場合はスムージングする
- `parameter-manual.md` に追記する

## Acceptance Check

- 手や物体の上で雨が止まって見える
- 植物が生えない位置と、雨が止まる見た目が大きくズレない
- 遮蔽物を外すと雨が地面まで戻る
- 雨柱が複数あっても表示が混ざらない
- 雨音の ON/OFF は従来通り `RainColumn` の存在に従う
- Phase 1 の植物遮蔽が壊れない

## Handoff Notes

Phase 2 は Phase 1 より差分が大きくなる可能性がある。まず Phase 1 の判定が安定してから着手する。
