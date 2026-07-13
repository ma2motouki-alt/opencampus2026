using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed partial class WorldSpaceMaskAnimationController
    {
        public float GetRainVisibleHeightRatio(VisualEffectInstance effect)
        {
            if (effect == null || !enableRainVisualOcclusion)
            {
                return 1f;
            }

            return rainVisibleHeightRatios.TryGetValue(effect.Id, out var ratio)
                ? Mathf.Clamp01(ratio)
                : 1f;
        }

        void AdvanceRainPlants(World world, MasterDatabase masters, float dt)
        {
            rainOcclusionBlockedDropsThisFrame = 0;
            rainOcclusionLandedDropsThisFrame = 0;
            rainOcclusionVisualClippedColumnsThisFrame = 0;
            RainOcclusionDebugText = string.Empty;

            if (enableRainPlants)
            {
                UpdateRainLanding(world, masters, dt);
            }

            UpdateRainOcclusionDebugText();
            UpdatePlants(dt);
        }

        void UpdateRainLanding(World world, MasterDatabase masters, float dt)
        {
            var liveKeys = new HashSet<int>();
            var liveEffectIds = new HashSet<int>();

            foreach (var effect in world.VisualEffects)
            {
                var effectMaster = masters.VisualEffects.Get(effect.VisualEffectMasterId);
                if (effectMaster.Kind != VisualEffectKind.RainColumn)
                {
                    continue;
                }

                var key = effect.SourceObjectId != 0 ? effect.SourceObjectId : effect.Id;
                liveKeys.Add(key);
                liveEffectIds.Add(effect.Id);

                rainActiveSeconds.TryGetValue(key, out var activeSeconds);
                rainLandedCounts.TryGetValue(key, out var landedCount);
                activeSeconds += dt;

                var rainOrigin = NormalizedToMaskPx(effect.Position);
                var rainHalfWidthPx = Mathf.Max(2f, effect.Size.x * MaskW * 0.45f);
                UpdateRainVisualOcclusion(effect, rainOrigin, rainHalfWidthPx, dt);

                var fallDistance = Mathf.Max(1f, rainOrigin.y - groundYPx);
                var landingInterval = fallDistance / Mathf.Max(1f, rainFallSpeedPxPerSec);
                var shouldHaveLanded = Mathf.FloorToInt(activeSeconds / Mathf.Max(0.05f, landingInterval));

                while (landedCount < shouldHaveLanded)
                {
                    var jitterX = UnityEngine.Random.Range(-rainHalfWidthPx, rainHalfWidthPx);
                    var landingPosition = new Vector2(Mathf.Clamp(rainOrigin.x + jitterX, 1f, MaskW - 2f), groundYPx);
                    var dropOrigin = new Vector2(landingPosition.x, rainOrigin.y);
                    if (IsRainBlockedByMask(dropOrigin, landingPosition))
                    {
                        rainOcclusionBlockedDropsThisFrame++;
                    }
                    else
                    {
                        OnRainLanded(landingPosition);
                        rainOcclusionLandedDropsThisFrame++;
                    }

                    landedCount++;
                }

                rainActiveSeconds[key] = activeSeconds;
                rainLandedCounts[key] = landedCount;
            }

            RemoveDeadRainSources(liveKeys);
            RemoveDeadRainVisualOcclusion(liveEffectIds);
        }

        void UpdateRainVisualOcclusion(VisualEffectInstance effect, Vector2 rainOriginPx, float rainHalfWidthPx, float dt)
        {
            if (!enableRainVisualOcclusion)
            {
                rainVisibleHeightRatios.Remove(effect.Id);
                return;
            }

            var fullDistance = Mathf.Max(1f, Mathf.Abs(rainOriginPx.y - groundYPx));
            var bestBlockedDistance = float.MaxValue;
            const int SampleCount = 5;

            for (var i = 0; i < SampleCount; i++)
            {
                var t = SampleCount == 1 ? 0.5f : (float)i / (SampleCount - 1);
                var x = Mathf.Clamp(rainOriginPx.x + Mathf.Lerp(-rainHalfWidthPx, rainHalfWidthPx, t), 1f, MaskW - 2f);
                var sampleOrigin = new Vector2(x, rainOriginPx.y);
                var sampleLanding = new Vector2(x, groundYPx);
                if (!TryFindRainOcclusionY(sampleOrigin, sampleLanding, out var blockedY))
                {
                    continue;
                }

                var blockedDistance = Mathf.Abs(rainOriginPx.y - blockedY);
                if (blockedDistance < bestBlockedDistance)
                {
                    bestBlockedDistance = blockedDistance;
                }
            }

            var targetRatio = 1f;
            if (bestBlockedDistance < float.MaxValue)
            {
                var minVisibleHeight = Mathf.Clamp(rainOcclusionMinVisibleHeightPx, 0, Mathf.RoundToInt(fullDistance));
                var visibleHeight = Mathf.Clamp(bestBlockedDistance, minVisibleHeight, fullDistance);
                targetRatio = Mathf.Clamp01(visibleHeight / fullDistance);
                rainOcclusionVisualClippedColumnsThisFrame++;
            }

            var currentRatio = rainVisibleHeightRatios.TryGetValue(effect.Id, out var current)
                ? current
                : targetRatio;
            var smoothingSeconds = Mathf.Max(0f, rainOcclusionVisualSmoothingSeconds);
            var blend = smoothingSeconds <= 0.0001f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, dt) / smoothingSeconds);
            rainVisibleHeightRatios[effect.Id] = Mathf.Lerp(currentRatio, targetRatio, blend);
        }

        bool IsRainBlockedByMask(Vector2 rainOriginPx, Vector2 landingPx)
        {
            return enableRainOcclusionByMask && TryFindRainOcclusionY(rainOriginPx, landingPx, out _);
        }

        bool TryFindRainOcclusionY(Vector2 rainOriginPx, Vector2 landingPx, out int blockedYPx)
        {
            blockedYPx = 0;
            if (effectiveMask == null || effectiveWhiteCount <= 0)
            {
                return false;
            }

            var startY = Mathf.RoundToInt(rainOriginPx.y);
            var endY = Mathf.RoundToInt(landingPx.y);
            var step = startY >= endY ? -1 : 1;
            startY += step * Mathf.Max(0, rainOcclusionTopPaddingPx);

            if ((step < 0 && startY < endY) || (step > 0 && startY > endY))
            {
                return false;
            }

            startY = Mathf.Clamp(startY, 0, MaskH - 1);
            endY = Mathf.Clamp(endY, 0, MaskH - 1);

            var centerX = Mathf.Clamp(Mathf.RoundToInt(landingPx.x), 0, MaskW - 1);
            var probeRadius = Mathf.Max(0, rainOcclusionProbeRadiusPx);
            var minX = Mathf.Clamp(centerX - probeRadius, 0, MaskW - 1);
            var maxX = Mathf.Clamp(centerX + probeRadius, 0, MaskW - 1);

            for (var y = startY;; y += step)
            {
                var row = y * MaskW;
                for (var x = minX; x <= maxX; x++)
                {
                    if (effectiveMask[row + x])
                    {
                        blockedYPx = y;
                        return true;
                    }
                }

                if (y == endY)
                {
                    break;
                }
            }

            return false;
        }

        void UpdateRainOcclusionDebugText()
        {
            if (!showRainOcclusionDebug)
            {
                RainOcclusionDebugText = string.Empty;
                return;
            }

            var status = enableRainOcclusionByMask ? "on" : "off";
            RainOcclusionDebugText =
                $"Rain Occlusion: {status}  Blocked: {rainOcclusionBlockedDropsThisFrame}  Landed: {rainOcclusionLandedDropsThisFrame}  Clipped: {rainOcclusionVisualClippedColumnsThisFrame}  Mask: {effectiveWhiteCount}";
        }

        void RemoveDeadRainSources(HashSet<int> liveKeys)
        {
            var dead = new List<int>();
            foreach (var key in rainActiveSeconds.Keys)
            {
                if (!liveKeys.Contains(key))
                {
                    dead.Add(key);
                }
            }

            foreach (var key in dead)
            {
                rainActiveSeconds.Remove(key);
                rainLandedCounts.Remove(key);
            }
        }

        void RemoveDeadRainVisualOcclusion(HashSet<int> liveEffectIds)
        {
            var dead = new List<int>();
            foreach (var effectId in rainVisibleHeightRatios.Keys)
            {
                if (!liveEffectIds.Contains(effectId))
                {
                    dead.Add(effectId);
                }
            }

            foreach (var effectId in dead)
            {
                rainVisibleHeightRatios.Remove(effectId);
            }
        }
    }
}
