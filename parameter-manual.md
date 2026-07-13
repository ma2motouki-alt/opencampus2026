# Parameter Manual

This document describes the parameters used by the current `main` branch.

Most visual tuning happens in `Assets/Scripts/LittlePeopleWorld/Unity/WorldSpaceMaskAnimationController.cs`. Domain-level tuning and effect defaults are in `Assets/Scripts/LittlePeopleWorld/Master/Masters.cs`.

## Unity Inspector Parameters

Select the GameObject that has `LittlePeopleWorldController`. The controller automatically ensures `WorldSpaceMaskAnimationController` exists when recognition mask animation is enabled.

### Development Rain

| Parameter | Default | Meaning |
|---|---:|---|
| `enableDevelopmentClickRain` | `true` | Right-click in the Game view to spawn a development-only rain column. |
| `developmentRainDurationSeconds` | `2.0` | How long the clicked rain column remains active. |
| `developmentRainWidth` | `0.08` | Normalized width of the clicked rain column. |

Clicked rain uses the same `RainColumn` visual effect as cloud rain, so rain visuals, rain audio, and plant growth follow the existing runtime path.

### Mask

| Parameter | Default | Meaning |
|---|---:|---|
| `maskWidth` | `256` | Internal mask buffer width. |
| `maskHeight` | `144` | Internal mask buffer height. |
| `includeHandContours` | `true` | Include hand contour objects in the mask. |
| `includeBarContours` | `true` | Include bar contour objects in the mask. |
| `showMask` | `false` | Show the low-resolution mask texture. Usually off for exhibition. |
| `transparentMaskBackground` | `false` | Use transparent background for the mask texture. |
| `minBlobPixels` | `40` | Ignore tiny mask blobs after rasterization. |

The visible hand/object fill is rendered by `InteractionObjectView` as a contour mesh. The mask texture is mainly an internal animation buffer for particles and plants.

### Particles

| Parameter | Default | Meaning |
|---|---:|---|
| `particleCount` | `160` | Number of small particles. |
| `particleSize` | `0.10` | Particle visual size in Unity world units. |
| `speedPxPerSec` | `55` | Base particle speed in mask pixels per second. |
| `steerLerp` | `6` | Direction steering smoothness. |
| `separationRadiusPx` | `8` | Particle separation radius. |
| `separationGain` | `380` | Strength of separation force. |
| `targetJitterPx` | `9` | Randomness around target points. |

### Particle Fairy Wings

| Parameter | Default | Meaning |
|---|---:|---|
| `showParticleWings` | `true` | Show translucent wings on both sides of each particle. |
| `particleWingSideOffsetRatio` | `0.44` | Wing distance from the particle body, relative to `particleSize`. |
| `particleWingBackOffsetRatio` | `-0.08` | Wing backward offset, relative to `particleSize`. |
| `particleWingWidthRatio` | `0.42` | Wing width, relative to `particleSize`. |
| `particleWingLengthRatio` | `2` | Wing length, relative to `particleSize`. |
| `particleWingAlpha` | `0.52` | Wing transparency. |
| `particleWingFlapSpeed` | `14` | Wing flap animation speed. |

### Mask Following

| Parameter | Default | Meaning |
|---|---:|---|
| `blurRadius` | `4` | Blur radius for the mask field. |
| `contourLevel` | `0.5` | Contour threshold for steering. |
| `correctionGain` | `5` | Strength of correction toward detected regions. |
| `senseThreshold` | `0.03` | Minimum sensed field value. |
| `penSenseRadiusPx` | `70` | Search radius around mask regions. |
| `minPenAttractRadiusPx` | `18` | Minimum attraction radius. |
| `penAttractRadiusPerSqrtPixel` | `2.5` | Attraction radius growth by blob size. |

### Particle Cloud Rain

| Parameter | Default | Meaning |
|---|---:|---|
| `enableParticleCloudRain` | `true` | Allows particles to trigger cloud rain. |
| `particleCloudTouchRadius` | `0.01` | Extra normalized padding added to cloud contact radius for particles. |
| `particleCloudRainCooldownSeconds` | `0.2` | Per-cloud cooldown before particles can retrigger rain. |

The cloud contact radius itself is defined in `AmbientObjectTypeMaster`.

### Rain To Plants

