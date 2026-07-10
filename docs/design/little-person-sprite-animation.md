# Little Person Sprite Animation

## Goal

小人の見た目を procedural な丸・線の組み合わせから、手描きスプライトによる2フレーム歩行アニメーションへ置き換える。

4種類の小人画像を master data の `LittlePersonArchetypeMaster` と対応させ、各小人が自分の archetype に応じた画像セットを使う。

## Target Scope

対象に含めるもの:

- 4種類の小人 archetype
- 4色の小人スプライト
- 各色2枚の歩行フレーム
- 歩行中の `frame1 / frame2` 交互表示
- 画面の下辺・上辺・左辺・右辺で足元が縁に向く回転
- 既存の発光色、驚き、落下、物体上移動の表示差分

対象外:

- Animator Controller の導入
- 複雑な状態別アニメーション
- 手足や表情のボーン制御
- 小人の移動ロジック変更

## Asset Layout

runtime で読み込む画像は `Resources` 配下に置く。

```text
Assets/
  Resources/
    LittlePeople/
      blue1.png
      blue2.png
      green1.png
      green2.png
      red1.png
      red2.png
      yellow1.png
      yellow2.png
```

画像の意味:

| ArchetypeId | Sprite prefix | Frames |
|---|---|---|
| `1` | `blue` | `blue1`, `blue2` |
| `2` | `green` | `green1`, `green2` |
| `3` | `red` | `red1`, `red2` |
| `4` | `yellow` | `yellow1`, `yellow2` |

## Master Data

`LittlePersonArchetypeMaster` は4種類にする。

- `1`: blue walker
- `2`: green walker
- `3`: red walker
- `4`: yellow walker

`World.Create` では `1 + i % 4` で初期小人へ archetype を割り当てる。

## View Design

`LittlePersonView` は以下の責務に限定する。

- `LittlePerson.ArchetypeId` から画像セットを選ぶ
- 小人が動いている時だけ2フレームを交互表示する
- `EdgeWalk` 中は画面の最寄り辺から足元方向を決める
- `SurfaceWalk` / `RideSurface` などは速度方向を基準に回転する
- sprite が読み込めない場合は従来の円表示へフォールバックする

## Edge Rotation Rule

スプライト画像は「足が下、頭が上」の姿勢を基準にする。

Unity表示では、足元が地面・縁に接するように回転させる。

| 近い辺 | 回転 |
|---|---:|
| 下辺 | `0` degrees |
| 上辺 | `180` degrees |
| 左辺 | `-90` degrees |
| 右辺 | `90` degrees |

これにより、左右の辺や上の辺でも足が縁についているように見える。

## Animation Rule

歩行中は `frameSeconds` ごとに2枚を切り替える。

```text
blue1 -> blue2 -> blue1 -> blue2
```

停止中や落下中は基本的に1枚目を表示する。

## Implementation Notes

- `Resources.Load<Texture2D>()` で画像を読み込む。
- `Sprite.Create()` で pivot を bottom center にして sprite 化する。
- 画像 import 設定への依存を避けるため、Unity の Sprite import 設定を手で揃えなくても最低限動く。
- `LittlePersonView` の `frameSeconds` と `spriteHeightMultiplier` はコードのデフォルト値で調整する。

## Acceptance Check

- 起動時に4種類の小人が混ざって表示される。
- 歩いている小人が2フレームでぱたぱた動く。
- 下辺では足が下、上辺では足が上、左辺では足が左、右辺では足が右を向く。
- 既存の小人移動、雨、植物、RealSense入力が壊れない。
- 画像が見つからない場合でも円表示で最低限動く。
