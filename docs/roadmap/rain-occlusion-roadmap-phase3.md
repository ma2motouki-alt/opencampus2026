# Rain Occlusion Roadmap Phase 3: Impact Feedback And Exhibition Tuning

## Goal

雨が手や物体に当たったことを、展示体験として分かりやすく、気持ちよく見せる。

Phase 1 で植物成長を止め、Phase 2 で雨の見た目を止めた後、Phase 3 では当たった場所に小さな水しぶき・波紋・光の反応を追加する。

## Target Scope

含めるもの:

- 雨が遮蔽された地点のフィードバック
- 水しぶき、波紋、光などの軽量な演出
- 表示頻度と強さの調整
- デバッグ表示
- 展示環境向けのチューニング

含めないもの:

- 高精度な流体表現
- 物体の材質ごとの水表現
- 複雑なパーティクルシステム
- FMOD などへの音響基盤移行

## Dependency

Phase 3 は Phase 1 と Phase 2 の後に行う。

前提:

- 雨が検出領域で遮蔽される
- 遮蔽された位置が取得できる
- 雨の見た目が遮蔽位置付近で止まる

## Experience Goal

来場者が手や物体を雨の下に置いた時に、以下が直感的に分かること。

- 雨が自分の手や物体に当たっている
- 雨が地面まで届いていない
- 手を動かすと雨の当たる場所も変わる
- 反応がうるさすぎず、世界の雰囲気を壊さない

## Proposed Effects

まずは軽量な Procedural 表示でよい。

候補:

- 小さな青白い点が数個はねる
- 薄い円形波紋
- 検出輪郭の上端付近が一瞬明るくなる
- 雨粒が当たった場所に短い発光線を出す

新しい `VisualEffectKind` を追加する場合:

```text
RainSplash = 6
```

ただし、最初は `WorldSpaceMaskAnimationController` 内のローカル描画でもよい。既存の `VisualEffectMaster` に入れるかは、演出が固まってから判断する。

## Proposed Parameters

```text
enableRainSplash = true
rainSplashCooldownSeconds = 0.08
rainSplashSizePx = 4
rainSplashLifetimeSeconds = 0.25
rainSplashAlpha = 0.7
showRainOcclusionDebug = false
```

## Work Items

- 雨が遮蔽された位置を記録する
- 一定時間隔で splash を生成する
- splash は短時間でフェードアウトする
- 検出ノイズで大量発生しないよう cooldown を入れる
- デバッグ表示では遮蔽ラインと遮蔽点を確認できるようにする
- `parameter-manual.md` に調整項目を追記する

## Debug Display

必要なデバッグ表示:

- 雨の縦判定ライン
- 遮蔽された最初のピクセル
- `groundYPx`
- 雨が地面に届いたか、遮られたか

ただし本番表示では消せること。

## Acceptance Check

- 手や物体で雨を止めると、当たっている場所に小さな反応が出る
- 反応がうるさすぎず、雨や植物より前に出すぎない
- 検出ノイズで大量に splash が出ない
- 右クリック雨でも雲雨でも同じように反応する
- 本番表示ではデバッグ線を消せる
- Phase 1/2 の遮蔽ロジックが壊れない

## Handoff Notes

Phase 3 は見た目調整が中心。展示会場の明るさ、検出ノイズ、画面サイズを見ながら Inspector で調整する。

最初から作り込みすぎず、Phase 1/2 が安定してから演出量を決める。
