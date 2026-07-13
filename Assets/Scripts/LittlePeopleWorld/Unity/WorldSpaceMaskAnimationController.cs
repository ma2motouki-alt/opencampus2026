using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
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

        public bool Equals(LeafHangSlot other)
        {
            return PlantId == other.PlantId && LeafIndex == other.LeafIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is LeafHangSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((PlantId * 397) ^ LeafIndex);
        }
    }

    public sealed partial class WorldSpaceMaskAnimationController : MonoBehaviour
    {
        const int CellsX = 16;
        const int CellsY = 9;

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
            new(1.00f, 0.85f, 0.25f),
            new(1.00f, 0.45f, 0.85f),
            new(0.45f, 0.95f, 0.85f),
            new(0.75f, 0.55f, 1.00f),
            new(1.00f, 1.00f, 1.00f)
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

        [SerializeField] int maxPlants = 10;
        [SerializeField] float plantMaxHeightPx = 46f;
        [SerializeField] float plantStemWidthPx = 2.0f;
        [SerializeField] float plantFlowerSizePx = 18f;
        [SerializeField] float plantSpawnMergingRadiusRatio = 0.5f;
        [SerializeField] float plantInfluenceRadiusRatio = 0.8f;
        [SerializeField] float plantClimbAttractRadiusRatio = 0.3f;
        // 登り中(IsClimbing)の粒を、通常移動より確実に花へ向かわせるための倍率。
        // 1.0だと通常移動と同じ強さのまま、値を上げるほど登りが速く・向きの追従も鋭くなる。
        [SerializeField] float plantClimbSpeedMultiplier = 2.0f;
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
        [SerializeField] float bloomSphereRadiusRatio = 1.0f;
        [SerializeField] float bloomAttractForce = 300f;

        // 花のはじけ(A): rain_shape版はペン描画で変化したマスクピクセル数(burstMinChangedPixels)を
        // 閾値にしていたが、mainでは手の輪郭(Hand種別のInteractionObject)がそのまま入力になるため、
        // 「ラスタのノイズ的な単発ピクセル」を弾く必要がなく、bloomSphereRadius(既存の吸い付き範囲)への
        // 幾何学的な重なり判定だけで十分に安定する。そのため burstMinChangedPixels 相当のしきい値は
        // 廃止し、代わりに「手の輪郭が bloomSphereRadius 以内に入った瞬間」を発火条件にしている。
        [Header("Flower Burst")]
        [SerializeField, Min(0f)] float flowerBurstTouchRadiusRatio = 0.25f;
        [SerializeField] float burstInitialSpeedPxPerSec = 220f;
        [SerializeField] float burstFreeSeconds = 1.5f;
        [SerializeField] float burstReattachCooldownSeconds = 2.0f;

        readonly List<Particle> particles = new();
        readonly List<PlantModel> plants = new();
        readonly Dictionary<int, PlantViewRuntime> plantViews = new();
        readonly Dictionary<int, float> rainActiveSeconds = new();
        readonly Dictionary<int, int> rainLandedCounts = new();
        readonly Dictionary<int, float> rainVisibleHeightRatios = new();
        readonly Dictionary<int, float> particleCloudRainCooldowns = new();
        readonly List<int> floodStack = new();
        readonly List<int> componentBuffer = new();

        bool[] mask;
        bool[] effectiveMask;
        bool[] visited;
        int[] componentSizeMap;
        int[] cellCounts = Array.Empty<int>();
        int[] cellMaxComponentSize = Array.Empty<int>();
        float[] field;
        float[] fieldTmp;
        Color32[] pixels;
        Texture2D maskTexture;
        SpriteRenderer maskRenderer;
        Transform particleRoot;
        Transform plantRoot;
        NormalizedScreenMapper mapper;
        int effectiveWhiteCount;
        int nextPlantId = 1;
        int rainOcclusionBlockedDropsThisFrame;
        int rainOcclusionLandedDropsThisFrame;
        int rainOcclusionVisualClippedColumnsThisFrame;

        public bool HasActivePlants => plants.Count > 0;
        public int ActiveBloomCount
        {
            get
            {
                var count = 0;
                foreach (var plant in plants)
                {
                    if (plant.IsBloomable)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PlantSpawnSequence { get; private set; }
        public int PlantBloomSequence { get; private set; }
        public int FlowerBurstSequence { get; private set; }
        public string RainOcclusionDebugText { get; private set; } = string.Empty;


        public bool HasGrowingPlants
        {
            get
            {
                foreach (var plant in plants)
                {
                    if (plant.CurrentStage is PlantStage.Seedling or PlantStage.Growing)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        int MaskW => Mathf.Clamp(maskWidth, 16, 1024);
        int MaskH => Mathf.Clamp(maskHeight, 16, 1024);

        public void Render(World world, MasterDatabase masters, NormalizedScreenMapper screenMapper, float deltaTime, bool debugEnabled)
        {
            if (world == null || masters == null || screenMapper == null)
            {
                SetVisible(false);
                return;
            }

            mapper = screenMapper;
            EnsureRuntime();

            BuildMaskFromInteractionObjects(world.InteractionObjects);
            RecomputeEffectiveMask();
            RebuildField();
            RebuildCellCounts();
            RenderMaskTexture();

            var dt = Mathf.Clamp(deltaTime, 0f, 0.05f);

            // 粒の操舵より前に判定する(はじけた粒がこのフレームから自由飛散状態になるようにするため)
            HandleFlowerBurst(world.InteractionObjects);

            AdvanceParticles(dt);
            TriggerCloudRainFromParticles(world, masters, dt);
            AdvanceRainPlants(world, masters, dt);
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            if (maskRenderer != null)
            {
                maskRenderer.enabled = visible && showMask;
            }

            if (particleRoot != null)
            {
                particleRoot.gameObject.SetActive(visible);
            }

            if (plantRoot != null)
            {
                plantRoot.gameObject.SetActive(visible);
            }
        }

        void EnsureRuntime()
        {
            EnsureRoots();
            EnsureBuffers();
            EnsureMaskSprite();
            EnsureParticles();
        }

        void EnsureRoots()
        {
            if (maskRenderer == null)
            {
                var maskObject = new GameObject("Recognition Binary Mask");
                maskObject.transform.SetParent(transform, false);
                maskRenderer = maskObject.AddComponent<SpriteRenderer>();
                maskRenderer.sortingOrder = -10;
            }

            if (particleRoot == null)
            {
                particleRoot = new GameObject("Mask Particles").transform;
                particleRoot.SetParent(transform, false);
            }

            if (plantRoot == null)
            {
                plantRoot = new GameObject("Mask Plants").transform;
                plantRoot.SetParent(transform, false);
            }
        }


    }
}
