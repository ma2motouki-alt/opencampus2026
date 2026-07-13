using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed partial class WorldSpaceMaskAnimationController
    {
        public bool TryGetNearestPlantLookTarget(Vector3 worldPosition, float radiusWorld, out int plantId, out Vector3 plantWorldPosition)
        {
            plantId = -1;
            plantWorldPosition = default;
            if (mapper == null || plants.Count == 0 || radiusWorld <= 0f)
            {
                return false;
            }

            var radiusSqr = radiusWorld * radiusWorld;
            var bestDistanceSqr = radiusSqr;
            var found = false;

            foreach (var plant in plants)
            {
                if (plant.CurrentStage == PlantStage.Dead || plant.HeightPx <= 2f)
                {
                    continue;
                }

                var rootWorldPosition = MaskToWorld(plant.Position);
                var bloomWorldPosition = MaskToWorld(plant.BloomPosition);
                var closestPoint = ClosestPointOnSegment(worldPosition, rootWorldPosition, bloomWorldPosition);
                var distanceSqr = (worldPosition - closestPoint).sqrMagnitude;
                if (distanceSqr > bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                plantId = plant.Id;
                plantWorldPosition = bloomWorldPosition;
                found = true;
            }

            return found;
        }

        public void PrepareSpatialQueries(NormalizedScreenMapper screenMapper)
        {
            if (screenMapper == null)
            {
                return;
            }

            mapper = screenMapper;
            EnsureRuntime();
        }

        public bool TryGetHighestAvailableLeafHangTarget(
            int plantId,
            Vector3 littlePersonWorldPosition,
            ISet<LeafHangSlot> occupiedLeafSlots,
            out LeafHangSlot selectedSlot,
            out Vector3 hangWorldPosition,
            out bool hangLeft)
        {
            selectedSlot = default;
            hangWorldPosition = default;
            hangLeft = false;
            if (mapper == null || plantId < 0 || plants.Count == 0)
            {
                return false;
            }

            var highestWorldY = float.NegativeInfinity;
            var found = false;

            foreach (var plant in plants)
            {
                if (plant.Id != plantId || plant.CurrentStage == PlantStage.Dead || plant.HeightPx <= 2f)
                {
                    continue;
                }

                for (var i = 0; i < Mathf.Max(0, plantLeafCount); i++)
                {
                    var slot = new LeafHangSlot(plant.Id, i);
                    if ((occupiedLeafSlots != null && occupiedLeafSlots.Contains(slot)) ||
                        !TryGetLeafHangWorldPosition(plant, i, out var candidate))
                    {
                        continue;
                    }

                    if (candidate.y <= highestWorldY)
                    {
                        continue;
                    }

                    highestWorldY = candidate.y;
                    selectedSlot = slot;
                    hangWorldPosition = candidate;
                    hangLeft = littlePersonWorldPosition.x < candidate.x;
                    found = true;
                }

                break;
            }

            return found;
        }

        public bool TryGetLeafHangTarget(int plantId, int leafIndex, out Vector3 hangWorldPosition)
        {
            hangWorldPosition = default;
            if (mapper == null || plantId < 0 || leafIndex < 0)
            {
                return false;
            }

            foreach (var plant in plants)
            {
                if (plant.Id != plantId)
                {
                    continue;
                }

                return plant.CurrentStage != PlantStage.Dead &&
                       plant.HeightPx > 2f &&
                       TryGetLeafHangWorldPosition(plant, leafIndex, out hangWorldPosition);
            }

            return false;
        }

        bool TryGetLeafHangWorldPosition(PlantModel plant, int leafIndex, out Vector3 hangWorldPosition)
        {
            hangWorldPosition = default;
            var worldUnitsPerMaskPx = mapper.WorldHeight / Mathf.Max(1f, MaskH - 1f);
            var rootWorldPosition = MaskToWorld(plant.Position);
            var bloomWorldPosition = MaskToWorld(plant.BloomPosition);
            var bloomLocalPosition = bloomWorldPosition - rootWorldPosition;
            if (!TryGetLeafTargetInfo(
                    leafIndex,
                    plantLeafCount,
                    bloomLocalPosition,
                    worldUnitsPerMaskPx,
                    plantLeafLengthPx,
                    plantLeafWidthPx,
                    plantLeafStartRatio,
                    plantLeafEndRatio,
                    plantLeafAngleDegrees,
                    out var leaf))
            {
                return false;
            }

            hangWorldPosition = rootWorldPosition +
                                leaf.LocalPosition +
                                leaf.Direction * leaf.LengthWorld * leaf.Scale * 0.48f +
                                Vector3.down * Mathf.Max(0f, leafHangOffsetPx) * worldUnitsPerMaskPx;
            return true;
        }

        public bool IsInputNearWorldPosition(
            IReadOnlyList<InteractionObject> interactionObjects,
            Vector3 worldPosition,
            float radiusWorld)
        {
            if (mapper == null || interactionObjects == null || interactionObjects.Count == 0 || radiusWorld <= 0f)
            {
                return false;
            }

            var pointPx = WorldToMaskPx(worldPosition);
            var radiusPx = WorldRadiusToMaskRadius(radiusWorld);
            foreach (var interactionObject in interactionObjects)
            {
                if (interactionObject == null)
                {
                    continue;
                }

                if (interactionObject.ShapeKind == InteractionShapeKind.Contour &&
                    interactionObject.ContourPoints.Count >= 3)
                {
                    var polygon = new Vector2[interactionObject.ContourPoints.Count];
                    for (var i = 0; i < polygon.Length; i++)
                    {
                        polygon[i] = NormalizedToMaskPx(interactionObject.ContourPoints[i]);
                    }

                    if (DistancePointToPolygon(polygon, pointPx) <= radiusPx)
                    {
                        return true;
                    }

                    continue;
                }

                var objectWorldPosition = mapper.ToWorld(interactionObject.Position);
                var objectRadius = mapper.ToWorldRadius(Mathf.Max(interactionObject.Size.x, interactionObject.Size.y) * 0.5f);
                if ((objectWorldPosition - worldPosition).sqrMagnitude <=
                    (radiusWorld + objectRadius) * (radiusWorld + objectRadius))
                {
                    return true;
                }
            }

            return false;
        }

        static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var segment = b - a;
            var lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= 0.000001f)
            {
                return a;
            }

            var t = Mathf.Clamp01(Vector3.Dot(point - a, segment) / lengthSqr);
            return a + segment * t;
        }

        void OnRainLanded(Vector2 landingPositionPx)
        {
            PlantModel target = null;
            var bestDistance = float.MaxValue;

            foreach (var plant in plants)
            {
                if (plant.CurrentStage == PlantStage.Dead)
                {
                    continue;
                }

                var mergeRadius = Mathf.Max(6f, plant.HeightPx * plantSpawnMergingRadiusRatio);
                var distance = Vector2.Distance(landingPositionPx, plant.Position);
                if (distance <= mergeRadius && distance < bestDistance)
                {
                    target = plant;
                    bestDistance = distance;
                }
            }

            if (target != null)
            {
                target.ReceiveRain();
                return;
            }

            if (plants.Count >= maxPlants)
            {
                return;
            }

            var newPlant = new PlantModel(
                nextPlantId++,
                landingPositionPx,
                plantSeedlingDuration,
                plantGrowingDuration,
                plantWiltingStartDelay,
                plantWiltingDuration,
                plantMaxHeightPx);
            newPlant.ReceiveRain();
            plants.Add(newPlant);
            PlantSpawnSequence++;
        }

        void UpdatePlants(float dt)
        {
            for (var i = plants.Count - 1; i >= 0; i--)
            {
                var plant = plants[i];
                var wasBloomable = plant.IsBloomable;
                plant.Advance(dt);
                if (!wasBloomable && plant.IsBloomable)
                {
                    PlantBloomSequence++;
                }

                if (plant.CurrentStage == PlantStage.Dead)
                {
                    plants.RemoveAt(i);
                }
            }

            SyncPlantViews();
        }

        enum PlantStage
        {
            Seedling,
            Growing,
            Blooming,
            Wilting,
            Dead
        }

        readonly struct LeafTargetInfo
        {
            public readonly Vector3 LocalPosition;
            public readonly Vector3 Direction;
            public readonly float LengthWorld;
            public readonly float WidthWorld;
            public readonly float Scale;

            public LeafTargetInfo(Vector3 localPosition, Vector3 direction, float lengthWorld, float widthWorld, float scale)
            {
                LocalPosition = localPosition;
                Direction = direction;
                LengthWorld = lengthWorld;
                WidthWorld = widthWorld;
                Scale = scale;
            }
        }

        static bool TryGetLeafTargetInfo(
            int index,
            int leafCount,
            Vector3 bloomLocalPosition,
            float worldUnitsPerMaskPx,
            float leafLengthPx,
            float leafWidthPx,
            float leafStartRatio,
            float leafEndRatio,
            float leafAngleDegrees,
            out LeafTargetInfo leaf)
        {
            leaf = default;
            var count = Mathf.Max(0, leafCount);
            var stemLength = bloomLocalPosition.magnitude;
            if (index < 0 || index >= count || stemLength <= 0.0005f)
            {
                return false;
            }

            var stemDirection = bloomLocalPosition.normalized;
            var start = Mathf.Clamp01(Mathf.Min(leafStartRatio, leafEndRatio));
            var end = Mathf.Clamp01(Mathf.Max(leafStartRatio, leafEndRatio));
            var leafLengthWorld = Mathf.Max(0.004f, leafLengthPx * worldUnitsPerMaskPx);
            var leafWidthWorld = Mathf.Max(0.002f, leafWidthPx * worldUnitsPerMaskPx);
            var growthScale = Mathf.Clamp01(stemLength / Mathf.Max(0.0005f, leafLengthWorld * 2.2f));
            if (growthScale <= 0.05f)
            {
                return false;
            }

            var ratio = count == 1 ? 0.5f : Mathf.Lerp(start, end, (float)index / (count - 1));
            var side = index % 2 == 0 ? -1f : 1f;
            var localPosition = bloomLocalPosition * ratio;
            var leafDirection = Quaternion.AngleAxis(leafAngleDegrees * side, Vector3.forward) * stemDirection;
            var phaseScale = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01((ratio - start + 0.08f) / Mathf.Max(0.01f, end - start + 0.08f)));
            var leafScale = growthScale * phaseScale;
            leaf = new LeafTargetInfo(localPosition, leafDirection, leafLengthWorld, leafWidthWorld, leafScale);
            return true;
        }

        sealed class PlantModel
        {
            readonly float seedlingDuration;
            readonly float growingDuration;
            readonly float wiltingStartDelay;
            readonly float wiltingDuration;
            readonly float maxHeightPx;

            public PlantModel(
                int id,
                Vector2 position,
                float seedlingDuration,
                float growingDuration,
                float wiltingStartDelay,
                float wiltingDuration,
                float maxHeightPx)
            {
                Id = id;
                Position = position;
                this.seedlingDuration = Mathf.Max(0.01f, seedlingDuration);
                this.growingDuration = Mathf.Max(0.01f, growingDuration);
                this.wiltingStartDelay = Mathf.Max(0f, wiltingStartDelay);
                this.wiltingDuration = Mathf.Max(0.01f, wiltingDuration);
                this.maxHeightPx = Mathf.Max(1f, maxHeightPx);
            }

            public int Id { get; }
            public Vector2 Position { get; }
            public float AgeSeconds { get; private set; }
            public float SecondsSinceRain { get; private set; }
            public float HeightPx { get; private set; }
            public Vector2 BloomPosition => new(Position.x, Position.y + HeightPx);
            public bool IsBloomable => CurrentStage is PlantStage.Blooming or PlantStage.Wilting;

            // 花のはじけ(A)の発火判定(立ち上がりエッジ検出)用。
            // 前フレームの時点で「手の輪郭がbloomSphereRadius以内にあったか」を保持する。
            public bool HandTouchingBloom;

            public PlantStage CurrentStage
            {
                get
                {
                    if (AgeSeconds < seedlingDuration)
                    {
                        return PlantStage.Seedling;
                    }

                    if (AgeSeconds < seedlingDuration + growingDuration)
                    {
                        return PlantStage.Growing;
                    }

                    var drySeconds = SecondsSinceRain - wiltingStartDelay;
                    if (drySeconds <= 0f)
                    {
                        return PlantStage.Blooming;
                    }

                    return drySeconds < wiltingDuration ? PlantStage.Wilting : PlantStage.Dead;
                }
            }

            public float WiltProgress01 => CurrentStage == PlantStage.Wilting
                ? Mathf.Clamp01((SecondsSinceRain - wiltingStartDelay) / wiltingDuration)
                : CurrentStage == PlantStage.Dead ? 1f : 0f;

            public void ReceiveRain()
            {
                SecondsSinceRain = 0f;
            }

            public void Advance(float deltaTime)
            {
                AgeSeconds += Mathf.Max(0f, deltaTime);
                SecondsSinceRain += Mathf.Max(0f, deltaTime);

                var growEnd = seedlingDuration + growingDuration;
                var growProgress = Mathf.Clamp01(AgeSeconds / growEnd);
                var eased = 1f - (1f - growProgress) * (1f - growProgress);
                HeightPx = maxHeightPx * eased;
            }
        }
    }
}
