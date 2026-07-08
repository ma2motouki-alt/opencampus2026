# パラメータ調整マニュアル(LITTLE ROYALE 統合ブランチ)

`integration/rain_shape-on-main` ブランチの統合作業以降に調整したパラメータの、ファイル名・コード上の位置・変更方法をまとめたもの。

Unityインスペクターから直接いじれるものと、コードを直接編集する必要があるものの2種類がある。

---

## 1. インスペクターから直接いじれるパラメータ

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Unity/WorldSpaceMaskAnimationController.cs`

Unityのヒエラルキーで、この`WorldSpaceMaskAnimationController`コンポーネントがアタッチされているGameObjectを選択すると、インスペクターに`[Header("...")]`ごとにセクション分けされたパラメータが並ぶ。再生中でも値を変更でき、その場で見た目に反映される。

| セクション | 主な項目 | 内容 |
|---|---|---|
| `Particles` | `particleCount` / `particleSize` / `speedPxPerSec` / `steerLerp` | 粒の数・大きさ・基本移動速度・操舵の効き |
| `Particle Separation` | `separationRadiusPx` / `separationGain` / `targetJitterPx` | 粒同士の反発・揺らぎ |
| `Rain To Plants` | `plantClimbAttractRadiusRatio` / `plantClimbSpeedMultiplier` / `plantInfluenceRadiusRatio` | 植物への接近・登りの強さ(詳細は§3) |
| `Bloom Attraction` | `bloomAttractRadiusRatio` / `bloomSphereRadiusRatio` / `bloomAttractForce` | 花への吸着範囲・引力の強さ |
| `Flower Burst` | `burstInitialSpeedPxPerSec` / `burstFreeSeconds` / `burstReattachCooldownSeconds` | 花のはじけ演出(詳細は§4) |

---

## 2. 植物の見た目(茎・花のスプライト形状)

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Unity/WorldSpaceMaskAnimationController.cs`

**検索キーワード**: `CreateRenderer("Stem"` または `CreateRenderer("Flower"`

`PlantViewRuntime.Initialize()`内、各パーツのスプライト種類を指定している箇所。

```csharp
public void Initialize()
{
    stemGlowRenderer = CreateRenderer("StemGlow", RuntimeSpriteFactory.Circle, -3);
    stemRenderer = CreateRenderer("Stem", RuntimeSpriteFactory.Circle, 6);      // ← ここ(現状:Circle)
    flowerGlowRenderer = CreateRenderer("FlowerGlow", RuntimeSpriteFactory.Circle, 7);
    flowerRenderer = CreateRenderer("Flower", RuntimeSpriteFactory.Star, 8);    // ← ここ(現状:Star=花びら付き)
    flowerCenterRenderer = CreateRenderer("FlowerCenter", RuntimeSpriteFactory.Circle, 9);
}
```

- 第2引数(`RuntimeSpriteFactory.○○`)を `Circle` / `Square` / `Star` / `Teardrop` に差し替えると形状が変わる
- 現状: 茎=Circle(丸みのある形)、花=Star(花びら付き)、それ以外=Circle

---

