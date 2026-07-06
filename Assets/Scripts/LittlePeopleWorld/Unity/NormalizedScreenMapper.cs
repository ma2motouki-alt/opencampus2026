using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class NormalizedScreenMapper
    {
        readonly Camera targetCamera;
        readonly float worldHeight;

        public NormalizedScreenMapper(Camera targetCamera, float worldHeight)
        {
            this.targetCamera = targetCamera;
            this.worldHeight = Mathf.Max(1f, worldHeight);
        }

        public float WorldHeight => worldHeight;
        public float WorldWidth => worldHeight * (targetCamera == null ? 16f / 9f : targetCamera.aspect);

        public Vector3 ToWorld(Vector2 normalized)
        {
            return new Vector3(
                (normalized.x - 0.5f) * WorldWidth,
                (0.5f - normalized.y) * worldHeight,
                0f);
        }

        public Vector2 ToNormalized(Vector3 world)
        {
            return new Vector2(
                Mathf.Clamp01(world.x / WorldWidth + 0.5f),
                Mathf.Clamp01(0.5f - world.y / worldHeight));
        }

        public Vector3 ToWorldScale(Vector2 normalizedSize)
        {
            return new Vector3(
                Mathf.Max(0.001f, normalizedSize.x * WorldWidth),
                Mathf.Max(0.001f, normalizedSize.y * worldHeight),
                1f);
        }

        public float ToWorldRadius(float normalizedRadius)
        {
            return normalizedRadius * Mathf.Min(WorldWidth, worldHeight);
        }
    }
}
