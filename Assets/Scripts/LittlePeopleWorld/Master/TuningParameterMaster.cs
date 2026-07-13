using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class TuningParameterMaster
    {
        public int Id { get; }
        public string Name { get; }
        public float MaxDeltaTime { get; }
        public float WorldEdgePadding { get; }
        public float InputHitPadding { get; }
        public float HandContourReactionPadding { get; }
        public float FallDuration { get; }
        public float FallLateralDistance { get; }
        public float FallLaunchDistance { get; }
        public int AmbientCloudCount { get; }
        public int AmbientStarCount { get; }
        public float RainLingerSeconds { get; }
        public float StarCooldownSeconds { get; }
        public float SurfaceReconnectCooldownSeconds { get; }

        public TuningParameterMaster(
            int id,
            string name,
            float maxDeltaTime,
            float worldEdgePadding,
            float inputHitPadding,
            float handContourReactionPadding,
            float fallDuration,
            float fallLateralDistance,
            float fallLaunchDistance,
            int ambientCloudCount,
            int ambientStarCount,
            float rainLingerSeconds,
            float starCooldownSeconds,
            float surfaceReconnectCooldownSeconds)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MaxDeltaTime = Mathf.Max(0.001f, maxDeltaTime);
            WorldEdgePadding = Mathf.Clamp01(worldEdgePadding);
            InputHitPadding = Mathf.Max(0f, inputHitPadding);
            HandContourReactionPadding = Mathf.Max(0f, handContourReactionPadding);
            FallDuration = Mathf.Max(0.001f, fallDuration);
            FallLateralDistance = Mathf.Max(0f, fallLateralDistance);
            FallLaunchDistance = Mathf.Max(0f, fallLaunchDistance);
            AmbientCloudCount = Math.Max(0, ambientCloudCount);
            AmbientStarCount = Math.Max(0, ambientStarCount);
            RainLingerSeconds = Mathf.Max(0.01f, rainLingerSeconds);
            StarCooldownSeconds = Mathf.Max(0.01f, starCooldownSeconds);
            SurfaceReconnectCooldownSeconds = Mathf.Max(0f, surfaceReconnectCooldownSeconds);
        }
    }
}