| Parameter | Default | Meaning |
|---|---:|---|
| `enableRainPlants` | `true` | Rain creates and grows plants. |
| `rainFallSpeedPxPerSec` | `40` | Internal rain landing speed for plant growth. |
| `groundYPx` | `3` | Plant root line in mask coordinates. |
| `maxPlants` | `10` | Maximum live plants. |
| `plantMaxHeightPx` | `46` | Mature plant height in mask pixels. |
| `plantStemWidthPx` | `2.0` | Stem width. |
| `plantFlowerSizePx` | `18` | Flower size. |
| `plantSpawnMergingRadiusRatio` | `0.5` | Merge rain into an existing nearby plant. |
| `plantInfluenceRadiusRatio` | `0.8` | Particle attraction range around a plant. |
| `plantClimbAttractRadiusRatio` | `0.3` | Distance from stem that counts as climbing. |
| `plantClimbSpeedMultiplier` | `2.0` | Speed multiplier while climbing toward a flower. |
| `plantSeedlingDuration` | `10` | Seedling stage duration. |
| `plantGrowingDuration` | `10` | Growing stage duration. |
| `plantWiltingStartDelay` | `5` | Delay before wilting after blooming. |
| `plantWiltingDuration` | `15` | Wilting duration. |

### Rain Occlusion

| Parameter | Default | Meaning |
|---|---:|---|
| `enableRainOcclusionByMask` | `true` | Prevent rain from reaching the ground when the vertical rain path intersects the current recognition mask. |
| `rainOcclusionProbeRadiusPx` | `1` | Horizontal mask-pixel radius checked around each falling rain sample. Higher values make occlusion easier to trigger. |
| `rainOcclusionTopPaddingPx` | `15` | Minimum vertical distance from the cloud, in mask pixels, before a recognition mask can block rain. Increase it to allow touching near the cloud without clipping the rain. |
| `showRainOcclusionDebug` | `false` | Adds rain occlusion counters to the debug overlay when `D` debug display is enabled. |
| `enableRainVisualOcclusion` | `true` | Shorten the visible rain column when its path intersects the current recognition mask. |
| `rainOcclusionVisualSmoothingSeconds` | `0.12` | Smooths visible rain-height changes to reduce flicker from noisy masks. Set to `0` for immediate clipping. |
| `rainOcclusionMinVisibleHeightPx` | `4` | Minimum visible clipped rain height in mask pixels, preventing rain from disappearing completely at the source. |

Phase 1 blocks plant spawning/growth. Phase 2 also clips the visible rain column at the first detected occlusion height.

### Plant Leaves

| Parameter | Default | Meaning |
|---|---:|---|
| `plantLeafCount` | `4` | Number of leaves placed along each plant stem. |
| `plantLeafLengthPx` | `10` | Leaf length in mask pixels before world conversion. |
| `plantLeafWidthPx` | `4.5` | Leaf width in mask pixels before world conversion. |
| `plantLeafStartRatio` | `0.24` | First leaf position along the stem, from root `0` to flower `1`. |
| `plantLeafEndRatio` | `0.72` | Last leaf position along the stem. |
| `plantLeafAngleDegrees` | `42` | Angle at which leaves open away from the stem. |
| `plantLeafAlpha` | `0.82` | Leaf body opacity. |
| `plantLeafVeinAlpha` | `0.62` | Leaf vein opacity. |

Leaves are visual-only. They are generated by `PlantViewRuntime` and do not change rain, growth, flower burst, or particle logic.

### Flower Burst

| Parameter | Default | Meaning |
|---|---:|---|
| `burstInitialSpeedPxPerSec` | `220` | Initial speed when flower-attached particles burst. |
| `burstFreeSeconds` | `1.5` | Time particles fly freely after burst. |
| `burstReattachCooldownSeconds` | `2.0` | Time before a burst particle can attach again. |

Flower burst is triggered when a hand contour touches the flower area. It does not use the old paint-mask changed-pixel threshold. The burst SE plays only when at least one flower-attached particle is actually released.

### Audio Layers

`WorldAudioController` is created automatically by `LittlePeopleWorldController` when audio layers are enabled. Add the component to the same GameObject in edit mode when you want to assign clips from the Inspector.

