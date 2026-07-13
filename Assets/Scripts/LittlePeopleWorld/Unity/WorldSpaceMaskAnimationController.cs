using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using LittlePeopleWorld.Unity.Animation;
using LittlePeopleWorld.Unity.Animation.Fairy;
using LittlePeopleWorld.Unity.Animation.Plants;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public readonly struct LeafHangSlot : IEquatable<LeafHangSlot>
    {
        public LeafHangSlot(int plantId, int leafIndex)
        {
            PlantId = plantId;
            LeafIndex = leafIndex;
        }

        public int PlantId { get; }
        public int LeafIndex { get; }
        public bool Equals(LeafHangSlot other) => PlantId == other.PlantId && LeafIndex == other.LeafIndex;
        public override bool Equals(object obj) => obj is LeafHangSlot other && Equals(other);
        public override int GetHashCode() => unchecked((PlantId * 397) ^ LeafIndex);
    }

    // Scene互換用のMonoBehaviour。処理本体はAnimation配下の独立クラスへ委譲する。
    public sealed class WorldSpaceMaskAnimationController : MonoBehaviour
    {
        [Header("Mask")]
        [SerializeField] int maskWidth = 256;
        [SerializeField] int maskHeight = 144;
        [SerializeField] bool includeHandContours = true;
        [SerializeField] bool showMask;
        [SerializeField] bool transparentMaskBackground;
        [SerializeField] Color32 maskOnColor = new(255, 255, 255, 255);
        [SerializeField] Color32 maskOffColor = new(8, 8, 10, 255);
        [SerializeField] int minBlobPixels = 40;

        [Header("Particles")]
        [SerializeField] int particleCount = 80;
        [SerializeField] float particleSize = 0.10f;
        [SerializeField] float speedPxPerSec = 25f;
        [SerializeField] float steerLerp = 6f;
        [SerializeField] Color[] particlePalette =
        {
            new(1.00f, 0.85f, 0.25f), new(1.00f, 0.45f, 0.85f), new(0.45f, 0.95f, 0.85f),
            new(0.75f, 0.55f, 1.00f), new(1.00f, 1.00f, 1.00f)
        };

        [Header("Particle Fairy Wings")]
        [SerializeField] bool showParticleWings = true;
        [SerializeField] float particleWingSideOffsetRatio = 0.44f;
        [SerializeField] float particleWingBackOffsetRatio = -0.08f;
        [SerializeField] float particleWingWidthRatio = 0.82f;
        [SerializeField] float particleWingLengthRatio = 2f;
        [SerializeField] float particleWingAlpha = 0.52f;
        [SerializeField] float particleWingFlapSpeed = 10f;

        [Header("Particle Separation")]
        [SerializeField] float separationRadiusPx = 8f;
        [SerializeField] float separationGain = 380f;
        [SerializeField] float targetJitterPx = 9f;

        [Header("Mask Following")]
        [SerializeField] int blurRadius = 4;
        [SerializeField] float contourLevel = 0.5f;
        [SerializeField] float correctionGain = 5f;
        [SerializeField] float senseThreshold = 0.03f;
        [SerializeField] float penSenseRadiusPx = 70f;
        [SerializeField] float minPenAttractRadiusPx = 18f;
        [SerializeField] float penAttractRadiusPerSqrtPixel = 2.5f;

        [Header("Edge Walk")]
        [SerializeField] float edgeMarginPx = 7f;
        [SerializeField] float edgePullGain = 0.12f;

        [Header("Particle Cloud Rain")]
        [SerializeField] bool enableParticleCloudRain = true;
        [SerializeField] float particleCloudTouchRadius = 0.01f;
        [SerializeField] float particleCloudRainCooldownSeconds = 0.2f;

        [Header("Rain To Plants")]
        [SerializeField] bool enableRainPlants = true;
        [SerializeField] float rainFallSpeedPxPerSec = 40f;
        [SerializeField] float groundYPx = 3f;

        [Header("Rain Occlusion")]
        [SerializeField] bool enableRainOcclusionByMask = true;
        [SerializeField] int rainOcclusionProbeRadiusPx = 1;
        [Tooltip("Minimum vertical distance from the cloud before recognition masks can block rain.")]
        [SerializeField, Min(0)] int rainOcclusionTopPaddingPx = 15;
        [SerializeField] bool showRainOcclusionDebug;
        [SerializeField] bool enableRainVisualOcclusion = true;
        [SerializeField] float rainOcclusionVisualSmoothingSeconds = 0.12f;
        [SerializeField] int rainOcclusionMinVisibleHeightPx = 4;

        [Header("Plants")]
        [SerializeField] int maxPlants = 10;
        [SerializeField] float plantMaxHeightPx = 46f;
        [SerializeField] float plantStemWidthPx = 2f;
        [SerializeField] float plantFlowerSizePx = 18f;
        [SerializeField] float plantSpawnMergingRadiusRatio = 0.5f;
        [SerializeField] float plantInfluenceRadiusRatio = 0.8f;
        [SerializeField] float plantClimbAttractRadiusRatio = 0.3f;
        [SerializeField] float plantClimbSpeedMultiplier = 2f;
        [SerializeField] float plantSeedlingDuration = 10f;
        [SerializeField] float plantGrowingDuration = 10f;
        [SerializeField] float plantWiltingStartDelay = 5f;
        [SerializeField] float plantWiltingDuration = 15f;

        [Header("Plant Leaves")]
        [SerializeField] int plantLeafCount = 4;
        [SerializeField] float plantLeafLengthPx = 10f;
        [SerializeField] float plantLeafWidthPx = 4.5f;
        [SerializeField] float plantLeafStartRatio = 0.24f;
        [SerializeField] float plantLeafEndRatio = 0.72f;
        [SerializeField] float plantLeafAngleDegrees = 80f;
        [SerializeField, Range(0f, 1f)] float plantLeafAlpha = 0.82f;
        [SerializeField, Range(0f, 1f)] float plantLeafVeinAlpha = 0.62f;

        [Header("Little Person Leaf Hang")]
        [SerializeField] float leafHangOffsetPx = 3f;

        [Header("Bloom Attraction")]
        [SerializeField] float bloomAttractRadiusRatio = 0.5f;
        [SerializeField] float bloomSphereRadiusRatio = 1f;
        [SerializeField] float bloomAttractForce = 300f;

        [Header("Flower Burst")]
        [SerializeField, Min(0f)] float flowerBurstTouchRadiusRatio = 0.25f;
        [SerializeField] float burstInitialSpeedPxPerSec = 220f;
        [SerializeField] float burstFreeSeconds = 1.5f;
        [SerializeField] float burstReattachCooldownSeconds = 2f;

        WorldAnimationController runtime;

        public bool HasActivePlants => runtime?.HasActivePlants == true;
        public bool HasGrowingPlants => runtime?.HasGrowingPlants == true;
        public int ActiveBloomCount => runtime?.ActiveBloomCount ?? 0;
        public int PlantSpawnSequence => runtime?.PlantSpawnSequence ?? 0;
        public int PlantBloomSequence => runtime?.PlantBloomSequence ?? 0;
        public int FlowerBurstSequence => runtime?.FlowerBurstSequence ?? 0;
        public string RainOcclusionDebugText => runtime?.RainOcclusionDebugText ?? string.Empty;

        public void Render(World world, MasterDatabase masters, NormalizedScreenMapper mapper, float deltaTime, bool debugEnabled)
        {
            EnsureRuntime();
            runtime.ConfigureMask(maskWidth, maskHeight, minBlobPixels, includeHandContours, showMask, transparentMaskBackground, maskOnColor, maskOffColor);
            runtime.Render(
                world, masters, mapper, deltaTime, debugEnabled, blurRadius,
                BuildFairySettings(), BuildPlantSettings(),
                enableRainPlants, rainFallSpeedPxPerSec, groundYPx,
                enableRainOcclusionByMask, rainOcclusionProbeRadiusPx, rainOcclusionTopPaddingPx,
                showRainOcclusionDebug, enableRainVisualOcclusion,
                rainOcclusionVisualSmoothingSeconds, rainOcclusionMinVisibleHeightPx);
        }

        public void SetVisible(bool visible)
        {
            EnsureRuntime();
            runtime.SetVisible(visible);
        }

        public void PrepareSpatialQueries(NormalizedScreenMapper mapper)
        {
            EnsureRuntime();
            runtime.PrepareSpatialQueries(mapper);
        }

        public bool TryGetNearestPlantLookTarget(Vector3 position, float radius, out int plantId, out Vector3 target)
        {
            EnsureRuntime();
            return runtime.TryGetNearestPlantLookTarget(position, radius, out plantId, out target);
        }

        public bool TryGetHighestAvailableLeafHangTarget(int plantId, Vector3 position, ISet<LeafHangSlot> occupied, out LeafHangSlot slot, out Vector3 target, out bool hangLeft)
        {
            EnsureRuntime();
            return runtime.TryGetHighestAvailableLeafHangTarget(plantId, position, occupied, out slot, out target, out hangLeft);
        }

        public bool TryGetLeafHangTarget(int plantId, int leafIndex, out Vector3 target)
        {
            EnsureRuntime();
            return runtime.TryGetLeafHangTarget(plantId, leafIndex, out target);
        }

        public bool IsInputNearWorldPosition(IReadOnlyList<InteractionObject> objects, Vector3 position, float radius)
        {
            EnsureRuntime();
            return runtime.IsInputNearWorldPosition(objects, position, radius);
        }

        public float GetRainVisibleHeightRatio(VisualEffectInstance effect)
        {
            EnsureRuntime();
            return runtime.GetRainVisibleHeightRatio(effect);
        }

        void EnsureRuntime()
        {
            runtime ??= new WorldAnimationController(transform);
        }

        FairySettings BuildFairySettings() => new()
        {
            Count = particleCount, Size = particleSize, SpeedPxPerSec = speedPxPerSec, SteerLerp = steerLerp,
            Palette = particlePalette, ShowWings = showParticleWings, WingSideOffsetRatio = particleWingSideOffsetRatio,
            WingBackOffsetRatio = particleWingBackOffsetRatio, WingWidthRatio = particleWingWidthRatio,
            WingLengthRatio = particleWingLengthRatio, WingAlpha = particleWingAlpha, WingFlapSpeed = particleWingFlapSpeed,
            SeparationRadiusPx = separationRadiusPx, SeparationGain = separationGain, TargetJitterPx = targetJitterPx,
            ContourLevel = contourLevel, CorrectionGain = correctionGain, SenseThreshold = senseThreshold,
            PenSenseRadiusPx = penSenseRadiusPx, MinPenAttractRadiusPx = minPenAttractRadiusPx,
            PenAttractRadiusPerSqrtPixel = penAttractRadiusPerSqrtPixel,
            EdgeMarginPx = edgeMarginPx, EdgePullGain = edgePullGain, EnableCloudRain = enableParticleCloudRain,
            CloudTouchRadius = particleCloudTouchRadius, CloudRainCooldownSeconds = particleCloudRainCooldownSeconds,
            BurstInitialSpeedPxPerSec = burstInitialSpeedPxPerSec, BurstFreeSeconds = burstFreeSeconds,
            BurstReattachCooldownSeconds = burstReattachCooldownSeconds
        };

        PlantSettings BuildPlantSettings() => new()
        {
            MaxPlants = maxPlants, MaxHeightPx = plantMaxHeightPx, StemWidthPx = plantStemWidthPx,
            FlowerSizePx = plantFlowerSizePx, SpawnMergingRadiusRatio = plantSpawnMergingRadiusRatio,
            InfluenceRadiusRatio = plantInfluenceRadiusRatio, ClimbAttractRadiusRatio = plantClimbAttractRadiusRatio,
            ClimbSpeedMultiplier = plantClimbSpeedMultiplier, SeedlingDuration = plantSeedlingDuration,
            GrowingDuration = plantGrowingDuration, WiltingStartDelay = plantWiltingStartDelay,
            WiltingDuration = plantWiltingDuration, LeafCount = plantLeafCount, LeafLengthPx = plantLeafLengthPx,
            LeafWidthPx = plantLeafWidthPx, LeafStartRatio = plantLeafStartRatio, LeafEndRatio = plantLeafEndRatio,
            LeafAngleDegrees = plantLeafAngleDegrees, LeafAlpha = plantLeafAlpha, LeafVeinAlpha = plantLeafVeinAlpha,
            LeafHangOffsetPx = leafHangOffsetPx, BloomAttractRadiusRatio = bloomAttractRadiusRatio,
            BloomSphereRadiusRatio = bloomSphereRadiusRatio, BloomAttractForce = bloomAttractForce,
            FlowerBurstTouchRadiusRatio = flowerBurstTouchRadiusRatio
        };
    }
}
