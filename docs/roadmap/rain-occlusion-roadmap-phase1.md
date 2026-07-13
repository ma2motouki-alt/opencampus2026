# Rain Occlusion Roadmap Phase 1: Block Rain Landing Logic

## Goal

物体検出で得られた手や物体の輪郭を「雨をさえぎる遮蔽物」として扱う。

Phase 1 では、まず見た目の雨はそのままでよい。雨が検出領域に当たった場合に地面まで届かず、植物発生・植物成長が起きない状態を作る。

## Target Scope

含めるもの:

- `WorldSpaceMaskAnimationController.UpdateRainLanding()` の遮蔽判定
- `RainColumn` の発生位置から地面までの縦方向チェック
- `effectiveMask` を使った雨のブロック判定
- 遮蔽された雨では `OnRainLanded()` を呼ばない処理
- 右クリック開発用雨、雲雨、粒による雲雨の共通対応
- Inspector 調整値
- `parameter-manual.md` の追記

含めないもの:

- 雨の見た目を途中で止める処理
- 雨が当たった場所の水しぶき
- Python / RealSense 側の変更
- Domain 層への天候モデル追加

## Current Implementation

現在の雨から植物への流れ:

```text
Cloud / particle-cloud / development right-click
  -> World.VisualEffects に RainColumn が生成される
  -> VisualEffectView が RainColumn を描画する
  -> WorldSpaceMaskAnimationController.UpdateRainLanding()
      -> RainColumn の位置から groundYPx まで雨が届いた扱いにする
      -> OnRainLanded()
      -> 植物を生成、または成長させる
```

物体検出マスクの流れ:

```text
InteractionObject.ContourPoints
  -> WorldSpaceMaskAnimationController.BuildMaskFromInteractionObjects()
  -> mask / effectiveMask
  -> particles / plants / flower burst に利用
```

Phase 1 では、この `effectiveMask` を雨遮蔽にも使う。

## Required Features

### Rain Block Detection

追加する想定メソッド:

```text
IsRainBlockedByMask(Vector2 rainOriginPx, Vector2 landingPx)
```

判定ルール:

- 雨の発生点 `rainOriginPx` から地面 `landingPx` までを縦方向に見る
- x 座標の周辺 `rainOcclusionProbeRadiusPx` px も見る
- `effectiveMask == true` のピクセルがあれば遮蔽とする
- `rainOcclusionTopPaddingPx` ぶんだけ発生点直下を無視し、雨の開始位置付近の誤判定を避ける

### Rain Landing Control

`UpdateRainLanding()` 内で、従来は必ず呼んでいた処理:

```text
OnRainLanded(landingPositionPx)
```

これを遮蔽判定付きにする。

```text
if (!IsRainBlockedByMask(rainOrigin, landingPositionPx))
    OnRainLanded(landingPositionPx)
```

重要:

- 遮蔽された雨でも `landedCount` は進める
- `landedCount` を進めないと、遮蔽物が外れた瞬間に過去分の雨がまとめて地面へ届いてしまう

## Proposed Parameters

`WorldSpaceMaskAnimationController` に追加する。

```text
enableRainOcclusionByMask = true
rainOcclusionProbeRadiusPx = 2
rainOcclusionTopPaddingPx = 12
showRainOcclusionDebug = false
```

意味:

- `enableRainOcclusionByMask`: 雨遮蔽 ON/OFF
- `rainOcclusionProbeRadiusPx`: 雨の x 座標周辺を何 px 幅で見るか
- `rainOcclusionTopPaddingPx`: 判定開始位置を雨発生点から少し下げる
- `showRainOcclusionDebug`: 遮蔽判定確認用

## Work Items

- `WorldSpaceMaskAnimationController` に `Rain Occlusion` Inspector 項目を追加する
- `IsRainBlockedByMask()` を追加する
- `UpdateRainLanding()` に遮蔽判定を入れる
- `parameter-manual.md` に調整値を追記する
- 必要なら遮蔽された回数の簡易デバッグ表示を追加する

## Acceptance Check

- 何も検出されていない状態では、雨が従来通り植物を生やす
- 手や物体の検出領域を雨の下に置くと、その下に植物が生えない
- 検出領域を雨から外すと、再び植物が生える
- 右クリック雨でも雲雨でも同じ遮蔽判定が効く
- 小さなノイズだけで雨が止まりすぎない
- 雨音や雨の発生条件は壊れない
- 既存の粒、植物、花のはじけが壊れない

## Handoff Notes

Phase 1 は最小変更で進める。`VisualEffectView` には触れない。

見た目の雨が地面まで降っていても、植物が生えなければ Phase 1 は成功とする。
