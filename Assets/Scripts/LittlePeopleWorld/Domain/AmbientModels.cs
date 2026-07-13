using System;
using System.Collections.Generic;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Domain
{
    public enum AmbientObjectKind
    {
        Cloud = 1,
        Star = 2
    }
    public enum AmbientObjectState
    {
        Idle,
        Reacting,
        Cooldown
    }
    public sealed class AmbientObject
    {
        public int Id { get; }
        public AmbientObjectKind Kind { get; }
        public Vector2 Position { get; private set; }
        public Vector2 Size { get; }
        public Vector2 Velocity { get; private set; }
        public float ContactRadius { get; }
        public AmbientObjectState State { get; private set; }
        public float ReactionSeconds { get; private set; }
        public float CooldownSeconds { get; private set; }
        public bool IsReacting => ReactionSeconds > 0f;

        public float EdgePadding { get; }
        public float MaxCenterY { get; }

        public AmbientObject(
            int id,
            AmbientObjectKind kind,
            Vector2 position,
            Vector2 size,
            Vector2 velocity,
            float contactRadius,
            float edgePadding = 0f,   // ← 追加。デフォルト0なので既存の呼び出し元は無変更で動く
            float maxCenterY = 1f)
        {
            Id = id;
            Kind = kind;
            Position = Clamp01(position);
            Size = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
            Velocity = velocity;
            ContactRadius = Mathf.Max(0.001f, contactRadius);
            EdgePadding = Mathf.Max(0f, edgePadding);   // ← 追加
            MaxCenterY = Mathf.Clamp01(maxCenterY);   // ← 追加
            State = AmbientObjectState.Idle;
        }

        public void Advance(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            Position += Velocity * deltaTime;
            BounceInsideWorld();
            ReactionSeconds = Mathf.Max(0f, ReactionSeconds - deltaTime);
            CooldownSeconds = Mathf.Max(0f, CooldownSeconds - deltaTime);

            if (ReactionSeconds > 0f)
            {
                State = AmbientObjectState.Reacting;
            }
            else if (CooldownSeconds > 0f)
            {
                State = AmbientObjectState.Cooldown;
            }
            else
            {
                State = AmbientObjectState.Idle;
            }
        }

        public bool IsTouchedBy(LittlePerson person)
        {
            if (person == null || person.CurrentBehavior == LittlePersonBehaviorKind.Falling)
            {
                return false;
            }

            return IsTouchedBy(person.Position);
        }

        // 追加: 位置だけで判定する汎用版。LittlePerson以外(深度マスクの粒など)からも使える。
        public bool IsTouchedBy(Vector2 position)
        {
            return Vector2.Distance(Position, position) <= ContactRadius;
        }

        public void MarkCloudTouched(float lingerSeconds)
        {
            ReactionSeconds = Mathf.Max(ReactionSeconds, Mathf.Max(0.01f, lingerSeconds));
            State = AmbientObjectState.Reacting;
        }

        public bool TryTriggerStar(float reactionSeconds, float cooldownSeconds)
        {
            if (CooldownSeconds > 0f)
            {
                return false;
            }

            ReactionSeconds = Mathf.Max(0.01f, reactionSeconds);
            CooldownSeconds = Mathf.Max(ReactionSeconds, cooldownSeconds);
            State = AmbientObjectState.Reacting;
            return true;
        }

        void BounceInsideWorld()
        {
            var half = Size * 0.5f;
            var minX = Mathf.Clamp01(half.x + EdgePadding);
            var minY = Mathf.Clamp01(half.y + EdgePadding);
            var maxX = Mathf.Clamp01(1f - half.x - EdgePadding);
            // 通常の右端/下端の余白と、MaxCenterY(下半分に行かせない制限)の両方のうち、厳しい方を使う
            var maxY = Mathf.Max(minY, Mathf.Min(Mathf.Clamp01(1f - half.y - EdgePadding), MaxCenterY));

            var min = new Vector2(minX, minY);
            var max = new Vector2(maxX, maxY);
            var next = Position;
            var nextVelocity = Velocity;

            if (next.x < min.x || next.x > max.x)
            {
                next.x = Mathf.Clamp(next.x, min.x, max.x);
                nextVelocity.x *= -1f;
            }

            if (next.y < min.y || next.y > max.y)
            {
                next.y = Mathf.Clamp(next.y, min.y, max.y);
                nextVelocity.y *= -1f;
            }

            Position = next;
            Velocity = nextVelocity;
        }

        static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }
    }
}