| Parameter | Default | Meaning |
|---|---:|---|
| `enableAudioLayers` | `true` | Enable the layered audio controller. This lives on `LittlePeopleWorldController`. |
| `baseAmbientClip` | none | Always-on ambient loop. |
| `rainLayerClip` | none | Loop that fades in while `RainColumn` effects exist. |
| `plantGrowthLayerClip` | none | Loop that fades in while plants are seedling/growing. |
| `plantStartClip` | none | One-shot SE played when a new plant is created by rain. |
| `plantBloomClip` | none | One-shot SE played when a plant first reaches the flower-visible stage. |
| `flowerBurstClip` | none | One-shot SE played when a touched flower releases attached particles. |
| `masterVolume` | `1` | Global multiplier for all audio layers. |
| `baseAmbientVolume` | `0.35` | Target volume for the base ambient layer. |
| `rainLayerVolume` | `0.45` | Target volume for the rain layer. |
| `plantGrowthLayerVolume` | `0.25` | Target volume for the plant growth layer. |
| `plantStartVolume` | `0.5` | One-shot SE volume for new plant start. |
| `plantBloomVolume` | `0.55` | One-shot SE volume for the flower-bloom timing. |
| `flowerBurstVolume` | `0.65` | One-shot SE volume for the flower burst timing. |
| `layerFadeSeconds` | `1.2` | Fade duration for layer volume changes. |
| `playBaseAmbient` | `true` | Play the base ambient layer. |
| `enableRainLayer` | `true` | Enable the rain layer. |
| `enablePlantGrowthLayer` | `true` | Enable the plant growth layer. |
| `enablePlantStartOneShot` | `true` | Enable the new-plant one-shot SE. |
| `enablePlantBloomOneShot` | `true` | Enable the flower-bloom one-shot SE. |
| `enableFlowerBurstOneShot` | `true` | Enable the flower burst one-shot SE. |

## Master Data Parameters

### Cloud

Location: `Assets/Scripts/LittlePeopleWorld/Master/Masters.cs`

Current cloud master:

```csharp
new AmbientObjectTypeMaster(
    1,
    AmbientObjectKind.Cloud,
    "drifting cloud",
    new Vector2(0.095f, 0.05f),
    new Vector2(0.014f, 0.003f),
    0.075f,
    new Color(0.86f, 0.95f, 1f, 0.78f),
    4,
    0.18f,
    0.38f)
```

Meaning:

- Size: `0.095 x 0.05`
- Drift velocity: `0.014 x 0.003`
- Contact radius: `0.075`
- Visual effect id: `4` (`RainColumn`)
- Movement edge padding: `0.18`
- Max center Y: `0.38`

Clouds are intentionally constrained away from the display edge path so rain does not trigger accidentally as often.

### Rain

Current `RainColumn` visual effect:

```csharp
new VisualEffectMaster(
    4,
    VisualEffectKind.RainColumn,
    VisualEffectRenderMode.Procedural,
    "cloud rain column",
    new Color(0.44f, 0.84f, 1f, 1f),
    4.0f,
    0.72f,
    new Vector2(0.075f, 0.28f),
    0.45f,
    string.Empty,
    0.4f)
```

Important values:

- Pulse speed: `4.0`
- Alpha: `0.72`
- Default size: `0.075 x 0.28`
- Duration: `0.45`
- Drop size scale: `0.4`

`RainLingerSeconds` is currently `2.0`. Touching a cloud keeps rain active for about two seconds after the last touch refresh.

### Star

Star contact radius is `0.085`, and star cooldown is `1.4` seconds.

## RealSense Parameters

Location: `python/realsense/config.py`

Important current values:

| Parameter | Value |
|---|---:|
| `MIN_CONTOUR_AREA_PIXELS` | `500` |
| `MORPH_KERNEL_SIZE` | `5` |
| `MAPPER_MODE` | `front` |
| `HAND_MIN_CONTOUR_AREA_PIXELS` | `800` |
| `HAND_MAX_CONTOUR_AREA_PIXELS` | `80000` |
| `HAND_APPROX_EPSILON_RATIO` | `0.005` |
| `HAND_MAX_POINTS` | `80` |
| `HAND_MIN_POINTS` | `8` |

## Exhibition Mode Notes

- Use mouse input first to verify Unity behavior.
- Use UDP RealSense mode for the actual camera.
- Press `4` in mouse mode to stop mouse placement and observe the world.
- Press `D` to toggle debug overlays.
- Use Python debug preview windows to tune noise and contour filtering before blaming Unity mapping.