## 3. 粒が茎を登る強さ・判定範囲

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Unity/WorldSpaceMaskAnimationController.cs`

### 3-1. 登る速さ(インスペクターで調整可能)

**項目名**: `Plant Climb Speed Multiplier`(`[Header("Rain To Plants")]`内、`plantClimbAttractRadiusRatio`の下)

```csharp
// 登り中(IsClimbing)の粒を、通常移動より確実に花へ向かわせるための倍率。
// 1.0だと通常移動と同じ強さのまま、値を上げるほど登りが速く・向きの追従も鋭くなる。
[SerializeField] float plantClimbSpeedMultiplier = 1.6f;
```

インスペクターの数値を直接変更するだけでよい(コード編集不要)。目安は1.5〜2.5程度。

### 3-2. 登りを判定する範囲(コード編集が必要)

**検索キーワード**: `ComputePlantApproach`

以前は「茎の根元1点からの距離」で判定していたため、背の高い茎の先端付近が判定範囲外になっていた。現在は「根元→花の線分全体までの最短距離」で判定するよう修正済み。

```csharp
(Vector2 Direction, bool IsClimbing) ComputePlantApproach(Particle particle, PlantModel plant)
{
    var distanceToStem = DistancePointToSegment(particle.Pos, plant.Position, plant.BloomPosition);
    var climbRadius = Mathf.Max(4f, plant.HeightPx * plantClimbAttractRadiusRatio);
    ...
```

判定範囲の広さ自体は`plantClimbAttractRadiusRatio`(インスペクターの`Rain To Plants`セクション)で調整する。

---

## 4. 花のはじけ(手が触れると粒が飛び散る演出)

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Unity/WorldSpaceMaskAnimationController.cs`

**項目**: インスペクターの `[Header("Flower Burst")]` セクション

| 項目名 | 内容 |
|---|---|
| `Burst Initial Speed Px Per Sec` | はじけた粒の飛び出す速さ(既定220) |
| `Burst Free Seconds` | 自由飛散状態(操舵されず慣性で飛ぶ)が続く時間(既定1.5秒) |
| `Burst Reattach Cooldown Seconds` | はじけた粒が再吸着できるようになるまでの時間(既定2.0秒、**粒ごと**の管理) |

いずれもインスペクターの数値変更のみで調整可能(コード編集不要)。発動条件(接触判定の範囲)自体は`Bloom Attraction`セクションの`bloomSphereRadiusRatio`で調整する。

---

## 5. 雨が画面下端まで届くか

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Domain/DomainModels.cs`

**検索キーワード**: `static Vector2 EffectSize`

雲の実際の高さから画面下端(正規化y=1.0)までの距離を、雨柱の高さとして動的に計算している。

```csharp
static Vector2 EffectSize(AmbientObject ambientObject, VisualEffectMaster effectMaster)
{
    if (effectMaster.Kind == VisualEffectKind.RainColumn)
    {
        var originY = RainPosition(ambientObject).y;
        var heightToGround = Mathf.Max(0.05f, 1f - originY);

        return new Vector2(
            Mathf.Max(effectMaster.DefaultSize.x, ambientObject.Size.x * 0.75f),
            heightToGround);
    }

    return effectMaster.DefaultSize;
}
```

- `0.05f`(最低保証の高さ)を変えると、雲が画面のかなり下の方にいる場合の見た目が変わる
- **注意**: `Assets/Scripts/LittlePeopleWorld/Master/Masters.cs`内の`TuningParameterMaster`の`rainLingerSeconds`(雲へのタッチ後、雨がどれだけ長く反応し続けるか)はこの「地面まで届くかどうか」には**無関係**と判明済み。触っても雨の飛距離は変わらないので注意。

---

## 6. 雨粒の速さ・大きさ

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Master/Masters.cs`

**検索キーワード**: `VisualEffectKind.RainColumn`

```csharp
new VisualEffectMaster(4, VisualEffectKind.RainColumn, VisualEffectRenderMode.Procedural, "cloud rain column", new Color(0.44f, 0.84f, 1f, 1f), 7.5f, 0.72f, new Vector2(0.075f, 0.28f), 0.45f, string.Empty, 1.5f),
```

引数の位置(前から数えて何番目か)で役割が決まる。

| 位置 | 現在値の例 | 役割 |
|---|---|---|
| 6番目 | `7.5f`(または`12.0f`など) | **落下の速さ**(`PulseSpeed`。大きいほど速い) |
| 8番目 | `new Vector2(0.075f, 0.28f)` | 幅・高さの基準値。**高さ(2つ目の値)は§5の`EffectSize()`で上書きされるため無効**。1つ目の値(幅)のみ有効 |
| 11番目(末尾、省略可) | `1.5f` | **粒の大きさ**(`DropSizeScale`。省略時は既定1.0f) |

末尾に`DropSizeScale`の値を追加/変更する形で大きさを調整し、6番目の値で速さを調整する。

---

## 7. 観察モード(操作を一切受け付けない展示モード)

**対象ファイル**: `Assets/Scripts/LittlePeopleWorld/Input/MouseInputProviderBehaviour.cs`

操作方法(コード編集不要): 実行中にキーボードの **`4`** を押すと、配置・選択・ドラッグ・削除いずれのマウス操作も無効になる「何もしないモード」に切り替わる。`1`/`2`/`3`のいずれかを押すと通常モードに戻る。

コード側の場所(参考): `inputEnabled`フィールドと`UpdateModeKeys()`内の`KeyCode.Alpha4`分岐。

---

## 早見表

| やりたいこと | ファイル | 検索キーワード |
|---|---|---|
| 粒の数・速度・大きさなど基本パラメータ | (コード編集不要) | Unityインスペクター |
| 茎・花の形(丸/四角/星/涙型) | `WorldSpaceMaskAnimationController.cs` | `CreateRenderer("Stem"` / `"Flower"` |
| 登る速さ | (コード編集不要) | インスペクター `Plant Climb Speed Multiplier` |
| 登りの判定範囲 | `WorldSpaceMaskAnimationController.cs` | `ComputePlantApproach` |
| 花のはじけの速さ・持続時間 | (コード編集不要) | インスペクター `Flower Burst` |
| 雨が地面まで届くか | `DomainModels.cs` | `EffectSize` |
| 雨粒の速さ・大きさ | `Masters.cs` | `VisualEffectKind.RainColumn` |
| 観察モードの切り替え | (コード編集不要) | 実行中にキー`4` |
