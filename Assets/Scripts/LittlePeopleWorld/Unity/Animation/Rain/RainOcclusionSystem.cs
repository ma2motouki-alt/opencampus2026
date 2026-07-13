using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Rain
{
    internal sealed class RainOcclusionSystem
    {
        readonly Dictionary<int, float> visibleHeightRatios = new();
        RecognitionMask mask;
        bool enabled;
        bool visualEnabled;
        int probeRadiusPx;
        int topPaddingPx;
        float smoothingSeconds;
        int minVisibleHeightPx;
        float groundYPx;

        public int BlockedDropsThisFrame { get; private set; }
        public int LandedDropsThisFrame { get; private set; }
        public int ClippedColumnsThisFrame { get; private set; }
        public string DebugText { get; private set; } = string.Empty;

        public void Configure(RecognitionMask recognitionMask, bool occlusionEnabled, bool visualOcclusionEnabled, int probeRadius, int topPadding, float smoothing, int minimumVisibleHeight, float groundY)
        {
            mask = recognitionMask;
            enabled = occlusionEnabled;
            visualEnabled = visualOcclusionEnabled;
            probeRadiusPx = Mathf.Max(0, probeRadius);
            topPaddingPx = Mathf.Max(0, topPadding);
            smoothingSeconds = Mathf.Max(0f, smoothing);
            minVisibleHeightPx = Mathf.Max(0, minimumVisibleHeight);
            groundYPx = groundY;
        }

        public void BeginFrame()
        {
            BlockedDropsThisFrame = 0;
            LandedDropsThisFrame = 0;
            ClippedColumnsThisFrame = 0;
            DebugText = string.Empty;
        }

        public void CountLanded() => LandedDropsThisFrame++;

        public bool IsBlocked(Vector2 origin, Vector2 landing)
        {
            var blocked = enabled && TryFindBlockedY(origin, landing, out _);
            if (blocked) BlockedDropsThisFrame++;
            return blocked;
        }

        public void UpdateVisual(VisualEffectInstance effect, Vector2 origin, float halfWidthPx, float deltaTime)
        {
            if (!visualEnabled)
            {
                visibleHeightRatios.Remove(effect.Id);
                return;
            }

            var fullDistance = Mathf.Max(1f, Mathf.Abs(origin.y - groundYPx));
            var nearest = float.MaxValue;
            const int sampleCount = 5;
            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / (sampleCount - 1);
                var x = Mathf.Clamp(origin.x + Mathf.Lerp(-halfWidthPx, halfWidthPx, t), 1f, mask.Width - 2f);
                if (!TryFindBlockedY(new Vector2(x, origin.y), new Vector2(x, groundYPx), out var blockedY)) continue;
                nearest = Mathf.Min(nearest, Mathf.Abs(origin.y - blockedY));
            }

            var target = 1f;
            if (nearest < float.MaxValue)
            {
                target = Mathf.Clamp01(Mathf.Clamp(nearest, minVisibleHeightPx, fullDistance) / fullDistance);
                ClippedColumnsThisFrame++;
            }
            var current = visibleHeightRatios.TryGetValue(effect.Id, out var ratio) ? ratio : target;
            var blend = smoothingSeconds <= 0.0001f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / smoothingSeconds);
            visibleHeightRatios[effect.Id] = Mathf.Lerp(current, target, blend);
        }

        public float GetVisibleHeightRatio(VisualEffectInstance effect)
        {
            if (effect == null || !visualEnabled) return 1f;
            return visibleHeightRatios.TryGetValue(effect.Id, out var ratio) ? Mathf.Clamp01(ratio) : 1f;
        }

        public void RemoveMissingEffects(ISet<int> liveEffectIds)
        {
            var dead = new List<int>();
            foreach (var id in visibleHeightRatios.Keys) if (!liveEffectIds.Contains(id)) dead.Add(id);
            foreach (var id in dead) visibleHeightRatios.Remove(id);
        }

        public void FinishFrame(bool showDebug)
        {
            if (!showDebug)
            {
                DebugText = string.Empty;
                return;
            }
            DebugText = $"Rain Occlusion: {(enabled ? "on" : "off")}  Blocked: {BlockedDropsThisFrame}  Landed: {LandedDropsThisFrame}  Clipped: {ClippedColumnsThisFrame}  Mask: {mask.EffectiveWhiteCount}";
        }

        bool TryFindBlockedY(Vector2 origin, Vector2 landing, out int blockedY)
        {
            blockedY = 0;
            if (mask == null || mask.EffectiveWhiteCount <= 0) return false;
            var startY = Mathf.RoundToInt(origin.y);
            var endY = Mathf.RoundToInt(landing.y);
            var step = startY >= endY ? -1 : 1;
            startY += step * topPaddingPx;
            if ((step < 0 && startY < endY) || (step > 0 && startY > endY)) return false;
            startY = Mathf.Clamp(startY, 0, mask.Height - 1);
            endY = Mathf.Clamp(endY, 0, mask.Height - 1);
            var centerX = Mathf.Clamp(Mathf.RoundToInt(landing.x), 0, mask.Width - 1);
            var minX = Mathf.Clamp(centerX - probeRadiusPx, 0, mask.Width - 1);
            var maxX = Mathf.Clamp(centerX + probeRadiusPx, 0, mask.Width - 1);
            for (var y = startY;; y += step)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (!mask.IsSet(x, y)) continue;
                    blockedY = y;
                    return true;
                }
                if (y == endY) break;
            }
            return false;
        }
    }
}
