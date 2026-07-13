using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Plants
{
    internal readonly struct LeafLayoutInfo
    {
        public LeafLayoutInfo(Vector3 localPosition, Vector3 direction, float lengthWorld, float widthWorld, float scale)
        {
            LocalPosition = localPosition;
            Direction = direction;
            LengthWorld = lengthWorld;
            WidthWorld = widthWorld;
            Scale = scale;
        }

        public Vector3 LocalPosition { get; }
        public Vector3 Direction { get; }
        public float LengthWorld { get; }
        public float WidthWorld { get; }
        public float Scale { get; }
    }

    internal static class LeafLayout
    {
        public static bool TryGet(int index, Vector3 bloomLocalPosition, float worldUnitsPerMaskPx, PlantSettings settings, out LeafLayoutInfo leaf)
        {
            leaf = default;
            var count = Mathf.Max(0, settings.LeafCount);
            var stemLength = bloomLocalPosition.magnitude;
            if (index < 0 || index >= count || stemLength <= 0.0005f) return false;

            var stemDirection = bloomLocalPosition.normalized;
            var start = Mathf.Clamp01(Mathf.Min(settings.LeafStartRatio, settings.LeafEndRatio));
            var end = Mathf.Clamp01(Mathf.Max(settings.LeafStartRatio, settings.LeafEndRatio));
            var length = Mathf.Max(0.004f, settings.LeafLengthPx * worldUnitsPerMaskPx);
            var width = Mathf.Max(0.002f, settings.LeafWidthPx * worldUnitsPerMaskPx);
            var growthScale = Mathf.Clamp01(stemLength / Mathf.Max(0.0005f, length * 2.2f));
            if (growthScale <= 0.05f) return false;

            var ratio = count == 1 ? 0.5f : Mathf.Lerp(start, end, (float)index / (count - 1));
            var side = index % 2 == 0 ? -1f : 1f;
            var position = bloomLocalPosition * ratio;
            var direction = Quaternion.AngleAxis(settings.LeafAngleDegrees * side, Vector3.forward) * stemDirection;
            var phaseScale = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01((ratio - start + 0.08f) / Mathf.Max(0.01f, end - start + 0.08f)));
            leaf = new LeafLayoutInfo(position, direction, length, width, growthScale * phaseScale);
            return true;
        }
    }
}
