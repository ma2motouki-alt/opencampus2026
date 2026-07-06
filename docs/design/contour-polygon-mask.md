# Contour Polygon Mask Spec

## Summary

RealSense / Python から送られてくる `InteractionObject.points` を使い、Unity上で検出領域を直接ポリゴンとして塗りつぶす。

これまでの `256x144` などの低解像度2値マスクをそのままTextureとして画面に貼る方式は、以下のズレが入りやすい。

- Python preview の画像座標
- UDP の正規化座標
- Unity Texture のピクセル座標
- Unity world 座標
- Camera / display のアスペクト比
- Texture更新時の上下反転

そのため、表示用の白マスクは **画像マスクTextureではなく、`points` で囲まれた領域をUnity world上のMeshとして塗りつぶす** 方針にする。

2値マスクは廃止するのではなく、粒子や植物の内部計算に必要な場合だけ使う。画面に見せる主表示はポリゴンMeshとする。

## Goals

- RealSenseで検出された手や物体の形が、Unity上でそのまま塗りつぶされて見える。
- 表示座標の変換経路を短くし、上下反転や解像度差によるズレを減らす。
- 小人、粒子、植物アニメーションはUnity world座標を主として扱う。
- 既存の `InteractionObject.ContourPoints`、手輪郭線表示、棒WalkableSurface、UDP入力を壊さない。
- 将来、白塗りだけでなく、半透明、発光、波紋、植物生成領域などへ拡張できる。

## Non Goals

- 手の3D形状復元はしない。
- 指一本ごとの認識はしない。
- ポリゴンの完全な自己交差修復はMVPでは扱わない。
- 2値画像フィルタをそのままUnity画面へ貼る方式は採用しない。
- Python側の検出アルゴリズム自体はこの仕様では変更しない。

## Coordinate Policy

### Normalized Coordinate

Python / UDP / domainで扱う標準座標。

```text
x: 0.0 left -> 1.0 right
y: 0.0 top  -> 1.0 bottom
```

`InteractionObject.ContourPoints` はこの座標系を使う。

### Unity World Coordinate

Unityの表示、粒子、植物、小人アニメーションで主に使う座標。

既存の `NormalizedScreenMapper` を唯一の変換口にする。

```csharp
Vector3 world = mapper.ToWorld(normalized);
Vector2 normalized = mapper.ToNormalized(world);
```

### Mask Pixel Coordinate

MVPの表示には使わない。

必要な場合だけ、粒子や植物の内部計算用に低解像度バッファを持ってよい。ただし、この座標系をUnity表示の正としない。

## Data Flow

```text
RealSense / Python
  -> UDP JSON
  -> UdpRealSenseInputProviderBehaviour
  -> InteractionObject[]
  -> LittlePeopleWorldOrchestrator
  -> World.InteractionObjects
  -> ContourPolygonMaskSystem
       - ContourPoints を world polygon へ変換
       - Mesh を三角形分割して白く塗る
       - Polygon query を粒子/植物へ提供
  -> Unity Views / Animation
```

## UDP Contract

既存の `shape=contour` をそのまま使う。

```json
{
  "id": 7,
  "kind": "hand",
  "shape": "contour",
  "x": 0.42,
  "y": 0.58,
  "w": 0.22,
  "h": 0.18,
  "angle": 0,
  "height": 0.06,
  "state": "placed",
  "points": [
    { "x": 0.38, "y": 0.51 },
    { "x": 0.44, "y": 0.49 },
    { "x": 0.50, "y": 0.54 }
  ]
}
```

Rules:

- `shape == contour` かつ `points.Count >= 3` の object を塗りつぶし対象にする。
- `points` は輪郭順に並んでいる前提にする。
- `points` がない場合は従来の primitive 表示にフォールバックする。
- `kind == hand` は手領域として塗る。
- `kind == bar_prop` でも `shape == contour` と `points` があれば、表示は輪郭塗りつぶしを優先できる。ただし、反応は既存の `bar_prop -> WalkableSurface` を維持する。

## Key Components

### `ContourPolygonMaskSystem`

輪郭塗りつぶしと、アニメーション用のポリゴンクエリをまとめるUnity側システム。

Responsibilities:

- 現在の `World.InteractionObjects` から contour object を集める。
- `ContourPoints` を `NormalizedScreenMapper.ToWorld()` でworld polygonに変換する。
- objectごとに `ContourPolygonMaskView` を同期する。
- 粒子・植物用に `ContourPolygonArea` の一覧を公開する。
- UDP停止やobject消失時に対応するviewを消す。

### `ContourPolygonMaskView`

1つの contour object を白塗りMeshとして表示する。

Responsibilities:

- world polygonから `Mesh` を作る。
- Mesh頂点は `Vector3(x, y, z)` とする。
- `MeshRenderer` で半透明白を描画する。
- 必要に応じて `LineRenderer` で輪郭線を重ねる。
- sorting layer / order をInspectorで調整できるようにする。

Initial visual:

- Fill color: white
- Alpha: `0.65`
- Outline: optional, light blue or hidden
- Sorting: backgroundより上、小人・重要演出より下

### `ContourTriangulator`

ポリゴンをMesh用の三角形に分割する。

MVPでは ear clipping を使う。

Requirements:

- 凹形状を扱える。
- 時計回り / 反時計回りの入力に対応する。
- 連続する重複点や極端に近い点を事前に除外する。
- 自己交差している場合は、Mesh生成をスキップして輪郭線表示へフォールバックする。

Fallback:

- 三角形分割に失敗した場合、塗りつぶしは表示しない。
- `LineRenderer` の輪郭線だけ表示して、Unity実行を止めない。

### `ContourPolygonArea`

粒子や植物が参照するworld座標の領域情報。

Responsibilities:

- `ContainsWorld(Vector3 worldPosition)`
- `DistanceToEdge(Vector3 worldPosition)`
- `ClosestPointOnEdge(Vector3 worldPosition)`
- `Bounds`
- `BottomPoint`
- `TopPoint`
- `RandomPointInside()`

Coordinate:

- 内部データはworld座標を正とする。
- 低解像度mask座標を正にしない。

## Animation Integration

### Particles

粒子はworld座標で動く。

White mask areaがある場合:

- 粒子は最寄りの `ContourPolygonArea` に引き寄せられる。
- 領域内に入った粒子は、輪郭の内側や輪郭線付近を漂う。
- 参照は `ContainsWorld` と `ClosestPointOnEdge` を使う。

White mask areaがない場合:

- 既存の漂い、縁歩き、環境反応に戻る。

### Plants

植物もworld座標で管理する。

Generation:

- 雨や小人反応などで植物を生成する場合、生成候補位置は `ContourPolygonArea` の下端、または地面に近い位置から選ぶ。
- 画面全体の2値mask下端ではなく、world polygonの `BottomPoint` や `Bounds` を使う。

Growth:

- 茎は縦方向に伸ばす。
- 花は茎の先端に咲かせる。
