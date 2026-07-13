using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class RainbowMaster
    {
        public int Id { get; }
        public string Name { get; }
        public int RequiredBloomCount { get; }
        public float MinCloudSunDistance { get; }
        public float DurationSeconds { get; }
        public float AppearDurationSeconds { get; }
        public float FadeDurationSeconds { get; }
        public float CooldownSeconds { get; }
        public float SpanNormalized { get; }
        public float RiseNormalized { get; }
        public int PathSegmentCount { get; }
        public float AttachDistance { get; }
        public float WalkSpeed { get; }
        public float SurfaceWidth { get; }
        public float TouchPadding { get; }
        public float TransferDurationSeconds { get; }
        public float ExitDwellSeconds { get; }
        public float DetachDistance { get; }
        public float ReconnectCooldownSeconds { get; }

        public RainbowMaster(
            int id,
            string name,
            int requiredBloomCount,
            float minCloudSunDistance,
            float durationSeconds,
            float appearDurationSeconds,
            float fadeDurationSeconds,
            float cooldownSeconds,
            float spanNormalized,
            float riseNormalized,
            int pathSegmentCount,
            float attachDistance,
            float walkSpeed,
            float surfaceWidth,
            float touchPadding,
            float transferDurationSeconds,
            float exitDwellSeconds,
            float detachDistance,
            float reconnectCooldownSeconds)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            RequiredBloomCount = Math.Max(1, requiredBloomCount);
            MinCloudSunDistance = Mathf.Max(0.001f, minCloudSunDistance);
            DurationSeconds = Mathf.Max(0.1f, durationSeconds);
            AppearDurationSeconds = Mathf.Clamp(appearDurationSeconds, 0f, DurationSeconds);
            FadeDurationSeconds = Mathf.Clamp(fadeDurationSeconds, 0f, DurationSeconds);
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            SpanNormalized = Mathf.Clamp(spanNormalized, 0.1f, 0.95f);
            RiseNormalized = Mathf.Clamp(riseNormalized, 0.05f, 0.9f);
            PathSegmentCount = Math.Max(4, pathSegmentCount);
            AttachDistance = Mathf.Max(0.001f, attachDistance);
            WalkSpeed = Mathf.Max(0.001f, walkSpeed);
            SurfaceWidth = Mathf.Max(0.001f, surfaceWidth);
            TouchPadding = Mathf.Max(0f, touchPadding);
            TransferDurationSeconds = Mathf.Max(0.001f, transferDurationSeconds);
            ExitDwellSeconds = Mathf.Max(0f, exitDwellSeconds);
            DetachDistance = Mathf.Max(0.001f, detachDistance);
            ReconnectCooldownSeconds = Mathf.Max(0f, reconnectCooldownSeconds);
        }
    }
    public sealed class AmbientObjectTypeMaster
    {
        public int Id { get; }
        public AmbientObjectKind Kind { get; }
        public string Name { get; }
        public Vector2 DefaultSize { get; }
        public Vector2 DriftVelocity { get; }
        public float ContactRadius { get; }
        public Color Color { get; }
        public int VisualEffectMasterId { get; }
        public float MovementEdgePadding { get; }
        public float MaxCenterY { get; }

        public AmbientObjectTypeMaster(
            int id,
            AmbientObjectKind kind,
            string name,
            Vector2 defaultSize,
            Vector2 driftVelocity,
            float contactRadius,
            Color color,
            int visualEffectMasterId,
            float movementEdgePadding = 0f,
            float maxCenterY = 1f)
        {
            Id = id;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DefaultSize = new Vector2(Mathf.Max(0.001f, defaultSize.x), Mathf.Max(0.001f, defaultSize.y));
            DriftVelocity = driftVelocity;
            ContactRadius = Mathf.Max(0.001f, contactRadius);
            Color = color;
            VisualEffectMasterId = visualEffectMasterId;
            MovementEdgePadding = Mathf.Max(0f, movementEdgePadding);
            MaxCenterY = Mathf.Clamp01(maxCenterY);
        }
    }
}
