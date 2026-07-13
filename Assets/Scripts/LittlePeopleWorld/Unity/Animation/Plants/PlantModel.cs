using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Plants
{
    internal enum PlantStage
    {
        Seedling,
        Growing,
        Blooming,
        Wilting,
        Dead
    }

    internal sealed class PlantModel
    {
        readonly float seedlingDuration;
        readonly float growingDuration;
        readonly float wiltingStartDelay;
        readonly float wiltingDuration;
        readonly float maxHeightPx;

        public PlantModel(int id, Vector2 position, PlantSettings settings)
        {
            Id = id;
            Position = position;
            seedlingDuration = Mathf.Max(0.01f, settings.SeedlingDuration);
            growingDuration = Mathf.Max(0.01f, settings.GrowingDuration);
            wiltingStartDelay = Mathf.Max(0f, settings.WiltingStartDelay);
            wiltingDuration = Mathf.Max(0.01f, settings.WiltingDuration);
            maxHeightPx = Mathf.Max(1f, settings.MaxHeightPx);
        }

        public int Id { get; }
        public Vector2 Position { get; }
        public float AgeSeconds { get; private set; }
        public float SecondsSinceRain { get; private set; }
        public float HeightPx { get; private set; }
        public Vector2 BloomPosition => new(Position.x, Position.y + HeightPx);
        public bool IsBloomable => CurrentStage is PlantStage.Blooming or PlantStage.Wilting;
        public bool HandTouchingBloom { get; set; }

        public PlantStage CurrentStage
        {
            get
            {
                if (AgeSeconds < seedlingDuration) return PlantStage.Seedling;
                if (AgeSeconds < seedlingDuration + growingDuration) return PlantStage.Growing;

                var drySeconds = SecondsSinceRain - wiltingStartDelay;
                if (drySeconds <= 0f) return PlantStage.Blooming;
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
            var growProgress = Mathf.Clamp01(AgeSeconds / (seedlingDuration + growingDuration));
            var eased = 1f - (1f - growProgress) * (1f - growProgress);
            HeightPx = maxHeightPx * eased;
        }
    }
}
