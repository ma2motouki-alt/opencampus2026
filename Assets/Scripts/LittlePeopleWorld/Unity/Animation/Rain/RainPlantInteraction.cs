using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using LittlePeopleWorld.Unity.Animation.Plants;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Rain
{
    internal sealed class RainPlantInteraction
    {
        readonly Dictionary<int, float> activeSeconds = new();
        readonly Dictionary<int, int> landedCounts = new();
        readonly RainOcclusionSystem occlusion;
        RecognitionMask mask;
        PlantSystem plants;
        float fallSpeedPxPerSec;
        float groundYPx;

        public RainPlantInteraction(RainOcclusionSystem occlusion)
        {
            this.occlusion = occlusion;
        }

        public void Configure(RecognitionMask recognitionMask, PlantSystem plantSystem, float fallSpeed, float groundY)
        {
            mask = recognitionMask;
            plants = plantSystem;
            fallSpeedPxPerSec = fallSpeed;
            groundYPx = groundY;
        }

        public void Advance(World world, MasterDatabase masters, float deltaTime, bool enablePlants)
        {
            var liveSources = new HashSet<int>();
            var liveEffects = new HashSet<int>();
            foreach (var effect in world.VisualEffects)
            {
                if (masters.VisualEffects.Get(effect.VisualEffectMasterId).Kind != VisualEffectKind.RainColumn) continue;
                var key = effect.SourceObjectId != 0 ? effect.SourceObjectId : effect.Id;
                liveSources.Add(key);
                liveEffects.Add(effect.Id);
                activeSeconds.TryGetValue(key, out var seconds);
                landedCounts.TryGetValue(key, out var landed);
                seconds += deltaTime;
                var origin = mask.NormalizedToPixel(effect.Position);
                var halfWidth = Mathf.Max(2f, effect.Size.x * mask.Width * 0.45f);
                occlusion.UpdateVisual(effect, origin, halfWidth, deltaTime);
                if (enablePlants)
                {
                    var interval = Mathf.Max(1f, origin.y - groundYPx) / Mathf.Max(1f, fallSpeedPxPerSec);
                    var targetCount = Mathf.FloorToInt(seconds / Mathf.Max(0.05f, interval));
                    while (landed < targetCount)
                    {
                        var x = Mathf.Clamp(origin.x + Random.Range(-halfWidth, halfWidth), 1f, mask.Width - 2f);
                        var landing = new Vector2(x, groundYPx);
                        if (!occlusion.IsBlocked(new Vector2(x, origin.y), landing))
                        {
                            plants.ReceiveRain(landing);
                            occlusion.CountLanded();
                        }
                        landed++;
                    }
                }
                activeSeconds[key] = seconds;
                landedCounts[key] = landed;
            }
            RemoveDead(activeSeconds, liveSources);
            RemoveDead(landedCounts, liveSources);
            occlusion.RemoveMissingEffects(liveEffects);
        }

        static void RemoveDead<T>(Dictionary<int, T> dictionary, ISet<int> live)
        {
            var dead = new List<int>();
            foreach (var key in dictionary.Keys) if (!live.Contains(key)) dead.Add(key);
            foreach (var key in dead) dictionary.Remove(key);
        }
    }
}
