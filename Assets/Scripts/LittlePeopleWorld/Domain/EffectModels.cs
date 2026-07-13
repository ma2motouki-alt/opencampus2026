using System;
using System.Collections.Generic;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Domain
{
    public enum VisualEffectKind
    {
        SoftGlow = 1,
        CuriousPulse = 2,
        StartleShadow = 3,
        RainColumn = 4,
        StarBurst = 5
    }
    public enum VisualEffectRenderMode
    {
        Procedural = 1,
        Prefab = 2
    }
    public sealed class ReactionInstance
    {
        public int ReactionMasterId { get; }
        public float RemainingSeconds { get; private set; }
        public bool IsExpired => RemainingSeconds <= 0f;

        public ReactionInstance(ReactionMaster master)
        {
            if (master == null)
            {
                throw new ArgumentNullException(nameof(master));
            }

            ReactionMasterId = master.Id;
            RemainingSeconds = master.DurationSeconds;
        }

        public void Advance(float deltaTime)
        {
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
        }
    }
    public sealed class VisualEffectInstance
    {
        public int Id { get; }
        public int VisualEffectMasterId { get; }
        public int SourceObjectId { get; }
        public Vector2 Position { get; private set; }
        public Vector2 Size { get; private set; }
        public float AngleDegrees { get; private set; }
        public float DurationSeconds { get; private set; }
        public float AgeSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsExpired => RemainingSeconds <= 0f;
        public float NormalizedAge => DurationSeconds <= 0.0001f ? 1f : Mathf.Clamp01(AgeSeconds / DurationSeconds);

        public VisualEffectInstance(
            int id,
            int visualEffectMasterId,
            int sourceObjectId,
            Vector2 position,
            Vector2 size,
            float angleDegrees,
            float durationSeconds)
        {
            Id = id;
            VisualEffectMasterId = visualEffectMasterId;
            SourceObjectId = sourceObjectId;
            Position = Clamp01(position);
            Size = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
            AngleDegrees = angleDegrees;
            DurationSeconds = Mathf.Max(0.001f, durationSeconds);
            RemainingSeconds = DurationSeconds;
        }

        public void Refresh(Vector2 position, Vector2 size, float angleDegrees, float durationSeconds)
        {
            Position = Clamp01(position);
            Size = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
            AngleDegrees = angleDegrees;
            DurationSeconds = Mathf.Max(0.001f, durationSeconds);
            RemainingSeconds = DurationSeconds;
        }

        public void Advance(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            AgeSeconds += deltaTime;
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
        }

        static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }
    }
}
