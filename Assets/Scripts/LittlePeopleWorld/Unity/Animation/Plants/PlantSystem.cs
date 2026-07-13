using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Plants
{
    internal sealed class PlantSystem
    {
        readonly List<PlantModel> plants = new();
        readonly Dictionary<int, PlantView> views = new();
        readonly Transform root;
        int nextPlantId = 1;
        PlantSettings settings;
        RecognitionMask mask;
        NormalizedScreenMapper mapper;

        public PlantSystem(Transform root)
        {
            this.root = root;
        }

        public IReadOnlyList<PlantModel> Plants => plants;
        public int SpawnSequence { get; private set; }
        public int BloomSequence { get; private set; }
        public bool HasActivePlants => plants.Count > 0;
        public bool HasGrowingPlants
        {
            get
            {
                foreach (var plant in plants)
                    if (plant.CurrentStage is PlantStage.Seedling or PlantStage.Growing) return true;
                return false;
            }
        }

        public int ActiveBloomCount
        {
            get
            {
                var count = 0;
                foreach (var plant in plants) if (plant.IsBloomable) count++;
                return count;
            }
        }

        public void Configure(PlantSettings nextSettings, RecognitionMask recognitionMask, NormalizedScreenMapper screenMapper)
        {
            settings = nextSettings;
            mask = recognitionMask;
            mapper = screenMapper;
        }

        public void ReceiveRain(Vector2 landingPositionPx)
        {
            if (settings == null) return;
            PlantModel target = null;
            var bestDistance = float.MaxValue;
            foreach (var plant in plants)
            {
                if (plant.CurrentStage == PlantStage.Dead) continue;
                var mergeRadius = Mathf.Max(6f, plant.HeightPx * settings.SpawnMergingRadiusRatio);
                var distance = Vector2.Distance(landingPositionPx, plant.Position);
                if (distance > mergeRadius || distance >= bestDistance) continue;
                target = plant;
                bestDistance = distance;
            }

            if (target != null)
            {
                target.ReceiveRain();
                return;
            }

            if (plants.Count >= settings.MaxPlants) return;
            var newPlant = new PlantModel(nextPlantId++, landingPositionPx, settings);
            newPlant.ReceiveRain();
            plants.Add(newPlant);
            SpawnSequence++;
        }

        public void Advance(float deltaTime)
        {
            for (var i = plants.Count - 1; i >= 0; i--)
            {
                var plant = plants[i];
                var wasBloomable = plant.IsBloomable;
                plant.Advance(deltaTime);
                if (!wasBloomable && plant.IsBloomable) BloomSequence++;
                if (plant.CurrentStage == PlantStage.Dead) plants.RemoveAt(i);
            }
            SyncViews();
        }

        public bool TryGetNearestLookTarget(Vector3 worldPosition, float radiusWorld, out int plantId, out Vector3 plantWorldPosition)
        {
            plantId = -1;
            plantWorldPosition = default;
            if (mapper == null || radiusWorld <= 0f) return false;
            var best = radiusWorld * radiusWorld;
            var found = false;
            foreach (var plant in plants)
            {
                if (plant.CurrentStage == PlantStage.Dead || plant.HeightPx <= 2f) continue;
                var rootWorld = mask.PixelToWorld(plant.Position, mapper);
                var bloomWorld = mask.PixelToWorld(plant.BloomPosition, mapper);
                var closest = ClosestPointOnSegment(worldPosition, rootWorld, bloomWorld);
                var distance = (worldPosition - closest).sqrMagnitude;
                if (distance > best) continue;
                best = distance;
                plantId = plant.Id;
                plantWorldPosition = bloomWorld;
                found = true;
            }
            return found;
        }

        public bool TryGetHighestAvailableLeaf(int plantId, Vector3 personWorld, ISet<LeafHangSlot> occupied, out LeafHangSlot slot, out Vector3 worldPosition, out bool hangLeft)
        {
            slot = default;
            worldPosition = default;
            hangLeft = false;
            var highest = float.NegativeInfinity;
            var found = false;
            foreach (var plant in plants)
            {
                if (plant.Id != plantId || plant.CurrentStage == PlantStage.Dead || plant.HeightPx <= 2f) continue;
                for (var i = 0; i < Mathf.Max(0, settings.LeafCount); i++)
                {
                    var candidateSlot = new LeafHangSlot(plant.Id, i);
                    if ((occupied != null && occupied.Contains(candidateSlot)) || !TryGetLeafWorldPosition(plant, i, out var candidate)) continue;
                    if (candidate.y <= highest) continue;
                    highest = candidate.y;
                    slot = candidateSlot;
                    worldPosition = candidate;
                    hangLeft = personWorld.x < candidate.x;
                    found = true;
                }
                break;
            }
            return found;
        }

        public bool TryGetLeafTarget(int plantId, int leafIndex, out Vector3 worldPosition)
        {
            worldPosition = default;
            foreach (var plant in plants)
            {
                if (plant.Id != plantId) continue;
                return plant.CurrentStage != PlantStage.Dead && plant.HeightPx > 2f && TryGetLeafWorldPosition(plant, leafIndex, out worldPosition);
            }
            return false;
        }

        bool TryGetLeafWorldPosition(PlantModel plant, int leafIndex, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (mapper == null || settings == null) return false;
            var unitsPerPixel = mapper.WorldHeight / Mathf.Max(1f, mask.Height - 1f);
            var rootWorld = mask.PixelToWorld(plant.Position, mapper);
            var bloomLocal = mask.PixelToWorld(plant.BloomPosition, mapper) - rootWorld;
            if (!LeafLayout.TryGet(leafIndex, bloomLocal, unitsPerPixel, settings, out var leaf)) return false;
            worldPosition = rootWorld + leaf.LocalPosition + leaf.Direction * leaf.LengthWorld * leaf.Scale * 0.48f +
                            Vector3.down * Mathf.Max(0f, settings.LeafHangOffsetPx) * unitsPerPixel;
            return true;
        }

        void SyncViews()
        {
            if (mapper == null || settings == null) return;
            var liveIds = new HashSet<int>();
            var unitsPerPixel = mapper.WorldHeight / Mathf.Max(1f, mask.Height - 1f);
            foreach (var plant in plants)
            {
                liveIds.Add(plant.Id);
                if (!views.TryGetValue(plant.Id, out var view))
                {
                    var go = new GameObject($"plant_{plant.Id}");
                    go.transform.SetParent(root, false);
                    view = go.AddComponent<PlantView>();
                    view.Initialize();
                    views[plant.Id] = view;
                }
                view.Render(plant, mask.PixelToWorld(plant.Position, mapper), mask.PixelToWorld(plant.BloomPosition, mapper), unitsPerPixel, settings);
            }

            var dead = new List<int>();
            foreach (var pair in views) if (!liveIds.Contains(pair.Key)) dead.Add(pair.Key);
            foreach (var id in dead)
            {
                if (views[id] != null) Object.Destroy(views[id].gameObject);
                views.Remove(id);
            }
        }

        static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var segment = b - a;
            if (segment.sqrMagnitude <= 0.000001f) return a;
            var t = Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude);
            return a + segment * t;
        }
    }
}
