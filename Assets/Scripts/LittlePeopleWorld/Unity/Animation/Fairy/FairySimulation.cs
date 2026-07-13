using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using LittlePeopleWorld.Unity.Animation.Plants;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Fairy
{
    internal sealed class FairySimulation
    {
        sealed class FairyState
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float SpeedScale;
            public Vector2 Jitter;
            public float JitterTimer;
            public Color Color;
            public FairyView View;
            public bool IsClimbing;
            public bool IsBloomAttached;
            public PlantModel AttachedPlant;
            public float BurstFreeTimer;
            public float ReattachCooldown;
        }

        readonly List<FairyState> fairies = new();
        readonly Dictionary<int, float> cloudCooldowns = new();
        readonly FairyRenderer renderer;
        RecognitionMask mask;
        PlantSystem plants;
        FairySettings settings;
        PlantSettings plantSettings;
        NormalizedScreenMapper mapper;

        public FairySimulation(FairyRenderer renderer)
        {
            this.renderer = renderer;
        }

        public int FlowerBurstSequence { get; private set; }

        public void Configure(FairySettings fairySettings, PlantSettings nextPlantSettings, RecognitionMask recognitionMask, PlantSystem plantSystem, NormalizedScreenMapper screenMapper)
        {
            settings = fairySettings;
            plantSettings = nextPlantSettings;
            mask = recognitionMask;
            plants = plantSystem;
            mapper = screenMapper;
            EnsureCount();
        }

        public void Advance(IReadOnlyList<InteractionObject> interactionObjects, float deltaTime)
        {
            HandleFlowerBurst(interactionObjects);
            var separation = new Vector2[fairies.Count];
            ComputeSeparation(separation, deltaTime);
            for (var i = 0; i < fairies.Count; i++)
            {
                var fairy = fairies[i];
                UpdateJitter(fairy, deltaTime);
                UpdateFairy(fairy, separation[i], deltaTime);
                renderer.Render(fairy.View, mask.PixelToWorld(fairy.Position, mapper), fairy.Velocity, fairy.Color, i, settings);
            }
        }

        public void TriggerCloudRain(World world, MasterDatabase masters, float deltaTime)
        {
            AdvanceCloudCooldowns(deltaTime);
            if (!settings.EnableCloudRain || fairies.Count == 0) return;
            var tuning = masters.TuningParameters.Get(1);
            var liveClouds = new HashSet<int>();
            foreach (var ambient in world.AmbientObjects)
            {
                if (ambient.Kind != AmbientObjectKind.Cloud) continue;
                liveClouds.Add(ambient.Id);
                if (cloudCooldowns.TryGetValue(ambient.Id, out var cooldown) && cooldown > 0f) continue;
                if (!TouchesCloud(ambient)) continue;
                if (world.MarkCloudTouchedByExternalSource(ambient.Id, tuning.RainLingerSeconds))
                    cloudCooldowns[ambient.Id] = Mathf.Max(0.01f, settings.CloudRainCooldownSeconds);
            }
            var dead = new List<int>();
            foreach (var key in cloudCooldowns.Keys) if (!liveClouds.Contains(key)) dead.Add(key);
            foreach (var key in dead) cloudCooldowns.Remove(key);
        }

        void EnsureCount()
        {
            var target = Mathf.Clamp(settings.Count, 0, 2000);
            while (fairies.Count < target)
            {
                var palette = settings.Palette is { Length: > 0 } ? settings.Palette : new[] { Color.white };
                var color = palette[Random.Range(0, palette.Length)];
                fairies.Add(new FairyState
                {
                    Position = new Vector2(Random.Range(0f, mask.Width), Random.Range(0f, mask.Height)),
                    Velocity = Random.insideUnitCircle.normalized * settings.SpeedPxPerSec,
                    SpeedScale = Random.Range(0.75f, 1.35f),
                    Jitter = Random.insideUnitCircle * settings.TargetJitterPx,
                    JitterTimer = Random.Range(0f, 2f),
                    Color = color,
                    View = renderer.Create(fairies.Count, color, settings)
                });
            }
            while (fairies.Count > target)
            {
                var last = fairies[^1];
                renderer.Destroy(last.View);
                fairies.RemoveAt(fairies.Count - 1);
            }
        }

        void HandleFlowerBurst(IReadOnlyList<InteractionObject> objects)
        {
            foreach (var plant in plants.Plants)
            {
                if (!plant.IsBloomable)
                {
                    plant.HandTouchingBloom = false;
                    continue;
                }
                var touching = IsHandNear(objects, plant.BloomPosition, Mathf.Max(1f, plantSettings.FlowerSizePx * plantSettings.FlowerBurstTouchRadiusRatio));
                if (touching && !plant.HandTouchingBloom) Burst(plant);
                plant.HandTouchingBloom = touching;
            }
        }

        bool IsHandNear(IReadOnlyList<InteractionObject> objects, Vector2 bloom, float radius)
        {
            if (objects == null) return false;
            foreach (var interactionObject in objects)
            {
                if (interactionObject == null || interactionObject.Kind != InteractionObjectKind.Hand || interactionObject.ShapeKind != InteractionShapeKind.Contour || interactionObject.ContourPoints.Count < 3) continue;
                var polygon = new Vector2[interactionObject.ContourPoints.Count];
                for (var i = 0; i < polygon.Length; i++) polygon[i] = mask.NormalizedToPixel(interactionObject.ContourPoints[i]);
                if (RecognitionMask.DistancePointToPolygon(polygon, bloom) <= radius) return true;
            }
            return false;
        }

        void Burst(PlantModel plant)
        {
            var count = 0;
            foreach (var fairy in fairies)
            {
                if (!fairy.IsBloomAttached || fairy.AttachedPlant != plant) continue;
                var direction = fairy.Position - plant.BloomPosition;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;
                fairy.Velocity = direction * settings.BurstInitialSpeedPxPerSec;
                fairy.IsBloomAttached = false;
                fairy.IsClimbing = false;
                fairy.AttachedPlant = null;
                fairy.BurstFreeTimer = settings.BurstFreeSeconds;
                fairy.ReattachCooldown = settings.BurstReattachCooldownSeconds;
                count++;
            }
            if (count > 0) FlowerBurstSequence++;
        }

        void UpdateFairy(FairyState fairy, Vector2 separation, float dt)
        {
            fairy.ReattachCooldown = Mathf.Max(0f, fairy.ReattachCooldown - dt);
            if (fairy.BurstFreeTimer > 0f)
            {
                fairy.BurstFreeTimer = Mathf.Max(0f, fairy.BurstFreeTimer - dt);
                fairy.Velocity += separation;
                fairy.Position += fairy.Velocity * dt;
                BounceAndClamp(fairy);
                return;
            }

            if (fairy.IsBloomAttached && fairy.AttachedPlant?.IsBloomable == true)
            {
                var toCenter = fairy.AttachedPlant.BloomPosition - fairy.Position;
                fairy.Velocity = (toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized * plantSettings.BloomAttractForce * dt : Vector2.zero) + separation;
                fairy.Position += fairy.Velocity * dt;
                Clamp(fairy);
                if (Vector2.Distance(fairy.Position, fairy.AttachedPlant.BloomPosition) > Mathf.Max(4f, fairy.AttachedPlant.HeightPx * plantSettings.BloomSphereRadiusRatio))
                {
                    fairy.IsBloomAttached = false;
                    fairy.AttachedPlant = null;
                }
                return;
            }

            if (fairy.IsBloomAttached)
            {
                fairy.IsBloomAttached = false;
                fairy.AttachedPlant = null;
            }

            var plant = fairy.IsClimbing && fairy.AttachedPlant?.CurrentStage != PlantStage.Dead
                ? fairy.AttachedPlant
                : FindNearestPlant(fairy.Position);
            fairy.IsClimbing = false;
            Vector2 desired;
            if (plant != null)
            {
                fairy.AttachedPlant = plant;
                var distanceToStem = RecognitionMask.DistancePointToSegment(fairy.Position, plant.Position, plant.BloomPosition);
                if (distanceToStem <= Mathf.Max(4f, plant.HeightPx * plantSettings.ClimbAttractRadiusRatio))
                {
                    desired = (plant.BloomPosition - fairy.Position).normalized;
                    fairy.IsClimbing = true;
                }
                else desired = (plant.Position - fairy.Position).normalized;
            }
            else
            {
                fairy.AttachedPlant = null;
                desired = mask.EffectiveWhiteCount == 0 ? EdgeSteer(fairy) : MaskSteer(fairy);
            }

            var combined = desired.normalized + separation;
            if (combined.sqrMagnitude < 0.0001f) combined = fairy.Velocity.sqrMagnitude > 0.0001f ? fairy.Velocity.normalized : Random.insideUnitCircle;
            var boost = fairy.IsClimbing ? Mathf.Max(1f, plantSettings.ClimbSpeedMultiplier) : 1f;
            var targetVelocity = combined.normalized * settings.SpeedPxPerSec * fairy.SpeedScale * boost;
            fairy.Velocity = Vector2.Lerp(fairy.Velocity, targetVelocity, 1f - Mathf.Exp(-settings.SteerLerp * boost * dt));
            if (fairy.IsClimbing) fairy.Velocity.y = Mathf.Max(fairy.Velocity.y, 0f);
            fairy.Position += fairy.Velocity * dt;
            Clamp(fairy);
            if (fairy.IsClimbing && plant?.IsBloomable == true && fairy.ReattachCooldown <= 0f &&
                Vector2.Distance(fairy.Position, plant.BloomPosition) <= Mathf.Max(4f, plant.HeightPx * plantSettings.BloomAttractRadiusRatio))
            {
                fairy.IsBloomAttached = true;
                fairy.IsClimbing = false;
            }
        }

        PlantModel FindNearestPlant(Vector2 position)
        {
            PlantModel best = null;
            var distance = float.MaxValue;
            foreach (var plant in plants.Plants)
            {
                if (plant.CurrentStage == PlantStage.Dead) continue;
                var current = Vector2.Distance(position, plant.Position);
                if (current > Mathf.Max(6f, plant.HeightPx * plantSettings.InfluenceRadiusRatio) || current >= distance) continue;
                best = plant;
                distance = current;
            }
            return best;
        }

        Vector2 MaskSteer(FairyState fairy)
        {
            var fieldValue = mask.SampleField(fairy.Position);
            if (fieldValue < settings.SenseThreshold)
            {
                var target = mask.FindNearestCluster(fairy.Position, fairy.Jitter);
                var toTarget = target - fairy.Position;
                var attractRadius = Mathf.Min(
                    settings.PenSenseRadiusPx,
                    settings.MinPenAttractRadiusPx + Mathf.Sqrt(mask.EffectiveWhiteCount) * settings.PenAttractRadiusPerSqrtPixel);
                return toTarget.sqrMagnitude > 0.001f && toTarget.sqrMagnitude <= attractRadius * attractRadius
                    ? toTarget : EdgeSteer(fairy);
            }
            var gradient = mask.FieldGradient(fairy.Position);
            if (gradient.sqrMagnitude < 0.00000001f) return fairy.Velocity;
            var normal = gradient.normalized;
            return new Vector2(-normal.y, normal.x) + normal * ((settings.ContourLevel - fieldValue) * settings.CorrectionGain);
        }

        Vector2 EdgeSteer(FairyState fairy)
        {
            var minX = settings.EdgeMarginPx;
            var maxX = mask.Width - settings.EdgeMarginPx;
            var minY = settings.EdgeMarginPx;
            var maxY = mask.Height - settings.EdgeMarginPx;
            var p = fairy.Position;
            var distances = new[] { p.y - minY, maxX - p.x, maxY - p.y, p.x - minX };
            var side = 0;
            for (var i = 1; i < 4; i++) if (distances[i] < distances[side]) side = i;
            Vector2 nearest;
            Vector2 tangent;
            switch (side)
            {
                case 0: nearest = new Vector2(Mathf.Clamp(p.x, minX, maxX), minY); tangent = Vector2.right; break;
                case 1: nearest = new Vector2(maxX, Mathf.Clamp(p.y, minY, maxY)); tangent = Vector2.up; break;
                case 2: nearest = new Vector2(Mathf.Clamp(p.x, minX, maxX), maxY); tangent = Vector2.left; break;
                default: nearest = new Vector2(minX, Mathf.Clamp(p.y, minY, maxY)); tangent = Vector2.down; break;
            }
            return tangent + (nearest + fairy.Jitter - p) * settings.EdgePullGain;
        }

        void ComputeSeparation(Vector2[] forces, float dt)
        {
            var radiusSqr = settings.SeparationRadiusPx * settings.SeparationRadiusPx;
            for (var i = 0; i < fairies.Count; i++)
            {
                var total = Vector2.zero;
                for (var j = 0; j < fairies.Count; j++)
                {
                    if (i == j) continue;
                    var difference = fairies[i].Position - fairies[j].Position;
                    if (difference.sqrMagnitude > radiusSqr || difference.sqrMagnitude < 0.0001f) continue;
                    var distance = difference.magnitude;
                    total += difference / distance * (1f - distance / settings.SeparationRadiusPx);
                }
                forces[i] = total * settings.SeparationGain * dt;
            }
        }

        void UpdateJitter(FairyState fairy, float dt)
        {
            fairy.JitterTimer -= dt;
            if (fairy.JitterTimer > 0f) return;
            fairy.Jitter = Random.insideUnitCircle * settings.TargetJitterPx;
            fairy.JitterTimer = Random.Range(1.2f, 2.6f);
        }

        void BounceAndClamp(FairyState fairy)
        {
            if (fairy.Position.x < 1f || fairy.Position.x > mask.Width - 2f) fairy.Velocity.x *= -0.6f;
            if (fairy.Position.y < 1f || fairy.Position.y > mask.Height - 2f) fairy.Velocity.y *= -0.6f;
            Clamp(fairy);
        }

        void Clamp(FairyState fairy)
        {
            fairy.Position.x = Mathf.Clamp(fairy.Position.x, 1f, mask.Width - 2f);
            fairy.Position.y = Mathf.Clamp(fairy.Position.y, 1f, mask.Height - 2f);
        }

        bool TouchesCloud(AmbientObject cloud)
        {
            var radiusSqr = (cloud.ContactRadius + Mathf.Max(0f, settings.CloudTouchRadius));
            radiusSqr *= radiusSqr;
            foreach (var fairy in fairies)
                if ((mask.PixelToNormalized(fairy.Position) - cloud.Position).sqrMagnitude <= radiusSqr) return true;
            return false;
        }

        void AdvanceCloudCooldowns(float dt)
        {
            var keys = new List<int>(cloudCooldowns.Keys);
            foreach (var key in keys) cloudCooldowns[key] = Mathf.Max(0f, cloudCooldowns[key] - dt);
        }
    }
}
