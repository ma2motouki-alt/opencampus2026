using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class MasterTable<TMaster> where TMaster : class
    {
        readonly Dictionary<int, TMaster> records = new();

        public MasterTable(IEnumerable<TMaster> source, Func<TMaster, int> keySelector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            foreach (var record in source)
            {
                records.Add(keySelector(record), record);
            }
        }

        public TMaster Get(int id)
        {
            if (records.TryGetValue(id, out var record))
            {
                return record;
            }

            throw new KeyNotFoundException($"Master record not found. type={typeof(TMaster).Name}, id={id}");
        }

        public bool TryGet(int id, out TMaster record)
        {
            return records.TryGetValue(id, out record);
        }
    }

    public sealed class WorldPresetMaster
    {
        public int Id { get; }
        public string Name { get; }
        public int InitialLittlePersonCount { get; }
        public int DefaultArchetypeId { get; }
        public int DefaultBehaviorProfileId { get; }
        public Color BackgroundColor { get; }

        public WorldPresetMaster(
            int id,
            string name,
            int initialLittlePersonCount,
            int defaultArchetypeId,
            int defaultBehaviorProfileId,
            Color backgroundColor)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            InitialLittlePersonCount = Math.Max(1, initialLittlePersonCount);
            DefaultArchetypeId = defaultArchetypeId;
            DefaultBehaviorProfileId = defaultBehaviorProfileId;
            BackgroundColor = backgroundColor;
        }
    }

    public sealed class LittlePersonArchetypeMaster
    {
        public int Id { get; }
        public string Name { get; }
        public Color BodyColor { get; }
        public float Size { get; }
        public float MoveSpeed { get; }
        public float Curiosity { get; }
        public float Fear { get; }

        public LittlePersonArchetypeMaster(
            int id,
            string name,
            Color bodyColor,
            float size,
            float moveSpeed,
            float curiosity,
            float fear)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            BodyColor = bodyColor;
            Size = Mathf.Max(0.005f, size);
            MoveSpeed = Mathf.Max(0.001f, moveSpeed);
            Curiosity = Mathf.Clamp01(curiosity);
            Fear = Mathf.Clamp01(fear);
        }
    }

    public sealed class BehaviorProfileMaster
    {
        public int Id { get; }
        public string Name { get; }
        public float WanderTurnInterval { get; }
        public float SteeringResponsiveness { get; }
        public float NeighborSeparationRadius { get; }
        public float NeighborSeparationStrength { get; }

        public BehaviorProfileMaster(
            int id,
            string name,
            float wanderTurnInterval,
            float steeringResponsiveness,
            float neighborSeparationRadius,
            float neighborSeparationStrength)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            WanderTurnInterval = Mathf.Max(0.1f, wanderTurnInterval);
            SteeringResponsiveness = Mathf.Max(0.1f, steeringResponsiveness);
            NeighborSeparationRadius = Mathf.Max(0.001f, neighborSeparationRadius);
            NeighborSeparationStrength = Mathf.Max(0f, neighborSeparationStrength);
        }
    }

    public sealed class ReactionMaster
    {
        public int Id { get; }
        public string Name { get; }
        public LittlePersonBehaviorKind BehaviorKind { get; }
        public LittlePersonEmotion Emotion { get; }
        public float DurationSeconds { get; }

        public ReactionMaster(
            int id,
            string name,
            LittlePersonBehaviorKind behaviorKind,
            LittlePersonEmotion emotion,
            float durationSeconds)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            BehaviorKind = behaviorKind;
            Emotion = emotion;
            DurationSeconds = Mathf.Max(0.05f, durationSeconds);
        }
    }

    public sealed class ReactionConditionMaster
    {
        public int Id { get; }
        public int ReactionMasterId { get; }
        public InteractionObjectKind ObjectKind { get; }
        public float TriggerDistance { get; }

        public ReactionConditionMaster(int id, int reactionMasterId, InteractionObjectKind objectKind, float triggerDistance)
        {
            Id = id;
            ReactionMasterId = reactionMasterId;
            ObjectKind = objectKind;
            TriggerDistance = Mathf.Max(0.001f, triggerDistance);
        }
    }

    public sealed class InteractionObjectTypeMaster
    {
        public int Id { get; }
        public InteractionObjectKind Kind { get; }
        public string Name { get; }
        public Vector2 DefaultSize { get; }
        public float DefaultHeight { get; }
        public int InteractionFieldMasterId { get; }
        public Color DebugColor { get; }

        public InteractionObjectTypeMaster(
            int id,
            InteractionObjectKind kind,
            string name,
            Vector2 defaultSize,
            float defaultHeight,
            int interactionFieldMasterId,
            Color debugColor)
        {
            Id = id;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DefaultSize = new Vector2(Mathf.Max(0.01f, defaultSize.x), Mathf.Max(0.01f, defaultSize.y));
            DefaultHeight = Mathf.Clamp01(defaultHeight);
            InteractionFieldMasterId = interactionFieldMasterId;
            DebugColor = debugColor;
        }
    }

    public sealed class InteractionFieldMaster
    {
        public int Id { get; }
        public InteractionFieldKind Kind { get; }
        public string Name { get; }
        public float Radius { get; }
        public float Strength { get; }
        public float EdgePadding { get; }

        public InteractionFieldMaster(
            int id,
            InteractionFieldKind kind,
            string name,
            float radius,
            float strength,
            float edgePadding)
        {
            Id = id;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Radius = Mathf.Max(0.001f, radius);
            Strength = Mathf.Max(0f, strength);
            EdgePadding = Mathf.Max(0f, edgePadding);
        }
    }

    public sealed class WalkableSurfaceMaster
    {
        public int Id { get; }
        public string Name { get; }
        public float AttachDistance { get; }
        public float DetachDistance { get; }
        public float SurfaceWalkSpeed { get; }
        public float RideVelocityLimit { get; }
        public float SurfaceWidth { get; }
        public float BarVisualScale { get; }
        public float TransferDurationSeconds { get; }
        public float AttachProgressInset { get; }
        public float ExitProgressInset { get; }
        public float SurfaceExitDwellSeconds { get; }
        public float SurfaceConnectionDistance { get; }
        public float SurfaceConnectionTransferDurationSeconds { get; }
        public float SurfaceConnectionCooldownSeconds { get; }
        public float TipCrossDurationSeconds { get; }
        public float ExitOppositeSidePadding { get; }
        public float TwoSidedVerticalToleranceDegrees { get; }
        public float AttachSideTolerance { get; }
        public float BarObstaclePadding { get; }
        public float EdgeBlockBackoffDistance { get; }
        public float EdgeBlockCooldownSeconds { get; }
        public float MinWalkDistance { get; }
        public float MaxWalkDistance { get; }

        public WalkableSurfaceMaster(
            int id,
            string name,
            float attachDistance,
            float detachDistance,
            float surfaceWalkSpeed,
            float rideVelocityLimit,
            float surfaceWidth,
            float barVisualScale,
            float transferDurationSeconds,
            float attachProgressInset,
            float exitProgressInset,
            float surfaceExitDwellSeconds,
            float surfaceConnectionDistance,
            float surfaceConnectionTransferDurationSeconds,
            float surfaceConnectionCooldownSeconds,
            float tipCrossDurationSeconds,
            float exitOppositeSidePadding,
            float twoSidedVerticalToleranceDegrees,
            float attachSideTolerance,
            float barObstaclePadding,
            float edgeBlockBackoffDistance,
            float edgeBlockCooldownSeconds,
            float minWalkDistance,
            float maxWalkDistance)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            AttachDistance = Mathf.Max(0.001f, attachDistance);
            DetachDistance = Mathf.Max(AttachDistance, detachDistance);
            SurfaceWalkSpeed = Mathf.Max(0.001f, surfaceWalkSpeed);
            RideVelocityLimit = Mathf.Max(0.001f, rideVelocityLimit);
            SurfaceWidth = Mathf.Max(0.001f, surfaceWidth);
            BarVisualScale = Mathf.Max(0.001f, barVisualScale);
            TransferDurationSeconds = Mathf.Max(0.001f, transferDurationSeconds);
            AttachProgressInset = Mathf.Clamp(attachProgressInset, 0f, 0.95f);
            ExitProgressInset = Mathf.Clamp(exitProgressInset, 0f, 0.95f);
            SurfaceExitDwellSeconds = Mathf.Max(0f, surfaceExitDwellSeconds);
            SurfaceConnectionDistance = Mathf.Max(0.001f, surfaceConnectionDistance);
            SurfaceConnectionTransferDurationSeconds = Mathf.Max(0.001f, surfaceConnectionTransferDurationSeconds);
            SurfaceConnectionCooldownSeconds = Mathf.Max(0f, surfaceConnectionCooldownSeconds);
            TipCrossDurationSeconds = Mathf.Max(0.001f, tipCrossDurationSeconds);
            ExitOppositeSidePadding = Mathf.Max(0f, exitOppositeSidePadding);
            TwoSidedVerticalToleranceDegrees = Mathf.Clamp(twoSidedVerticalToleranceDegrees, 0f, 90f);
            AttachSideTolerance = Mathf.Max(0f, attachSideTolerance);
            BarObstaclePadding = Mathf.Max(0f, barObstaclePadding);
            EdgeBlockBackoffDistance = Mathf.Max(0f, edgeBlockBackoffDistance);
            EdgeBlockCooldownSeconds = Mathf.Max(0f, edgeBlockCooldownSeconds);
            MinWalkDistance = Mathf.Max(0.001f, minWalkDistance);
            MaxWalkDistance = Mathf.Max(MinWalkDistance, maxWalkDistance);
        }
    }

    public sealed class AmbientObjectTypeMaster
    {
        public int Id { get; }
        public AmbientObjectKind Kind { get; }
        public string Name { get; }
        public Vector2 DefaultSize { get; }
        public Vector2 DriftVelocity { get; }
        public float ContactRadius { get; }
        public Color Color { get; }
        public int VisualEffectMasterId { get; }

        public AmbientObjectTypeMaster(
            int id,
            AmbientObjectKind kind,
            string name,
            Vector2 defaultSize,
            Vector2 driftVelocity,
            float contactRadius,
            Color color,
            int visualEffectMasterId)
        {
            Id = id;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DefaultSize = new Vector2(Mathf.Max(0.001f, defaultSize.x), Mathf.Max(0.001f, defaultSize.y));
            DriftVelocity = driftVelocity;
            ContactRadius = Mathf.Max(0.001f, contactRadius);
            Color = color;
            VisualEffectMasterId = visualEffectMasterId;
        }
    }

    public sealed class VisualEffectMaster
    {
        public int Id { get; }
        public VisualEffectKind Kind { get; }
        public VisualEffectRenderMode RenderMode { get; }
        public string Name { get; }
        public Color Color { get; }
        public float PulseSpeed { get; }
        public float Alpha { get; }
        public Vector2 DefaultSize { get; }
        public float DurationSeconds { get; }
        public string AssetKey { get; }
        public float DropSizeScale { get; }

        public VisualEffectMaster(
            int id,
            VisualEffectKind kind,
            VisualEffectRenderMode renderMode,
            string name,
            Color color,
            float pulseSpeed,
            float alpha,
            Vector2 defaultSize,
            float durationSeconds,
            string assetKey,
            float dropSizeScale = 1f)
        {
            Id = id;
            Kind = kind;
            RenderMode = renderMode;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Color = color;
            PulseSpeed = Mathf.Max(0f, pulseSpeed);
            Alpha = Mathf.Clamp01(alpha);
            DefaultSize = new Vector2(Mathf.Max(0.001f, defaultSize.x), Mathf.Max(0.001f, defaultSize.y));
            DurationSeconds = Mathf.Max(0.001f, durationSeconds);
            AssetKey = assetKey ?? string.Empty;
            DropSizeScale = Mathf.Max(0.05f, dropSizeScale);
        }
    }

    public sealed class SoundCueMaster
    {
        public int Id { get; }
        public string Name { get; }
        public string ResourcePath { get; }

        public SoundCueMaster(int id, string name, string resourcePath)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ResourcePath = resourcePath ?? string.Empty;
        }
    }

    public sealed class TuningParameterMaster
    {
        public int Id { get; }
        public string Name { get; }
        public float MaxDeltaTime { get; }
        public float WorldEdgePadding { get; }
        public float InputHitPadding { get; }
        public float HandContourReactionPadding { get; }
        public float EdgeAttachDistance { get; }
        public float ClimbSpeed { get; }
        public float BarTopDwellSeconds { get; }
        public float BarSideWalkPadding { get; }
        public float FallDuration { get; }
        public float FallLateralDistance { get; }
        public float FallLaunchDistance { get; }
        public float ClimbCooldownSeconds { get; }
        public bool BarDragBlocksClimb { get; }
        public int AmbientCloudCount { get; }
        public int AmbientStarCount { get; }
        public float RainLingerSeconds { get; }
        public float StarCooldownSeconds { get; }
        public float SurfaceReconnectCooldownSeconds { get; }

        public TuningParameterMaster(
            int id,
            string name,
            float maxDeltaTime,
            float worldEdgePadding,
            float inputHitPadding,
            float handContourReactionPadding,
            float edgeAttachDistance,
            float climbSpeed,
            float barTopDwellSeconds,
            float barSideWalkPadding,
            float fallDuration,
            float fallLateralDistance,
            float fallLaunchDistance,
            float climbCooldownSeconds,
            bool barDragBlocksClimb,
            int ambientCloudCount,
            int ambientStarCount,
            float rainLingerSeconds,
            float starCooldownSeconds,
            float surfaceReconnectCooldownSeconds)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MaxDeltaTime = Mathf.Max(0.001f, maxDeltaTime);
            WorldEdgePadding = Mathf.Clamp01(worldEdgePadding);
            InputHitPadding = Mathf.Max(0f, inputHitPadding);
            HandContourReactionPadding = Mathf.Max(0f, handContourReactionPadding);
            EdgeAttachDistance = Mathf.Max(0.001f, edgeAttachDistance);
            ClimbSpeed = Mathf.Max(0.001f, climbSpeed);
            BarTopDwellSeconds = Mathf.Max(0f, barTopDwellSeconds);
            BarSideWalkPadding = Mathf.Max(0f, barSideWalkPadding);
            FallDuration = Mathf.Max(0.001f, fallDuration);
            FallLateralDistance = Mathf.Max(0f, fallLateralDistance);
            FallLaunchDistance = Mathf.Max(0f, fallLaunchDistance);
            ClimbCooldownSeconds = Mathf.Max(0f, climbCooldownSeconds);
            BarDragBlocksClimb = barDragBlocksClimb;
            AmbientCloudCount = Math.Max(0, ambientCloudCount);
            AmbientStarCount = Math.Max(0, ambientStarCount);
            RainLingerSeconds = Mathf.Max(0.01f, rainLingerSeconds);
            StarCooldownSeconds = Mathf.Max(0.01f, starCooldownSeconds);
            SurfaceReconnectCooldownSeconds = Mathf.Max(0f, surfaceReconnectCooldownSeconds);
        }
    }

    public sealed class MasterDatabase
    {
        public MasterTable<WorldPresetMaster> WorldPresets { get; }
        public MasterTable<LittlePersonArchetypeMaster> LittlePersonArchetypes { get; }
        public MasterTable<BehaviorProfileMaster> BehaviorProfiles { get; }
        public MasterTable<ReactionMaster> Reactions { get; }
        public MasterTable<ReactionConditionMaster> ReactionConditions { get; }
        public MasterTable<InteractionObjectTypeMaster> InteractionObjectTypes { get; }
        public MasterTable<InteractionFieldMaster> InteractionFields { get; }
        public MasterTable<WalkableSurfaceMaster> WalkableSurfaces { get; }
        public MasterTable<AmbientObjectTypeMaster> AmbientObjectTypes { get; }
        public MasterTable<VisualEffectMaster> VisualEffects { get; }
        public MasterTable<SoundCueMaster> SoundCues { get; }
        public MasterTable<TuningParameterMaster> TuningParameters { get; }

        MasterDatabase(
            MasterTable<WorldPresetMaster> worldPresets,
            MasterTable<LittlePersonArchetypeMaster> littlePersonArchetypes,
            MasterTable<BehaviorProfileMaster> behaviorProfiles,
            MasterTable<ReactionMaster> reactions,
            MasterTable<ReactionConditionMaster> reactionConditions,
            MasterTable<InteractionObjectTypeMaster> interactionObjectTypes,
            MasterTable<InteractionFieldMaster> interactionFields,
            MasterTable<WalkableSurfaceMaster> walkableSurfaces,
            MasterTable<AmbientObjectTypeMaster> ambientObjectTypes,
            MasterTable<VisualEffectMaster> visualEffects,
            MasterTable<SoundCueMaster> soundCues,
            MasterTable<TuningParameterMaster> tuningParameters)
        {
            WorldPresets = worldPresets;
            LittlePersonArchetypes = littlePersonArchetypes;
            BehaviorProfiles = behaviorProfiles;
            Reactions = reactions;
            ReactionConditions = reactionConditions;
            InteractionObjectTypes = interactionObjectTypes;
            InteractionFields = interactionFields;
            WalkableSurfaces = walkableSurfaces;
            AmbientObjectTypes = ambientObjectTypes;
            VisualEffects = visualEffects;
            SoundCues = soundCues;
            TuningParameters = tuningParameters;
        }

        public InteractionObjectTypeMaster GetObjectType(InteractionObjectKind kind)
        {
            return InteractionObjectTypes.Get((int)kind);
        }

        public AmbientObjectTypeMaster GetAmbientObjectType(AmbientObjectKind kind)
        {
            return AmbientObjectTypes.Get((int)kind);
        }

        public static MasterDatabase CreateDefault()
        {
            var worldPresets = new[]
            {
                new WorldPresetMaster(1, "MVP dark table", 42, 1, 1, new Color(0.015f, 0.014f, 0.02f, 1f))
            };

            var littlePeople = new[]
            {
                new LittlePersonArchetypeMaster(1, "curious glow", new Color(0.7f, 1f, 0.86f, 1f), 0.018f, 0.13f, 0.78f, 0.35f),
                new LittlePersonArchetypeMaster(2, "shy violet", new Color(0.92f, 0.66f, 1f, 1f), 0.017f, 0.12f, 0.58f, 0.66f),
                new LittlePersonArchetypeMaster(3, "bright scout", new Color(1f, 0.95f, 0.45f, 1f), 0.016f, 0.15f, 0.9f, 0.28f)
            };

            var behaviorProfiles = new[]
            {
                new BehaviorProfileMaster(1, "soft flock", 1.8f, 7.5f, 0.035f, 0.18f)
            };

            var reactions = new[]
            {
                new ReactionMaster(1, "notice", LittlePersonBehaviorKind.Approach, LittlePersonEmotion.Curious, 1.2f),
                new ReactionMaster(2, "startle", LittlePersonBehaviorKind.Flee, LittlePersonEmotion.Startled, 1.0f),
                new ReactionMaster(3, "orbit", LittlePersonBehaviorKind.Orbit, LittlePersonEmotion.Curious, 2.0f),
                new ReactionMaster(4, "surface walk", LittlePersonBehaviorKind.SurfaceWalk, LittlePersonEmotion.Curious, 1.8f)
            };

            var reactionConditions = new[]
            {
                new ReactionConditionMaster(1, 2, InteractionObjectKind.Hand, 0.24f),
                new ReactionConditionMaster(2, 3, InteractionObjectKind.RoundProp, 0.28f),
                new ReactionConditionMaster(3, 4, InteractionObjectKind.BarProp, 0.23f)
            };

            var fields = new[]
            {
                new InteractionFieldMaster(1, InteractionFieldKind.Repeller, "hand shadow", 0.25f, 1.25f, 0.02f),
                new InteractionFieldMaster(2, InteractionFieldKind.OrbitAttractor, "round curiosity", 0.29f, 1.0f, 0.015f),
                new InteractionFieldMaster(3, InteractionFieldKind.GuideEdge, "bar edge", 0.15f, 1.05f, 0.01f),
                new InteractionFieldMaster(4, InteractionFieldKind.Attractor, "block curiosity", 0.20f, 0.75f, 0.015f)
            };

            var walkableSurfaces = new[]
            {
                new WalkableSurfaceMaster(1, "bar edge lane", 0.14f, 0.22f, 0.13f, 0.72f, 0.022f, 4.32f, 0.22f, 0.03f, 0f, 0.2f, 0.08f, 0.16f, 0.25f, 0.16f, 0.022f, 15f, 0.01f, 0.01f, 0.015f, 0.25f, 0.075f, 0.19f)
            };

            var objectTypes = new[]
            {
                new InteractionObjectTypeMaster(1, InteractionObjectKind.Hand, "hand", new Vector2(0.18f, 0.14f), 0.08f, 1, new Color(0.35f, 0.42f, 1f, 0.6f)),
                new InteractionObjectTypeMaster(2, InteractionObjectKind.RoundProp, "round prop", new Vector2(0.12f, 0.12f), 0.05f, 2, new Color(1f, 0.43f, 0.88f, 0.65f)),
                new InteractionObjectTypeMaster(3, InteractionObjectKind.BarProp, "bar prop", new Vector2(0.12f, 0.026f), 0.04f, 3, new Color(0.3f, 0.95f, 1f, 0.65f)),
                new InteractionObjectTypeMaster(4, InteractionObjectKind.BlockProp, "block prop", new Vector2(0.1f, 0.1f), 0.07f, 4, new Color(1f, 0.78f, 0.25f, 0.65f))
            };

            var ambientObjectTypes = new[]
            {
                new AmbientObjectTypeMaster(1, AmbientObjectKind.Cloud, "drifting cloud", new Vector2(0.095f, 0.05f), new Vector2(0.014f, 0.003f), 0.105f, new Color(0.86f, 0.95f, 1f, 0.78f), 4),
                new AmbientObjectTypeMaster(2, AmbientObjectKind.Star, "spark star", new Vector2(0.06f, 0.06f), new Vector2(0.01f, 0.006f), 0.085f, new Color(1f, 0.94f, 0.36f, 0.9f), 5)
            };

            var visualEffects = new[]
            {
                new VisualEffectMaster(1, VisualEffectKind.SoftGlow, VisualEffectRenderMode.Procedural, "soft cyan glow", new Color(0.3f, 0.95f, 1f, 1f), 2.4f, 0.22f, new Vector2(0.1f, 0.1f), 1.0f, string.Empty),
                new VisualEffectMaster(2, VisualEffectKind.CuriousPulse, VisualEffectRenderMode.Procedural, "curious pink pulse", new Color(1f, 0.35f, 0.9f, 1f), 3.2f, 0.28f, new Vector2(0.1f, 0.1f), 1.0f, string.Empty),
                new VisualEffectMaster(3, VisualEffectKind.StartleShadow, VisualEffectRenderMode.Procedural, "startle blue shadow", new Color(0.25f, 0.35f, 1f, 1f), 4.2f, 0.25f, new Vector2(0.1f, 0.1f), 1.0f, string.Empty),
                new VisualEffectMaster(4, VisualEffectKind.RainColumn, VisualEffectRenderMode.Procedural, "cloud rain column", new Color(0.44f, 0.84f, 1f, 1f), 4.0f, 0.72f, new Vector2(0.075f, 0.28f), 0.45f, string.Empty, 0.4f),
                new VisualEffectMaster(5, VisualEffectKind.StarBurst, VisualEffectRenderMode.Procedural, "star burst", new Color(0.9f, 0.96f, 1f, 1f), 6.2f, 0.86f, new Vector2(0.20f, 0.20f), 0.75f, string.Empty)
            };

            var soundCues = new[]
            {
                new SoundCueMaster(1, "notice chirp", string.Empty),
                new SoundCueMaster(2, "startle shimmer", string.Empty)
            };

            var tuning = new[]
            {
                new TuningParameterMaster(1, "default", 0.05f, 0.03f, 0.02f, 0.035f, 0.14f, 0.18f, 0.22f, 0.006f, 0.72f, 0.14f, 0.08f, 1.1f, true, 2, 2, 10.0f, 1.4f, 1.1f)
            };

            return new MasterDatabase(
                new MasterTable<WorldPresetMaster>(worldPresets, x => x.Id),
                new MasterTable<LittlePersonArchetypeMaster>(littlePeople, x => x.Id),
                new MasterTable<BehaviorProfileMaster>(behaviorProfiles, x => x.Id),
                new MasterTable<ReactionMaster>(reactions, x => x.Id),
                new MasterTable<ReactionConditionMaster>(reactionConditions, x => x.Id),
                new MasterTable<InteractionObjectTypeMaster>(objectTypes, x => x.Id),
                new MasterTable<InteractionFieldMaster>(fields, x => x.Id),
                new MasterTable<WalkableSurfaceMaster>(walkableSurfaces, x => x.Id),
                new MasterTable<AmbientObjectTypeMaster>(ambientObjectTypes, x => x.Id),
                new MasterTable<VisualEffectMaster>(visualEffects, x => x.Id),
                new MasterTable<SoundCueMaster>(soundCues, x => x.Id),
                new MasterTable<TuningParameterMaster>(tuning, x => x.Id));
        }
    }
}
