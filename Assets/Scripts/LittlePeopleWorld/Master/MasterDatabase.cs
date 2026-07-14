using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class MasterDatabase
    {
        public MasterTable<WorldPresetMaster> WorldPresets { get; }
        public MasterTable<LittlePersonArchetypeMaster> LittlePersonArchetypes { get; }
        public MasterTable<BehaviorProfileMaster> BehaviorProfiles { get; }
        public MasterTable<ReactionMaster> Reactions { get; }
        public MasterTable<ReactionConditionMaster> ReactionConditions { get; }
        public MasterTable<InteractionObjectTypeMaster> InteractionObjectTypes { get; }
        public MasterTable<InteractionFieldMaster> InteractionFields { get; }
        public MasterTable<RainbowMaster> Rainbows { get; }
        public MasterTable<RainbowCloudJumpMaster> RainbowCloudJumps { get; }
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
            MasterTable<RainbowMaster> rainbows,
            MasterTable<RainbowCloudJumpMaster> rainbowCloudJumps,
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
            Rainbows = rainbows;
            RainbowCloudJumps = rainbowCloudJumps;
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
                //小人の数を調整
                new WorldPresetMaster(1, "MVP dark table", 20, 1, 1, new Color(0.015f, 0.014f, 0.02f, 1f))
            };

            var littlePeople = new[]
            {
                //key, name, color, size, speed, curiosity, startle
                new LittlePersonArchetypeMaster(1, "blue walker", new Color(0.45f, 0.76f, 1f, 1f), 0.018f, 0.12f, 0.78f, 0.35f),
                new LittlePersonArchetypeMaster(2, "green walker", new Color(0.55f, 1f, 0.72f, 1f), 0.017f, 0.11f, 0.58f, 0.66f),
                new LittlePersonArchetypeMaster(3, "red walker", new Color(1f, 0.48f, 0.45f, 1f), 0.025f, 0.18f, 0.9f, 0.28f),
                new LittlePersonArchetypeMaster(4, "yellow walker", new Color(1f, 0.95f, 0.45f, 1f), 0.017f, 0.125f, 0.72f, 0.42f)
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
                new ReactionConditionMaster(2, 3, InteractionObjectKind.RoundProp, 0.28f)
            };

            var fields = new[]
            {
                new InteractionFieldMaster(1, InteractionFieldKind.Repeller, "hand shadow", 0.25f, 1.25f, 0.02f),
                new InteractionFieldMaster(2, InteractionFieldKind.OrbitAttractor, "round curiosity", 0.29f, 1.0f, 0.015f),
                new InteractionFieldMaster(3, InteractionFieldKind.Shadow, "development mask stroke", 0.15f, 0f, 0.01f),
                new InteractionFieldMaster(4, InteractionFieldKind.Attractor, "block curiosity", 0.20f, 0.75f, 0.015f)
            };

            var rainbows = new[]
            {
                //虹の見た目を調整0.80f,0.55f(幅、高さ)、32(分割数)、0.055f(虹の幅)、0.13f(虹の高さ)、0.018f(虹の透明度)、0.02f(虹のぼかし)、0.22f(虹の色相)、0.2f(虹の彩度)、0.22f(虹の明度)、1.1f(虹の輝度)
                new RainbowMaster(1, "rare rain-and-bloom rainbow", 4, 0.50f, 20f, 1.0f, 2.0f, 30f, 0.80f, 0.55f, 32, 0.055f, 0.13f, 0.018f, 0.02f, 0.22f, 0.2f, 0.22f, 1.1f)
            };

            var rainbowCloudJumps = new[]
            {
                new RainbowCloudJumpMaster(
                    1,
                    "rainbow to cloud jump",
                    0.16f,
                    0.03f,
                    0.35f,
                    0.025f,
                    0.07f,
                    0.35f,
                    0.80f,
                    0.03f,//雲の接触時間
                    0.025f,
                    0.30f,
                    0.70f,
                    1.5f,
                    1,
                    false)
            };

            var objectTypes = new[]
            {
                new InteractionObjectTypeMaster(1, InteractionObjectKind.Hand, "hand", new Vector2(0.18f, 0.14f), 0.08f, 1, new Color(0.35f, 0.42f, 1f, 0.6f)),
                new InteractionObjectTypeMaster(2, InteractionObjectKind.RoundProp, "round prop", new Vector2(0.12f, 0.12f), 0.05f, 2, new Color(1f, 0.43f, 0.88f, 0.65f)),
                new InteractionObjectTypeMaster(3, InteractionObjectKind.MaskStroke, "development mask stroke", new Vector2(0.12f, 0.026f), 0.04f, 3, new Color(0.3f, 0.95f, 1f, 0.65f)),
                new InteractionObjectTypeMaster(4, InteractionObjectKind.BlockProp, "block prop", new Vector2(0.1f, 0.1f), 0.07f, 4, new Color(1f, 0.78f, 0.25f, 0.65f))
            };

            var ambientObjectTypes = new[]
            {
                new AmbientObjectTypeMaster(1, AmbientObjectKind.Cloud, "drifting cloud", new Vector2(0.095f, 0.05f), new Vector2(0.014f, 0.003f), 0.075f, new Color(0.86f, 0.95f, 1f, 0.78f), 4, 0.18f, 0.38f),
                new AmbientObjectTypeMaster(2, AmbientObjectKind.Star, "fixed sun", new Vector2(0.08f, 0.08f), Vector2.zero, 0.085f, new Color(1f, 0.84f, 0.20f, 0.95f), 5)
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
//2.0fは雨の継続時間
            var tuning = new[]
            {
                new TuningParameterMaster(1, "default", 0.05f, 0.03f, 0.02f, 0.035f, 0.72f, 0.14f, 0.08f, 3, 1, 2.0f, 1.4f, 1.1f)
            };

            return new MasterDatabase(
                new MasterTable<WorldPresetMaster>(worldPresets, x => x.Id),
                new MasterTable<LittlePersonArchetypeMaster>(littlePeople, x => x.Id),
                new MasterTable<BehaviorProfileMaster>(behaviorProfiles, x => x.Id),
                new MasterTable<ReactionMaster>(reactions, x => x.Id),
                new MasterTable<ReactionConditionMaster>(reactionConditions, x => x.Id),
                new MasterTable<InteractionObjectTypeMaster>(objectTypes, x => x.Id),
                new MasterTable<InteractionFieldMaster>(fields, x => x.Id),
                new MasterTable<RainbowMaster>(rainbows, x => x.Id),
                new MasterTable<RainbowCloudJumpMaster>(rainbowCloudJumps, x => x.Id),
                new MasterTable<AmbientObjectTypeMaster>(ambientObjectTypes, x => x.Id),
                new MasterTable<VisualEffectMaster>(visualEffects, x => x.Id),
                new MasterTable<SoundCueMaster>(soundCues, x => x.Id),
                new MasterTable<TuningParameterMaster>(tuning, x => x.Id));
        }
    }
}
