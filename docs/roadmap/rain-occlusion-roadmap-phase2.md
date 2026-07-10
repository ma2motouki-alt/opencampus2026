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
- Inspector 調整値

含めないもの:

- 水しぶきや波紋
- 雨粒ごとの完全な物理挙動
- 検出物体の材質ごとの雨表現
- Python / RealSense 側の変更

## Implementation

`WorldSpaceMaskAnimationController` が `RainColumn` ごとの表示高さ倍率を計算し、`VisualEffectView` がその倍率を受け取って RainColumn の描画高さを短くする。

```text
WorldSpaceMaskAnimationController
  -> TryFindRainOcclusionY()
  -> rainVisibleHeightRatios[effect.Id]
  -> LittlePeopleWorldController
  -> VisualEffectView.Render(..., rainVisibleHeightRatio)
  -> RainColumnEffectRenderer
```

## Required Features

### Occlusion Height

Phase 1 の雨遮蔽判定を拡張し、遮蔽された最初の y 座標を返せるようにする。

```text
TryFindRainOcclusionY(rainOriginPx, landingPx, out blockedYPx)
```

### Visual Height Ratio

雨の発生位置から地面までを `1.0` とし、遮蔽位置までの距離を表示高さ倍率にする。

```text
visibleHeightRatio = distance(rainOrigin, blockedY) / distance(rainOrigin, groundY)
```

遮蔽がない場合は `1.0` に戻す。

### Sampling

雨柱は横幅を持つため、中心線だけではなく雨幅内を複数点サンプリングする。

- 初期実装は 5 点
- もっとも雨源に近い遮蔽位置を採用
- これにより、手や物体に雨が当たった瞬間に雨柱がそこで止まって見える

### Smoothing

検出マスクのノイズで雨の高さが細かく揺れるため、表示高さ倍率は軽くスムージングする。

## Parameters

| Parameter | Default | Meaning |
|---|---:|---|
| `enableRainVisualOcclusion` | `true` | 雨の見た目を検出マスクで短くする |
| `rainOcclusionVisualSmoothingSeconds` | `0.12` | 雨の高さ変化のスムージング秒数 |
| `rainOcclusionMinVisibleHeightPx` | `4` | 遮蔽時でも最低限表示する雨の高さ |

既存の `rainOcclusionProbeRadiusPx` は、着地判定と表示遮蔽の両方に使う。

## Acceptance Check

- 手や物体の上で雨が止まって見える
- 植物が生えない位置と、雨が止まる見た目が大きくズレない
- 遮蔽物を外すと雨が地面まで戻る
- 雨柱が複数あっても表示が混ざらない
- 雨音の ON/OFF は従来通り `RainColumn` の存在に従う
- Phase 1 の植物遮蔽が壊れない

## Handoff Notes

Phase 2 は見た目の調整が必要になりやすい。まず `rainOcclusionProbeRadiusPx = 0..1`、`rainOcclusionVisualSmoothingSeconds = 0.08..0.18` あたりから調整する。
