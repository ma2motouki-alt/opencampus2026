using System;
using System.Collections.Generic;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Domain
{
    public sealed class World
    {
        readonly List<LittlePerson> littlePeople = new();
        readonly List<InteractionObject> interactionObjects = new();
        readonly List<InteractionField> interactionFields = new();
        readonly List<WalkableSurface> walkableSurfaces = new();
        readonly List<AmbientObject> ambientObjects = new();
        readonly List<VisualEffectInstance> visualEffects = new();
        readonly List<RainbowInstance> rainbows = new();
        readonly HashSet<Guid> movementPausedLittlePersonIds = new();
        int nextVisualEffectId = 1;
        int nextRainbowId = 1;
        int rainbowSpawnSequence;
        int nextDevelopmentRainSourceId = -100000;
        int activeBloomCount;
        bool rainbowConditionLatched;
        float rainbowCooldownSeconds;

        public IReadOnlyList<LittlePerson> LittlePeople => littlePeople;
        public IReadOnlyList<InteractionObject> InteractionObjects => interactionObjects;
        public IReadOnlyList<InteractionField> InteractionFields => interactionFields;
        public IReadOnlyList<WalkableSurface> WalkableSurfaces => walkableSurfaces;
        public IReadOnlyList<AmbientObject> AmbientObjects => ambientObjects;
        public IReadOnlyList<VisualEffectInstance> VisualEffects => visualEffects;
        public IReadOnlyList<RainbowInstance> Rainbows => rainbows;
        public int RainbowSpawnSequence => rainbowSpawnSequence;

        public void SetMovementPausedLittlePeople(IReadOnlyCollection<Guid> personIds)
        {
            movementPausedLittlePersonIds.Clear();
            if (personIds == null)
            {
                return;
            }

            foreach (var personId in personIds)
            {
                movementPausedLittlePersonIds.Add(personId);
            }
        }
        public void SetActiveBloomCount(int count)
        {
            activeBloomCount = Math.Max(0, count);
        }


        public static World Create(MasterDatabase masters, int worldPresetId)
        {
            var preset = masters.WorldPresets.Get(worldPresetId);
            var tuning = masters.TuningParameters.Get(1);
            var world = new World();
            var random = new System.Random(1367);

            for (var i = 0; i < preset.InitialLittlePersonCount; i++)
            {
                var archetypeId = 1 + i % 4;
                var edgeProgress = (float)random.NextDouble();
                var edgeDirection = random.NextDouble() < 0.5 ? -1 : 1;
                world.littlePeople.Add(new LittlePerson(
                    Guid.NewGuid(),
                    archetypeId,
                    preset.DefaultBehaviorProfileId,
                    random.Next(),
                    edgeProgress,
                    edgeDirection,
                    tuning.WorldEdgePadding));
            }

            world.CreateAmbientObjects(masters, tuning);
            return world;
        }

        public void SetInteractionObjects(IReadOnlyList<InteractionObject> objects, MasterDatabase masters)
        {
            interactionObjects.Clear();
            interactionFields.Clear();
            walkableSurfaces.Clear();

            if (objects != null)
            {
                foreach (var interactionObject in objects)
                {
                    interactionObjects.Add(interactionObject);
                    interactionFields.Add(interactionObject.CreateField(masters));
                }
            }

            RebuildRainbowSurfaces(masters);
        }

        public bool TriggerDevelopmentRainbow(MasterDatabase masters)
        {
            if (masters == null || rainbows.Count > 0)
            {
                return false;
            }

            AmbientObject sun = null;
            AmbientObject selectedCloud = null;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind == AmbientObjectKind.Star)
                {
                    sun = ambientObject;
                    break;
                }
            }

            if (sun == null)
            {
                return false;
            }

            var bestDistance = -1f;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind != AmbientObjectKind.Cloud)
                {
                    continue;
                }

                var distance = Vector2.Distance(ambientObject.Position, sun.Position);
                if (distance > bestDistance)
                {
                    selectedCloud = ambientObject;
                    bestDistance = distance;
                }
            }

            if (selectedCloud == null)
            {
                return false;
            }

            var tuning = masters.TuningParameters.Get(1);
            rainbows.Add(new RainbowInstance(
                nextRainbowId++,
                selectedCloud.Id,
                selectedCloud.Position,
                sun.Position,
                tuning.WorldEdgePadding,
                masters.Rainbows.Get(1)));
            rainbowSpawnSequence++;
            rainbowConditionLatched = true;
            rainbowCooldownSeconds = 0f;
            RebuildRainbowSurfaces(masters);
            return true;
        }

        public void TriggerDevelopmentRain(MasterDatabase masters, Vector2 position, float width, float durationSeconds)
        {
            if (masters == null)
            {
                return;
            }

            var effectMaster = masters.VisualEffects.Get((int)VisualEffectKind.RainColumn);
            var rainPosition = Clamp01(position);
            var heightToGround = Mathf.Max(0.05f, 1f - rainPosition.y);
            var size = new Vector2(Mathf.Max(effectMaster.DefaultSize.x, width), heightToGround);
            RefreshOrCreateVisualEffect(
                effectMaster.Id,
                nextDevelopmentRainSourceId--,
                rainPosition,
                size,
                0f,
                Mathf.Max(0.05f, durationSeconds));
        }

        public bool MarkCloudTouchedByExternalSource(int ambientObjectId, float lingerSeconds)
        {
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Id == ambientObjectId && ambientObject.Kind == AmbientObjectKind.Cloud)
                {
                    ambientObject.MarkCloudTouched(lingerSeconds);
                    return true;
                }
            }

            return false;
        }

        public void Advance(float deltaTime, MasterDatabase masters)
        {
            var tuning = masters.TuningParameters.Get(1);
            deltaTime = Mathf.Min(Mathf.Max(0f, deltaTime), tuning.MaxDeltaTime);

            foreach (var person in littlePeople)
            {
                if (movementPausedLittlePersonIds.Contains(person.Id) &&
                    person.CurrentBehavior == LittlePersonBehaviorKind.EdgeWalk)
                {
                    person.HoldPosition(deltaTime);
                    continue;
                }

                person.Advance(deltaTime, interactionFields, walkableSurfaces, littlePeople, masters, tuning);
            }

            foreach (var ambientObject in ambientObjects)
            {
                ambientObject.Advance(deltaTime);
            }

            UpdateAmbientReactions(masters, tuning);
            AdvanceVisualEffects(deltaTime);
            AdvanceRainbows(deltaTime, masters);
            UpdateRainbowTrigger(masters, tuning);
            RebuildRainbowSurfaces(masters);
        }
        void AdvanceRainbows(float deltaTime, MasterDatabase masters)
        {
            var rainbowMaster = masters.Rainbows.Get(1);
            rainbowCooldownSeconds = Mathf.Max(0f, rainbowCooldownSeconds - Mathf.Max(0f, deltaTime));

            for (var i = rainbows.Count - 1; i >= 0; i--)
            {
                rainbows[i].Advance(deltaTime);
                if (!rainbows[i].IsExpired)
                {
                    continue;
                }

                rainbows.RemoveAt(i);
                rainbowCooldownSeconds = Mathf.Max(rainbowCooldownSeconds, rainbowMaster.CooldownSeconds);
            }
        }

        void UpdateRainbowTrigger(MasterDatabase masters, TuningParameterMaster tuning)
        {
            var rainbowMaster = masters.Rainbows.Get(1);
            var hasDistantRainingCloud =
                TryFindDistantRainingCloud(rainbowMaster, out var sourceCloud, out var sun);
            var conditionMet = activeBloomCount >= rainbowMaster.RequiredBloomCount && hasDistantRainingCloud;

            if (!conditionMet)
            {
                rainbowConditionLatched = false;
                return;
            }

            if (rainbowConditionLatched || rainbowCooldownSeconds > 0f || rainbows.Count > 0)
            {
                return;
            }

            rainbows.Add(new RainbowInstance(
                nextRainbowId++,
                sourceCloud.Id,
                sourceCloud.Position,
                sun.Position,
                tuning.WorldEdgePadding,
                rainbowMaster));
            rainbowSpawnSequence++;
            rainbowConditionLatched = true;
        }

        bool TryFindDistantRainingCloud(
            RainbowMaster master,
            out AmbientObject selectedCloud,
            out AmbientObject sun)
        {
            selectedCloud = null;
            sun = null;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind == AmbientObjectKind.Star)
                {
                    sun = ambientObject;
                    break;
                }
            }

            if (sun == null)
            {
                return false;
            }

            var selectedDistance = master.MinCloudSunDistance;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind != AmbientObjectKind.Cloud || !ambientObject.IsReacting)
                {
                    continue;
                }

                var distance = Vector2.Distance(ambientObject.Position, sun.Position);
                if (distance < selectedDistance)
                {
                    continue;
                }

                selectedCloud = ambientObject;
                selectedDistance = distance;
            }

            return selectedCloud != null;
        }

        void RebuildRainbowSurfaces(MasterDatabase masters)
        {
            for (var i = walkableSurfaces.Count - 1; i >= 0; i--)
            {
                if (walkableSurfaces[i].Kind == WalkableSurfaceKind.Rainbow)
                {
                    walkableSurfaces.RemoveAt(i);
                }
            }

            var rainbowMaster = masters.Rainbows.Get(1);
            foreach (var rainbow in rainbows)
            {
                rainbow.AddWalkableSurfaces(walkableSurfaces, rainbowMaster);
            }
        }


        void CreateAmbientObjects(MasterDatabase masters, TuningParameterMaster tuning)
        {
            var nextId = 1;
            var cloudType = masters.GetAmbientObjectType(AmbientObjectKind.Cloud);
            for (var i = 0; i < tuning.AmbientCloudCount; i++)
            {
                ambientObjects.Add(new AmbientObject(
                    nextId++,
                    AmbientObjectKind.Cloud,
                    AmbientSpawnPosition(AmbientObjectKind.Cloud, i),
                    cloudType.DefaultSize,
                    AmbientVelocity(cloudType.DriftVelocity, i),
                    cloudType.ContactRadius,
                    cloudType.MovementEdgePadding,
                    cloudType.MaxCenterY));
            }

            var starType = masters.GetAmbientObjectType(AmbientObjectKind.Star);
            for (var i = 0; i < tuning.AmbientStarCount; i++)
            {
                ambientObjects.Add(new AmbientObject(
                    nextId++,
                    AmbientObjectKind.Star,
                    AmbientSpawnPosition(AmbientObjectKind.Star, i),
                    starType.DefaultSize,
                    AmbientVelocity(starType.DriftVelocity, i),
                    starType.ContactRadius,
                    starType.MovementEdgePadding,
                    starType.MaxCenterY));
            }
        }

        void UpdateAmbientReactions(MasterDatabase masters, TuningParameterMaster tuning)
        {
            foreach (var ambientObject in ambientObjects)
            {
                var touched = IsTouchedByAnyLittlePerson(ambientObject);
                var type = masters.GetAmbientObjectType(ambientObject.Kind);
                var effectMaster = masters.VisualEffects.Get(type.VisualEffectMasterId);

                if (ambientObject.Kind == AmbientObjectKind.Cloud)
                {
                    if (touched)
                    {
                        ambientObject.MarkCloudTouched(tuning.RainLingerSeconds);
                    }

                    if (ambientObject.IsReacting)
                    {
                        RefreshOrCreateVisualEffect(
                            effectMaster.Id,
                            ambientObject.Id,
                            RainPosition(ambientObject),
                            EffectSize(ambientObject, effectMaster),
                            0f,
                            Mathf.Max(0.12f, ambientObject.ReactionSeconds));
                    }
                }
                else if (ambientObject.Kind == AmbientObjectKind.Star &&
                         touched &&
                         ambientObject.TryTriggerStar(effectMaster.DurationSeconds, tuning.StarCooldownSeconds))
                {
                    RefreshOrCreateVisualEffect(
                        effectMaster.Id,
                        ambientObject.Id,
                        ambientObject.Position,
                        EffectSize(ambientObject, effectMaster),
                        0f,
                        effectMaster.DurationSeconds);
                }
            }
        }

        bool IsTouchedByAnyLittlePerson(AmbientObject ambientObject)
        {
            foreach (var person in littlePeople)
            {
                if (ambientObject.IsTouchedBy(person))
                {
                    return true;
                }
            }

            return false;
        }

        void RefreshOrCreateVisualEffect(
            int visualEffectMasterId,
            int sourceObjectId,
            Vector2 position,
            Vector2 size,
            float angleDegrees,
            float durationSeconds)
        {
            foreach (var visualEffect in visualEffects)
            {
                if (visualEffect.VisualEffectMasterId == visualEffectMasterId &&
                    visualEffect.SourceObjectId == sourceObjectId)
                {
                    visualEffect.Refresh(position, size, angleDegrees, durationSeconds);
                    return;
                }
            }

            visualEffects.Add(new VisualEffectInstance(
                nextVisualEffectId++,
                visualEffectMasterId,
                sourceObjectId,
                position,
                size,
                angleDegrees,
                durationSeconds));
        }

        void AdvanceVisualEffects(float deltaTime)
        {
            for (var i = visualEffects.Count - 1; i >= 0; i--)
            {
                visualEffects[i].Advance(deltaTime);
                if (visualEffects[i].IsExpired)
                {
                    visualEffects.RemoveAt(i);
                }
            }
        }

        static Vector2 RainPosition(AmbientObject ambientObject)
        {
            return Clamp01(ambientObject.Position + new Vector2(0f, ambientObject.Size.y * 0.45f));
        }

        static Vector2 EffectSize(AmbientObject ambientObject, VisualEffectMaster effectMaster)
        {
            if (effectMaster.Kind == VisualEffectKind.RainColumn)
            {
                // 雨の発生位置(RainPositionと同じ基準点)から画面下端(正規化y=1.0)までの
                // 距離を雨柱の高さにする。固定値(旧: effectMaster.DefaultSize.y)だと、
                // 雲の高さに関わらず常に同じ短い範囲でループしてしまい、地面まで届かなかった。
                var originY = RainPosition(ambientObject).y;
                var heightToGround = Mathf.Max(0.05f, 1f - originY);

                return new Vector2(
                    Mathf.Max(effectMaster.DefaultSize.x, ambientObject.Size.x * 0.75f),
                    heightToGround);
            }

            return effectMaster.DefaultSize;
        }

        static Vector2 AmbientSpawnPosition(AmbientObjectKind kind, int index)
        {
            if (kind == AmbientObjectKind.Cloud)
            {
                switch (index % 3)
                {
                    case 0:
                        return new Vector2(0.30f, 0.24f);
                    case 1:
                        return new Vector2(0.70f, 0.31f);
                    default:
                        return new Vector2(0.50f, 0.27f);
                }
            }

            return new Vector2(0.84f, 0.12f);
        }

        static Vector2 AmbientVelocity(Vector2 baseVelocity, int index)
        {
            var xSign = index % 2 == 0 ? 1f : -1f;
            var ySign = index % 3 == 0 ? 1f : -1f;
            return new Vector2(baseVelocity.x * xSign, baseVelocity.y * ySign);
        }

        static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }
    }
}
