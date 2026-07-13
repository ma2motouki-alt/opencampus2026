using System;
using System.Collections.Generic;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Domain
{
    public enum InteractionObjectKind
    {
        Hand = 1,
        RoundProp = 2,
        MaskStroke = 3,
        BlockProp = 4
    }

    public enum InteractionShapeKind
    {
        Primitive = 1,
        Contour = 2
    }

    public enum InteractionObjectState
    {
        Placing,
        Dragging,
        Placed
    }

    public enum InteractionFieldKind
    {
        Attractor,
        Repeller,
        OrbitAttractor,
        Shadow
    }

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

    public enum RainbowState
    {
        Appearing,
        Active,
        Fading,
        Expired
    }

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

    public enum LittlePersonBehaviorKind
    {
        Wander,
        Approach,
        Flee,
        Orbit,
        FollowEdge,
        EdgeWalk,
        TransferToSurface,
        SurfaceWalk,
        Falling
    }

    public enum WalkableSurfaceShape
    {
        Line = 1,
        Polyline = 2
    }

    public enum WalkableSurfaceKind
    {
        Rainbow = 1
    }

    public enum LittlePersonEmotion
    {
        Calm,
        Curious,
        Startled
    }

    public sealed class InteractionObject
    {
        public int Id { get; }
        public InteractionObjectKind Kind { get; }
        public Vector2 Position { get; private set; }
        public Vector2 Size { get; private set; }
        public float AngleDegrees { get; private set; }
        public float Height { get; private set; }
        public Vector2 Velocity { get; private set; }
        public InteractionObjectState State { get; private set; }
        public InteractionShapeKind ShapeKind { get; }
        public IReadOnlyList<Vector2> ContourPoints { get; }
        float lastTimestamp;

        public InteractionObject(
            int id,
            InteractionObjectKind kind,
            Vector2 position,
            Vector2 size,
            float angleDegrees,
            float height,
            InteractionObjectState state,
            InteractionShapeKind shapeKind = InteractionShapeKind.Primitive,
            IReadOnlyList<Vector2> contourPoints = null)
        {
            Id = id;
            Kind = kind;
            Position = Clamp01(position);
            Size = new Vector2(Mathf.Clamp(size.x, 0.015f, 0.7f), Mathf.Clamp(size.y, 0.015f, 0.7f));
            AngleDegrees = angleDegrees;
            Height = Mathf.Clamp01(height);
            State = state;
            ShapeKind = shapeKind;
            ContourPoints = BuildContourPoints(contourPoints);
        }

        static IReadOnlyList<Vector2> BuildContourPoints(IReadOnlyList<Vector2> source)
        {
            var points = new List<Vector2>();
            if (source != null)
            {
                foreach (var point in source)
                {
                    points.Add(Clamp01(point));
                }
            }
            return points;
        }

        public void MoveTo(Vector2 position, float timestamp)
        {
            position = Clamp01(position);
            var dt = timestamp - lastTimestamp;
            Velocity = dt > 0.0001f ? (position - Position) / dt : Vector2.zero;
            Position = position;
            lastTimestamp = timestamp;
            State = InteractionObjectState.Dragging;
        }

        public void Resize(float delta)
        {
            var aspect = Size.y <= 0.0001f ? 1f : Size.x / Size.y;
            var nextY = Mathf.Clamp(Size.y + delta, 0.025f, 0.45f);
            var nextX = Mathf.Clamp(nextY * aspect, 0.025f, 0.75f);

            if (Kind == InteractionObjectKind.MaskStroke)
            {
                nextX = Mathf.Clamp(Size.x + delta * 1.0f, 0.045f, 0.30f);
                nextY = Mathf.Clamp(Size.y + delta * 0.14f, 0.014f, 0.055f);
            }

            Size = new Vector2(nextX, nextY);
        }

        public void Rotate(float deltaDegrees)
        {
            AngleDegrees = Mathf.Repeat(AngleDegrees + deltaDegrees, 360f);
        }

        public void Release()
        {
            Velocity = Vector2.zero;
            State = InteractionObjectState.Placed;
        }

        public bool Contains(Vector2 normalizedPoint, float padding)
        {
            var local = Rotate(normalizedPoint - Position, -AngleDegrees);
            var half = Size * 0.5f + Vector2.one * padding;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        }

        public InteractionField CreateField(MasterDatabase masters)
        {
            var type = masters.GetObjectType(Kind);
            var fieldMaster = masters.InteractionFields.Get(type.InteractionFieldMasterId);
            return new InteractionField(
                Id,
                Kind,
                State,
                Velocity,
                fieldMaster.Kind,
                Position,
                Size,
                AngleDegrees,
                fieldMaster.Radius,
                fieldMaster.Strength,
                ShapeKind,
                ContourPoints);
        }

        static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        static Vector2 Rotate(Vector2 value, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }
    }

    public sealed class InteractionField
    {
        public int SourceObjectId { get; }
        public InteractionObjectKind SourceKind { get; }
        public InteractionObjectState SourceState { get; }
        public Vector2 SourceVelocity { get; }
        public InteractionFieldKind Kind { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public float AngleDegrees { get; }
        public float Radius { get; }
        public float Strength { get; }
        public InteractionShapeKind ShapeKind { get; }
        public IReadOnlyList<Vector2> ContourPoints { get; }

        public InteractionField(
            int sourceObjectId,
            InteractionObjectKind sourceKind,
            InteractionObjectState sourceState,
            Vector2 sourceVelocity,
            InteractionFieldKind kind,
            Vector2 position,
            Vector2 size,
            float angleDegrees,
            float radius,
            float strength,
            InteractionShapeKind shapeKind = InteractionShapeKind.Primitive,
            IReadOnlyList<Vector2> contourPoints = null)
        {
            SourceObjectId = sourceObjectId;
            SourceKind = sourceKind;
            SourceState = sourceState;
            SourceVelocity = sourceVelocity;
            Kind = kind;
            Position = position;
            Size = size;
            AngleDegrees = angleDegrees;
            Radius = radius;
            Strength = strength;
            ShapeKind = shapeKind;
            ContourPoints = BuildContourPoints(contourPoints);
        }

        public float DistanceTo(Vector2 point)
        {
            if (UsesContourShape)
            {
                return DistanceToContour(point);
            }

            return Vector2.Distance(point, Position);
        }

        bool UsesContourShape =>
            SourceKind == InteractionObjectKind.Hand &&
            ShapeKind == InteractionShapeKind.Contour &&
            ContourPoints.Count >= 3;

        float DistanceToContour(Vector2 point)
        {
            if (IsPointInsidePolygon(ContourPoints, point))
            {
                return 0f;
            }

            return Vector2.Distance(point, ClosestPointOnContour(point));
        }

        Vector2 ClosestPointOnContour(Vector2 point)
        {
            var closest = ContourPoints[0];
            var closestDistanceSqr = float.MaxValue;
            for (var i = 0; i < ContourPoints.Count; i++)
            {
                var a = ContourPoints[i];
                var b = ContourPoints[(i + 1) % ContourPoints.Count];
                var candidate = ClosestPointOnSegment(point, a, b);
                var distanceSqr = (candidate - point).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closest = candidate;
                    closestDistanceSqr = distanceSqr;
                }
            }

            return closest;
        }

        static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= 0.000001f)
            {
                return a;
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSqr);
            return a + segment * t;
        }

        static bool IsPointInsidePolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (var i = 0; i < polygon.Count; i++)
            {
                var j = (i + polygon.Count - 1) % polygon.Count;
                var current = polygon[i];
                var previous = polygon[j];
                var crossesY = current.y > point.y != previous.y > point.y;
                if (!crossesY)
                {
                    continue;
                }

                var intersectionX = (previous.x - current.x) *
                    (point.y - current.y) /
                    (previous.y - current.y + 0.000001f) +
                    current.x;
                if (point.x < intersectionX)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        static IReadOnlyList<Vector2> BuildContourPoints(IReadOnlyList<Vector2> source)
        {
            var points = new List<Vector2>();
            if (source == null)
            {
                return points;
            }

            foreach (var point in source)
            {
                points.Add(new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y)));
            }

            return points;
        }
    }

    public sealed class WalkableSurface
    {
        readonly List<Vector2> pathPoints = new();
        readonly List<float> cumulativeLengths = new();
        float totalLength;

        public int Id { get; }
        public int SourceObjectId { get; }
        public WalkableSurfaceShape Shape { get; }
        public WalkableSurfaceKind Kind { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public float Width { get; }
        public IReadOnlyList<Vector2> PathPoints => pathPoints;
        public bool AllowsNewAttachment { get; }
        public float AttachProgress { get; }
        public float ExitProgress { get; }
        public Vector2 AttachPoint => PositionAt(AttachProgress);
        public Vector2 PathEndPoint => PositionAt(ExitProgress);
        public float Length => Mathf.Max(0.001f, totalLength);

        public WalkableSurface(
            int id,
            int sourceObjectId,
            WalkableSurfaceShape shape,
            WalkableSurfaceKind kind,
            Vector2 start,
            Vector2 end,
            float width,
            float attachProgress,
            float exitProgress,
            bool allowsNewAttachment = true,
            IReadOnlyList<Vector2> points = null)
        {
            Id = id;
            SourceObjectId = sourceObjectId;
            Shape = shape;
            Kind = kind;
            if (points != null && points.Count >= 2)
            {
                pathPoints.AddRange(points);
            }
            else
            {
                pathPoints.Add(start);
                pathPoints.Add(end);
            }
            Start = pathPoints[0];
            End = pathPoints[pathPoints.Count - 1];
            Width = Mathf.Max(0.001f, width);
            AttachProgress = Mathf.Clamp(attachProgress, 0f, 0.98f);
            ExitProgress = Mathf.Clamp(exitProgress, AttachProgress + 0.001f, 1f);
            AllowsNewAttachment = allowsNewAttachment;
            RebuildPathLengths();
        }

        public Vector2 PositionAt(float progress)
        {
            var targetDistance = Mathf.Clamp01(progress) * totalLength;
            for (var i = 1; i < pathPoints.Count; i++)
            {
                if (cumulativeLengths[i] < targetDistance)
                {
                    continue;
                }

                var segmentStartDistance = cumulativeLengths[i - 1];
                var segmentLength = Mathf.Max(0.000001f, cumulativeLengths[i] - segmentStartDistance);
                var segmentProgress = Mathf.Clamp01((targetDistance - segmentStartDistance) / segmentLength);
                return Vector2.Lerp(pathPoints[i - 1], pathPoints[i], segmentProgress);
            }

            return End;
        }

        public Vector2 Tangent()
        {
            return TangentAt(ExitProgress);
        }

        public Vector2 TangentAt(float progress)
        {
            var targetDistance = Mathf.Clamp01(progress) * totalLength;
            for (var i = 1; i < pathPoints.Count; i++)
            {
                if (cumulativeLengths[i] + 0.000001f < targetDistance)
                {
                    continue;
                }

                var tangent = pathPoints[i] - pathPoints[i - 1];
                if (tangent.sqrMagnitude > 0.000001f)
                {
                    return tangent.normalized;
                }
            }

            var fallback = End - Start;
            return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector2.right;
        }

        void RebuildPathLengths()
        {
            cumulativeLengths.Clear();
            cumulativeLengths.Add(0f);
            totalLength = 0f;
            for (var i = 1; i < pathPoints.Count; i++)
            {
                totalLength += Vector2.Distance(pathPoints[i - 1], pathPoints[i]);
                cumulativeLengths.Add(totalLength);
            }
        }

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

    public sealed class SensorFrame
    {
        public long Frame { get; }
        public float TimestampSeconds { get; }
        public IReadOnlyList<InteractionObject> Objects { get; }

        public SensorFrame(long frame, float timestampSeconds, IReadOnlyList<InteractionObject> objects)
        {
            Frame = frame;
            TimestampSeconds = timestampSeconds;
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }
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
    public sealed class RainbowInstance
    {
        const int SurfaceIdBase = 1000000;
        const int SourceIdBase = 2000000;
        readonly List<Vector2> pathPoints = new();
        readonly List<Vector2> reversedPathPoints = new();

        public int Id { get; }
        public int SourceCloudId { get; }
        public RainbowState State { get; private set; }
        public Vector2 LeftFoot { get; }
        public Vector2 RightFoot { get; }
        public Vector2 Apex { get; }
        public IReadOnlyList<Vector2> PathPoints => pathPoints;
        public float DurationSeconds { get; }
        public float AppearDurationSeconds { get; }
        public float FadeDurationSeconds { get; }
        public float AgeSeconds { get; private set; }
        public float RemainingSeconds => Mathf.Max(0f, DurationSeconds - AgeSeconds);
        public bool IsExpired => State == RainbowState.Expired;
        public bool AllowsNewAttachment => State == RainbowState.Active;

        public float Opacity
        {
            get
            {
                if (State == RainbowState.Expired)
                {
                    return 0f;
                }

                if (AppearDurationSeconds > 0f && AgeSeconds < AppearDurationSeconds)
                {
                    return Mathf.Clamp01(AgeSeconds / AppearDurationSeconds);
                }

                if (FadeDurationSeconds > 0f && RemainingSeconds <= FadeDurationSeconds)
                {
                    return Mathf.Clamp01(RemainingSeconds / FadeDurationSeconds);
                }

                return 1f;
            }
        }

        public RainbowInstance(
            int id,
            int sourceCloudId,
            Vector2 cloudPosition,
            Vector2 sunPosition,
            float edgePadding,
            RainbowMaster master)
        {
            if (master == null)
            {
                throw new ArgumentNullException(nameof(master));
            }

            Id = id;
            SourceCloudId = sourceCloudId;
            DurationSeconds = master.DurationSeconds;
            AppearDurationSeconds = master.AppearDurationSeconds;
            FadeDurationSeconds = master.FadeDurationSeconds;
            State = RainbowState.Appearing;

            var groundY = Mathf.Clamp01(1f - edgePadding);
            var halfSpan = master.SpanNormalized * 0.5f;
            var minCenterX = edgePadding + halfSpan;
            var maxCenterX = 1f - edgePadding - halfSpan;
            var requestedCenterX = 0.5f;
            var centerX = minCenterX <= maxCenterX
                ? Mathf.Clamp(requestedCenterX, minCenterX, maxCenterX)
                : 0.5f;

            LeftFoot = new Vector2(centerX - halfSpan, groundY);
            RightFoot = new Vector2(centerX + halfSpan, groundY);
            Apex = new Vector2(centerX, Mathf.Max(edgePadding + 0.04f, groundY - master.RiseNormalized));
            var control = new Vector2(centerX, Apex.y * 2f - groundY);

            for (var i = 0; i <= master.PathSegmentCount; i++)
            {
                var t = i / (float)master.PathSegmentCount;
                var inverse = 1f - t;
                pathPoints.Add(
                    inverse * inverse * LeftFoot +
                    2f * inverse * t * control +
                    t * t * RightFoot);
            }

            for (var i = pathPoints.Count - 1; i >= 0; i--)
            {
                reversedPathPoints.Add(pathPoints[i]);
            }
        }

        public void Advance(float deltaTime)
        {
            AgeSeconds += Mathf.Max(0f, deltaTime);
            if (AgeSeconds >= DurationSeconds)
            {
                State = RainbowState.Expired;
            }
            else if (RemainingSeconds <= FadeDurationSeconds)
            {
                State = RainbowState.Fading;
            }
            else if (AgeSeconds < AppearDurationSeconds)
            {
                State = RainbowState.Appearing;
            }
            else
            {
                State = RainbowState.Active;
            }
        }

        public void AddWalkableSurfaces(List<WalkableSurface> destination, RainbowMaster master)
        {
            if (destination == null || master == null || IsExpired)
            {
                return;
            }

            AddSurface(destination, master, pathPoints, SurfaceIdBase + Id * 10 + 1);
            AddSurface(destination, master, reversedPathPoints, SurfaceIdBase + Id * 10 + 2);
        }

        void AddSurface(
            List<WalkableSurface> destination,
            RainbowMaster master,
            IReadOnlyList<Vector2> points,
            int surfaceId)
        {
            var start = points[0];
            var end = points[points.Count - 1];
            destination.Add(new WalkableSurface(
                surfaceId,
                SourceIdBase + Id,
                WalkableSurfaceShape.Polyline,
                WalkableSurfaceKind.Rainbow,
                start,
                end,
                master.SurfaceWidth,
                0f,
                1f,
                AllowsNewAttachment,
                points));
        }
    }


    public sealed class LittlePerson
    {
        readonly System.Random random;
        float wanderTimer;
        float edgeProgress;
        int edgeDirection;
        int surfaceId = -1;
        int surfaceSourceObjectId = -1;
        WalkableSurfaceKind? activeSurfaceKind;
        float surfaceProgress;
        bool surfaceExitReached;
        float surfaceExitDwellTimer;
        Vector2 transferStart;
        Vector2 transferEnd;
        float transferTimer;
        float transferDurationSeconds;
        float transferTargetProgress;
        Vector2 fallStart;
        Vector2 fallControl;
        Vector2 fallEnd;
        float fallTimer;
        int fallSourceObjectId = -1;
        int fallExitEdgeDirection = 1;
        int reconnectCooldownSourceObjectId = -1;
        float reconnectCooldownTimer;

        public Guid Id { get; }
        public int ArchetypeId { get; }
        public int BehaviorProfileId { get; }
        public int PreferenceSeed { get; }
        public Vector2 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public LittlePersonBehaviorKind CurrentBehavior { get; private set; }
        public LittlePersonEmotion Emotion { get; private set; }
        public int TargetObjectId { get; private set; }
        public ReactionInstance CurrentReaction { get; private set; }
        public float EdgeProgress => edgeProgress;
        public int SurfaceId => surfaceId;
        public WalkableSurfaceKind? ActiveSurfaceKind => activeSurfaceKind;

        public LittlePerson(
            Guid id,
            int archetypeId,
            int behaviorProfileId,
            int preferenceSeed,
            float edgeProgress,
            int edgeDirection,
            float edgePadding)
        {
            Id = id;
            ArchetypeId = archetypeId;
            BehaviorProfileId = behaviorProfileId;
            PreferenceSeed = preferenceSeed;
            this.edgeProgress = Mathf.Repeat(edgeProgress, 1f);
            this.edgeDirection = edgeDirection < 0 ? -1 : 1;
            Position = PositionOnEdge(this.edgeProgress, edgePadding);
            CurrentBehavior = LittlePersonBehaviorKind.EdgeWalk;
            Emotion = LittlePersonEmotion.Calm;
            TargetObjectId = -1;
            random = new System.Random(preferenceSeed);
            wanderTimer = Mathf.Lerp(1.2f, 3.4f, (float)random.NextDouble());
        }

        public void Advance(
            float deltaTime,
            IReadOnlyList<InteractionField> fields,
            IReadOnlyList<WalkableSurface> surfaces,
            IReadOnlyList<LittlePerson> neighbors,
            MasterDatabase masters,
            TuningParameterMaster tuning)
        {
            var archetype = masters.LittlePersonArchetypes.Get(ArchetypeId);
            var profile = masters.BehaviorProfiles.Get(BehaviorProfileId);

            CurrentReaction?.Advance(deltaTime);
            if (CurrentReaction != null && CurrentReaction.IsExpired)
            {
                CurrentReaction = null;
            }

            AdvanceReconnectCooldown(deltaTime);

            if (TryDropFromTouchedRainbow(fields, surfaces, masters, tuning))
            {
                AdvanceFalling(deltaTime, tuning);
                return;
            }

            switch (CurrentBehavior)
            {
                case LittlePersonBehaviorKind.TransferToSurface:
                    AdvanceTransferToSurface(deltaTime, surfaces, masters, tuning);
                    break;
                case LittlePersonBehaviorKind.SurfaceWalk:
                    AdvanceSurfaceMotion(deltaTime, surfaces, masters, tuning);
                    break;
                case LittlePersonBehaviorKind.Falling:
                    AdvanceFalling(deltaTime, tuning);
                    break;
                default:
                    AdvanceEdgeWalk(deltaTime, fields, surfaces, archetype, profile, masters, tuning);
                    break;
            }
        }

        public void HoldPosition(float deltaTime)
        {
            CurrentReaction?.Advance(deltaTime);
            if (CurrentReaction != null && CurrentReaction.IsExpired)
            {
                CurrentReaction = null;
            }

            AdvanceReconnectCooldown(deltaTime);
            Velocity = Vector2.zero;
        }

        void AdvanceEdgeWalk(
            float deltaTime,
            IReadOnlyList<InteractionField> fields,
            IReadOnlyList<WalkableSurface> surfaces,
            LittlePersonArchetypeMaster archetype,
            BehaviorProfileMaster profile,
            MasterDatabase masters,
            TuningParameterMaster tuning)
        {
            AdvanceReconnectCooldown(deltaTime);

            CurrentBehavior = LittlePersonBehaviorKind.EdgeWalk;
            Emotion = LittlePersonEmotion.Calm;
            TargetObjectId = -1;
            surfaceId = -1;
            surfaceSourceObjectId = -1;
            activeSurfaceKind = null;
            surfaceExitReached = false;
            surfaceExitDwellTimer = 0f;

            if (TryStartSurfaceTransfer(surfaces, masters, tuning))
            {
                AdvanceTransferToSurface(deltaTime, surfaces, masters, tuning);
                return;
            }

            ReactToEdgeNearbyFields(fields, masters, tuning);
            AdvanceEdgeDirectionTimer(deltaTime, profile);

            var previous = Position;
            var pathLength = EdgePathLength(tuning.WorldEdgePadding);
            var nextProgress = Mathf.Repeat(edgeProgress + edgeDirection * archetype.MoveSpeed * deltaTime / pathLength, 1f);
            var nextPosition = PositionOnEdge(nextProgress, tuning.WorldEdgePadding);
            edgeProgress = nextProgress;
            Position = nextPosition;
            Velocity = deltaTime > 0.0001f ? (Position - previous) / deltaTime : Vector2.zero;
        }

        bool TryDropFromTouchedRainbow(
            IReadOnlyList<InteractionField> fields,
            IReadOnlyList<WalkableSurface> surfaces,
            MasterDatabase masters,
            TuningParameterMaster tuning)
        {
            if (CurrentBehavior != LittlePersonBehaviorKind.TransferToSurface &&
                CurrentBehavior != LittlePersonBehaviorKind.SurfaceWalk)
            {
                return false;
            }

            var surface = FindSurfaceById(surfaces, surfaceId);
            if (surface == null || surface.Kind != WalkableSurfaceKind.Rainbow || fields == null)
            {
                return false;
            }

            var rainbowMaster = masters.Rainbows.Get(1);
            foreach (var field in fields)
            {
                if (field.SourceKind != InteractionObjectKind.Hand)
                {
                    continue;
                }

                var touchDistance = field.ShapeKind == InteractionShapeKind.Contour
                    ? rainbowMaster.TouchPadding
                    : field.Radius;
                if (field.DistanceTo(Position) <= touchDistance)
                {
                    StartFalling(tuning, Position);
                    return true;
                }
            }

            return false;
        }

        bool TryStartSurfaceTransfer(IReadOnlyList<WalkableSurface> surfaces, MasterDatabase masters, TuningParameterMaster tuning)
        {
            if (surfaces == null)
            {
                return false;
            }

            var rainbowMaster = masters.Rainbows.Get(1);
            WalkableSurface selected = null;
            Vector2 selectedPoint = Vector2.zero;
            float selectedProgress = 0f;
            var selectedDistance = float.MaxValue;

            foreach (var surface in surfaces)
            {
                if (surface.Kind != WalkableSurfaceKind.Rainbow || !surface.AllowsNewAttachment)
                {
                    continue;
                }

                if (reconnectCooldownTimer > 0f && surface.SourceObjectId == reconnectCooldownSourceObjectId)
                {
                    continue;
                }

                var closestPoint = surface.AttachPoint;
                var progress = surface.AttachProgress;
                var distance = Vector2.Distance(Position, closestPoint);
                if (distance <= rainbowMaster.AttachDistance && distance < selectedDistance)
                {
                    selected = surface;
                    selectedPoint = closestPoint;
                    selectedProgress = progress;
                    selectedDistance = distance;
                }
            }

            if (selected == null)
            {
                return false;
            }

            surfaceId = selected.Id;
            surfaceSourceObjectId = selected.SourceObjectId;
            activeSurfaceKind = selected.Kind;
            surfaceProgress = selectedProgress;
            surfaceExitReached = false;
            surfaceExitDwellTimer = 0f;
            transferStart = Position;
            transferEnd = selectedPoint;
            transferTimer = 0f;
            transferDurationSeconds = rainbowMaster.TransferDurationSeconds;
            transferTargetProgress = selectedProgress;
            TargetObjectId = selected.SourceObjectId;
            CurrentBehavior = LittlePersonBehaviorKind.TransferToSurface;
            Emotion = LittlePersonEmotion.Curious;
            EnsureReaction(masters.Reactions.Get(4));
            Velocity = Vector2.zero;
            return true;
        }

        void AdvanceTransferToSurface(
            float deltaTime,
            IReadOnlyList<WalkableSurface> surfaces,
            MasterDatabase masters,
            TuningParameterMaster tuning)
        {
            var surface = FindSurfaceById(surfaces, surfaceId);
            if (surface == null)
            {
                StartFalling(tuning, Position);
                AdvanceFalling(deltaTime, tuning);
                return;
            }

            surfaceProgress = Mathf.Clamp(transferTargetProgress, surface.AttachProgress, surface.ExitProgress);
            transferEnd = surface.PositionAt(surfaceProgress);
            CurrentBehavior = LittlePersonBehaviorKind.TransferToSurface;
            Emotion = LittlePersonEmotion.Curious;
            TargetObjectId = surface.SourceObjectId;
            surfaceSourceObjectId = surface.SourceObjectId;
            activeSurfaceKind = surface.Kind;
            EnsureReaction(masters.Reactions.Get(4));

            var previous = Position;
            transferTimer += Mathf.Max(0f, deltaTime);
            var duration = Mathf.Max(0.001f, transferDurationSeconds);
            var t = Mathf.Clamp01(transferTimer / duration);
            Position = Vector2.Lerp(transferStart, transferEnd, Mathf.SmoothStep(0f, 1f, t));
            Velocity = deltaTime > 0.0001f ? (Position - previous) / deltaTime : Vector2.zero;

            if (t >= 1f)
            {
                Position = transferEnd;
                CurrentBehavior = LittlePersonBehaviorKind.SurfaceWalk;
            }
        }

        void AdvanceSurfaceMotion(
            float deltaTime,
            IReadOnlyList<WalkableSurface> surfaces,
            MasterDatabase masters,
            TuningParameterMaster tuning)
        {
            var surface = FindSurfaceById(surfaces, surfaceId);
            if (surface == null)
            {
                StartFalling(tuning, Position);
                AdvanceFalling(deltaTime, tuning);
                return;
            }

            TargetObjectId = surface.SourceObjectId;
            surfaceSourceObjectId = surface.SourceObjectId;
            activeSurfaceKind = surface.Kind;
            Emotion = LittlePersonEmotion.Curious;
            EnsureReaction(masters.Reactions.Get(4));

            var previous = Position;
            if (surfaceExitReached)
            {
                Position = surface.PathEndPoint;
                Velocity = deltaTime > 0.0001f ? (Position - previous) / deltaTime : Vector2.zero;
                surfaceExitDwellTimer += Mathf.Max(0f, deltaTime);

                var exitDwellSeconds = masters.Rainbows.Get(1).ExitDwellSeconds;
                if (surfaceExitDwellTimer >= exitDwellSeconds)
                {
                    CompleteRainbowWalk(surface, tuning, masters);
                }

                return;
            }

            CurrentBehavior = LittlePersonBehaviorKind.SurfaceWalk;
            var walkDistance = masters.Rainbows.Get(1).WalkSpeed * Mathf.Max(0f, deltaTime);
            surfaceProgress = Mathf.Min(surface.ExitProgress, surfaceProgress + walkDistance / surface.Length);

            if (surfaceProgress >= surface.ExitProgress)
            {
                var pathEndPoint = surface.PathEndPoint;
                Position = pathEndPoint;
                Velocity = deltaTime > 0.0001f ? (Position - previous) / deltaTime : Vector2.zero;
                surfaceProgress = surface.ExitProgress;
                surfaceExitReached = true;
                surfaceExitDwellTimer = 0f;
                return;
            }

            var next = surface.PositionAt(surfaceProgress);
            var detachDistance = masters.Rainbows.Get(1).DetachDistance;
            if (Vector2.Distance(previous, next) > detachDistance)
            {
                StartFalling(tuning, previous);
                AdvanceFalling(deltaTime, tuning);
                return;
            }

            Position = next;
            Velocity = deltaTime > 0.0001f ? (Position - previous) / deltaTime : Vector2.zero;
        }
        void CompleteRainbowWalk(WalkableSurface surface, TuningParameterMaster tuning, MasterDatabase masters)
        {
            edgeProgress = ClosestProgressOnEdge(surface.PathEndPoint, tuning.WorldEdgePadding, out var edgePoint);
            Position = edgePoint;
            Velocity = Vector2.zero;
            CurrentBehavior = LittlePersonBehaviorKind.EdgeWalk;
            Emotion = LittlePersonEmotion.Calm;
            TargetObjectId = -1;
            surfaceId = -1;
            surfaceSourceObjectId = -1;
            activeSurfaceKind = null;
            surfaceExitReached = false;
            surfaceExitDwellTimer = 0f;
            reconnectCooldownSourceObjectId = surface.SourceObjectId;
            reconnectCooldownTimer = masters.Rainbows.Get(1).ReconnectCooldownSeconds;

            var tangent = surface.Tangent();
            if (Mathf.Abs(tangent.x) > 0.0001f)
            {
                edgeDirection = tangent.x >= 0f ? -1 : 1;
            }
        }


        void AdvanceFalling(float deltaTime, TuningParameterMaster tuning)
        {
            CurrentBehavior = LittlePersonBehaviorKind.Falling;
            Emotion = LittlePersonEmotion.Startled;

            var previous = Position;
            fallTimer = Mathf.Min(tuning.FallDuration, fallTimer + deltaTime);
            var t = Mathf.Clamp01(fallTimer / tuning.FallDuration);
            Position = QuadraticBezier(fallStart, fallControl, fallEnd, t);
            Velocity = deltaTime > 0.0001f ? (Position - previous) / deltaTime : Vector2.zero;

            if (t >= 1f)
            {
                edgeProgress = ClosestProgressOnEdge(fallEnd, tuning.WorldEdgePadding, out var edgePoint);
                Position = edgePoint;
                Velocity = Vector2.zero;
                CurrentBehavior = LittlePersonBehaviorKind.EdgeWalk;
                Emotion = LittlePersonEmotion.Calm;
                TargetObjectId = -1;
                surfaceId = -1;
                surfaceSourceObjectId = -1;
                activeSurfaceKind = null;
                edgeDirection = fallExitEdgeDirection;
                reconnectCooldownSourceObjectId = fallSourceObjectId;
                reconnectCooldownTimer = tuning.SurfaceReconnectCooldownSeconds;
                fallSourceObjectId = -1;
            }
        }

        void StartFalling(TuningParameterMaster tuning, Vector2 startPosition)
        {
            fallStart = startPosition;
            Position = fallStart;
            fallSourceObjectId = surfaceSourceObjectId;
            var nearestProgress = ClosestProgressOnEdge(fallStart, tuning.WorldEdgePadding, out _);
            fallExitEdgeDirection = ChooseFallExitDirection(nearestProgress, tuning);
            var shiftedProgress = nearestProgress + fallExitEdgeDirection * tuning.FallLateralDistance / EdgePathLength(tuning.WorldEdgePadding);
            fallEnd = PositionOnEdge(shiftedProgress, tuning.WorldEdgePadding);
            fallControl = BuildFallControlPoint(fallStart, fallEnd, tuning);
            fallTimer = 0f;
            CurrentBehavior = LittlePersonBehaviorKind.Falling;
            Emotion = LittlePersonEmotion.Startled;
            TargetObjectId = -1;
            surfaceId = -1;
            surfaceSourceObjectId = -1;
            activeSurfaceKind = null;
            surfaceExitReached = false;
            surfaceExitDwellTimer = 0f;
        }

        void AdvanceReconnectCooldown(float deltaTime)
        {
            if (reconnectCooldownTimer <= 0f)
            {
                return;
            }

            reconnectCooldownTimer = Mathf.Max(0f, reconnectCooldownTimer - Mathf.Max(0f, deltaTime));
            if (reconnectCooldownTimer <= 0f)
            {
                reconnectCooldownSourceObjectId = -1;
            }
        }

        int ChooseFallExitDirection(float nearestProgress, TuningParameterMaster tuning)
        {
            var pathLength = EdgePathLength(tuning.WorldEdgePadding);
            var sampleDistance = 0.01f / pathLength;
            var forward = PositionOnEdge(nearestProgress + sampleDistance, tuning.WorldEdgePadding);
            var backward = PositionOnEdge(nearestProgress - sampleDistance, tuning.WorldEdgePadding);
            var edgeTangent = (forward - backward).normalized;
            var projected = Vector2.Dot(Velocity, edgeTangent);

            if (Mathf.Abs(projected) > 0.001f)
            {
                return projected >= 0f ? 1 : -1;
            }

            return (PreferenceSeed & 1) == 0 ? 1 : -1;
        }

        Vector2 BuildFallControlPoint(Vector2 start, Vector2 end, TuningParameterMaster tuning)
        {
            var launchDirection = Velocity.sqrMagnitude > 0.000001f ? Velocity.normalized : (start - end).normalized;
            if (launchDirection.sqrMagnitude <= 0.000001f)
            {
                launchDirection = new Vector2(fallExitEdgeDirection, -1f).normalized;
            }

            return start + launchDirection * tuning.FallLaunchDistance + (end - start) * 0.24f;
        }

        static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        void EnsureReaction(ReactionMaster master)
        {
            if (CurrentReaction == null || CurrentReaction.ReactionMasterId != master.Id)
            {
                CurrentReaction = new ReactionInstance(master);
            }
        }

        void ReactToEdgeNearbyFields(IReadOnlyList<InteractionField> fields, MasterDatabase masters, TuningParameterMaster tuning)
        {
            foreach (var field in fields)
            {
                if (field.SourceKind == InteractionObjectKind.Hand)
                {
                    var reactionDistance = field.ShapeKind == InteractionShapeKind.Contour && field.ContourPoints.Count >= 3
                        ? tuning.HandContourReactionPadding
                        : field.Radius;
                    if (field.DistanceTo(Position) <= reactionDistance)
                    {
                        edgeDirection *= -1;
                        Emotion = LittlePersonEmotion.Startled;
                        EnsureReaction(masters.Reactions.Get(2));
                        return;
                    }
                }

                if (field.SourceKind == InteractionObjectKind.RoundProp && field.DistanceTo(Position) <= field.Radius)
                {
                    Emotion = LittlePersonEmotion.Curious;
                    EnsureReaction(masters.Reactions.Get(3));
                }
            }
        }

        void AdvanceEdgeDirectionTimer(float deltaTime, BehaviorProfileMaster profile)
        {
            wanderTimer -= deltaTime;
            if (wanderTimer > 0f)
            {
                return;
            }

            wanderTimer = profile.WanderTurnInterval * Mathf.Lerp(0.75f, 1.8f, (float)random.NextDouble());
            if (random.NextDouble() < 0.18)
            {
                edgeDirection *= -1;
            }
        }

        static WalkableSurface FindSurfaceById(IReadOnlyList<WalkableSurface> surfaces, int id)
        {
            if (surfaces == null || id < 0)
            {
                return null;
            }

            foreach (var surface in surfaces)
            {
                if (surface.Id == id)
                {
                    return surface;
                }
            }

            return null;
        }

        static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        public static float EdgePathLength(float padding)
        {
            var width = Mathf.Max(0.001f, 1f - padding * 2f);
            var height = Mathf.Max(0.001f, 1f - padding * 2f);
            return (width + height) * 2f;
        }

        public static Vector2 PositionOnEdge(float progress, float padding)
        {
            var width = Mathf.Max(0.001f, 1f - padding * 2f);
            var height = Mathf.Max(0.001f, 1f - padding * 2f);
            var perimeter = (width + height) * 2f;
            var distance = Mathf.Repeat(progress, 1f) * perimeter;

            if (distance <= width)
            {
                return new Vector2(padding + distance, padding);
            }

            distance -= width;
            if (distance <= height)
            {
                return new Vector2(1f - padding, padding + distance);
            }

            distance -= height;
            if (distance <= width)
            {
                return new Vector2(1f - padding - distance, 1f - padding);
            }

            distance -= width;
            return new Vector2(padding, 1f - padding - distance);
        }

        public static float ClosestProgressOnEdge(Vector2 point, float padding, out Vector2 closestPoint)
        {
            var width = Mathf.Max(0.001f, 1f - padding * 2f);
            var height = Mathf.Max(0.001f, 1f - padding * 2f);
            var perimeter = (width + height) * 2f;
            var bestDistance = float.MaxValue;
            var bestProgressDistance = 0f;
            var bestPoint = new Vector2(padding, padding);

            ConsiderEdgePoint(
                point,
                new Vector2(Mathf.Clamp(point.x, padding, 1f - padding), padding),
                Mathf.Clamp(point.x - padding, 0f, width),
                ref bestDistance,
                ref bestProgressDistance,
                ref bestPoint);

            ConsiderEdgePoint(
                point,
                new Vector2(1f - padding, Mathf.Clamp(point.y, padding, 1f - padding)),
                width + Mathf.Clamp(point.y - padding, 0f, height),
                ref bestDistance,
                ref bestProgressDistance,
                ref bestPoint);

            ConsiderEdgePoint(
                point,
                new Vector2(Mathf.Clamp(point.x, padding, 1f - padding), 1f - padding),
                width + height + Mathf.Clamp(1f - padding - point.x, 0f, width),
                ref bestDistance,
                ref bestProgressDistance,
                ref bestPoint);

            ConsiderEdgePoint(
                point,
                new Vector2(padding, Mathf.Clamp(point.y, padding, 1f - padding)),
                width + height + width + Mathf.Clamp(1f - padding - point.y, 0f, height),
                ref bestDistance,
                ref bestProgressDistance,
                ref bestPoint);

            closestPoint = bestPoint;
            return Mathf.Repeat(bestProgressDistance / perimeter, 1f);
        }

        static void ConsiderEdgePoint(
            Vector2 point,
            Vector2 candidate,
            float progressDistance,
            ref float bestDistance,
            ref float bestProgressDistance,
            ref Vector2 bestPoint)
        {
            var distance = Vector2.SqrMagnitude(point - candidate);
            if (distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            bestProgressDistance = progressDistance;
            bestPoint = candidate;
        }
    }

    public sealed class World
    {
        readonly List<LittlePerson> littlePeople = new();
        readonly List<InteractionObject> interactionObjects = new();
        readonly List<InteractionField> interactionFields = new();
        readonly List<WalkableSurface> walkableSurfaces = new();
        readonly List<AmbientObject> ambientObjects = new();
        readonly List<VisualEffectInstance> visualEffects = new();
        readonly List<RainbowInstance> rainbows = new();
        readonly HashSet<Guid> movementPausedLittlePersonIds = new();
        int nextVisualEffectId = 1;
        int nextRainbowId = 1;
        int rainbowSpawnSequence;
        int nextDevelopmentRainSourceId = -100000;
        int activeBloomCount;
        bool rainbowConditionLatched;
        float rainbowCooldownSeconds;

        public IReadOnlyList<LittlePerson> LittlePeople => littlePeople;
        public IReadOnlyList<InteractionObject> InteractionObjects => interactionObjects;
        public IReadOnlyList<InteractionField> InteractionFields => interactionFields;
        public IReadOnlyList<WalkableSurface> WalkableSurfaces => walkableSurfaces;
        public IReadOnlyList<AmbientObject> AmbientObjects => ambientObjects;
        public IReadOnlyList<VisualEffectInstance> VisualEffects => visualEffects;
        public IReadOnlyList<RainbowInstance> Rainbows => rainbows;
        public int RainbowSpawnSequence => rainbowSpawnSequence;

        public void SetMovementPausedLittlePeople(IReadOnlyCollection<Guid> personIds)
        {
            movementPausedLittlePersonIds.Clear();
            if (personIds == null)
            {
                return;
            }

            foreach (var personId in personIds)
            {
                movementPausedLittlePersonIds.Add(personId);
            }
        }
        public void SetActiveBloomCount(int count)
        {
            activeBloomCount = Math.Max(0, count);
        }


        public static World Create(MasterDatabase masters, int worldPresetId)
        {
            var preset = masters.WorldPresets.Get(worldPresetId);
            var tuning = masters.TuningParameters.Get(1);
            var world = new World();
            var random = new System.Random(1367);

            for (var i = 0; i < preset.InitialLittlePersonCount; i++)
            {
                var archetypeId = 1 + i % 4;
                var edgeProgress = (float)random.NextDouble();
                var edgeDirection = random.NextDouble() < 0.5 ? -1 : 1;
                world.littlePeople.Add(new LittlePerson(
                    Guid.NewGuid(),
                    archetypeId,
                    preset.DefaultBehaviorProfileId,
                    random.Next(),
                    edgeProgress,
                    edgeDirection,
                    tuning.WorldEdgePadding));
            }

            world.CreateAmbientObjects(masters, tuning);
            return world;
        }

        public void SetInteractionObjects(IReadOnlyList<InteractionObject> objects, MasterDatabase masters)
        {
            interactionObjects.Clear();
            interactionFields.Clear();
            walkableSurfaces.Clear();

            if (objects != null)
            {
                foreach (var interactionObject in objects)
                {
                    interactionObjects.Add(interactionObject);
                    interactionFields.Add(interactionObject.CreateField(masters));
                }
            }

            RebuildRainbowSurfaces(masters);
        }

        public bool TriggerDevelopmentRainbow(MasterDatabase masters)
        {
            if (masters == null || rainbows.Count > 0)
            {
                return false;
            }

            AmbientObject sun = null;
            AmbientObject selectedCloud = null;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind == AmbientObjectKind.Star)
                {
                    sun = ambientObject;
                    break;
                }
            }

            if (sun == null)
            {
                return false;
            }

            var bestDistance = -1f;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind != AmbientObjectKind.Cloud)
                {
                    continue;
                }

                var distance = Vector2.Distance(ambientObject.Position, sun.Position);
                if (distance > bestDistance)
                {
                    selectedCloud = ambientObject;
                    bestDistance = distance;
                }
            }

            if (selectedCloud == null)
            {
                return false;
            }

            var tuning = masters.TuningParameters.Get(1);
            rainbows.Add(new RainbowInstance(
                nextRainbowId++,
                selectedCloud.Id,
                selectedCloud.Position,
                sun.Position,
                tuning.WorldEdgePadding,
                masters.Rainbows.Get(1)));
            rainbowSpawnSequence++;
            rainbowConditionLatched = true;
            rainbowCooldownSeconds = 0f;
            RebuildRainbowSurfaces(masters);
            return true;
        }

        public void TriggerDevelopmentRain(MasterDatabase masters, Vector2 position, float width, float durationSeconds)
        {
            if (masters == null)
            {
                return;
            }

            var effectMaster = masters.VisualEffects.Get((int)VisualEffectKind.RainColumn);
            var rainPosition = Clamp01(position);
            var heightToGround = Mathf.Max(0.05f, 1f - rainPosition.y);
            var size = new Vector2(Mathf.Max(effectMaster.DefaultSize.x, width), heightToGround);
            RefreshOrCreateVisualEffect(
                effectMaster.Id,
                nextDevelopmentRainSourceId--,
                rainPosition,
                size,
                0f,
                Mathf.Max(0.05f, durationSeconds));
        }

        public bool MarkCloudTouchedByExternalSource(int ambientObjectId, float lingerSeconds)
        {
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Id == ambientObjectId && ambientObject.Kind == AmbientObjectKind.Cloud)
                {
                    ambientObject.MarkCloudTouched(lingerSeconds);
                    return true;
                }
            }

            return false;
        }

        public void Advance(float deltaTime, MasterDatabase masters)
        {
            var tuning = masters.TuningParameters.Get(1);
            deltaTime = Mathf.Min(Mathf.Max(0f, deltaTime), tuning.MaxDeltaTime);

            foreach (var person in littlePeople)
            {
                if (movementPausedLittlePersonIds.Contains(person.Id) &&
                    person.CurrentBehavior == LittlePersonBehaviorKind.EdgeWalk)
                {
                    person.HoldPosition(deltaTime);
                    continue;
                }

                person.Advance(deltaTime, interactionFields, walkableSurfaces, littlePeople, masters, tuning);
            }

            foreach (var ambientObject in ambientObjects)
            {
                ambientObject.Advance(deltaTime);
            }

            UpdateAmbientReactions(masters, tuning);
            AdvanceVisualEffects(deltaTime);
            AdvanceRainbows(deltaTime, masters);
            UpdateRainbowTrigger(masters, tuning);
            RebuildRainbowSurfaces(masters);
        }
        void AdvanceRainbows(float deltaTime, MasterDatabase masters)
        {
            var rainbowMaster = masters.Rainbows.Get(1);
            rainbowCooldownSeconds = Mathf.Max(0f, rainbowCooldownSeconds - Mathf.Max(0f, deltaTime));

            for (var i = rainbows.Count - 1; i >= 0; i--)
            {
                rainbows[i].Advance(deltaTime);
                if (!rainbows[i].IsExpired)
                {
                    continue;
                }

                rainbows.RemoveAt(i);
                rainbowCooldownSeconds = Mathf.Max(rainbowCooldownSeconds, rainbowMaster.CooldownSeconds);
            }
        }

        void UpdateRainbowTrigger(MasterDatabase masters, TuningParameterMaster tuning)
        {
            var rainbowMaster = masters.Rainbows.Get(1);
            var hasDistantRainingCloud =
                TryFindDistantRainingCloud(rainbowMaster, out var sourceCloud, out var sun);
            var conditionMet = activeBloomCount >= rainbowMaster.RequiredBloomCount && hasDistantRainingCloud;

            if (!conditionMet)
            {
                rainbowConditionLatched = false;
                return;
            }

            if (rainbowConditionLatched || rainbowCooldownSeconds > 0f || rainbows.Count > 0)
            {
                return;
            }

            rainbows.Add(new RainbowInstance(
                nextRainbowId++,
                sourceCloud.Id,
                sourceCloud.Position,
                sun.Position,
                tuning.WorldEdgePadding,
                rainbowMaster));
            rainbowSpawnSequence++;
            rainbowConditionLatched = true;
        }

        bool TryFindDistantRainingCloud(
            RainbowMaster master,
            out AmbientObject selectedCloud,
            out AmbientObject sun)
        {
            selectedCloud = null;
            sun = null;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind == AmbientObjectKind.Star)
                {
                    sun = ambientObject;
                    break;
                }
            }

            if (sun == null)
            {
                return false;
            }

            var selectedDistance = master.MinCloudSunDistance;
            foreach (var ambientObject in ambientObjects)
            {
                if (ambientObject.Kind != AmbientObjectKind.Cloud || !ambientObject.IsReacting)
                {
                    continue;
                }

                var distance = Vector2.Distance(ambientObject.Position, sun.Position);
                if (distance < selectedDistance)
                {
                    continue;
                }

                selectedCloud = ambientObject;
                selectedDistance = distance;
            }

            return selectedCloud != null;
        }

        void RebuildRainbowSurfaces(MasterDatabase masters)
        {
            for (var i = walkableSurfaces.Count - 1; i >= 0; i--)
            {
                if (walkableSurfaces[i].Kind == WalkableSurfaceKind.Rainbow)
                {
                    walkableSurfaces.RemoveAt(i);
                }
            }

            var rainbowMaster = masters.Rainbows.Get(1);
            foreach (var rainbow in rainbows)
            {
                rainbow.AddWalkableSurfaces(walkableSurfaces, rainbowMaster);
            }
        }


        void CreateAmbientObjects(MasterDatabase masters, TuningParameterMaster tuning)
        {
            var nextId = 1;
            var cloudType = masters.GetAmbientObjectType(AmbientObjectKind.Cloud);
            for (var i = 0; i < tuning.AmbientCloudCount; i++)
            {
                ambientObjects.Add(new AmbientObject(
                    nextId++,
                    AmbientObjectKind.Cloud,
                    AmbientSpawnPosition(AmbientObjectKind.Cloud, i),
                    cloudType.DefaultSize,
                    AmbientVelocity(cloudType.DriftVelocity, i),
                    cloudType.ContactRadius,
                    cloudType.MovementEdgePadding,
                    cloudType.MaxCenterY));
            }

            var starType = masters.GetAmbientObjectType(AmbientObjectKind.Star);
            for (var i = 0; i < tuning.AmbientStarCount; i++)
            {
                ambientObjects.Add(new AmbientObject(
                    nextId++,
                    AmbientObjectKind.Star,
                    AmbientSpawnPosition(AmbientObjectKind.Star, i),
                    starType.DefaultSize,
                    AmbientVelocity(starType.DriftVelocity, i),
                    starType.ContactRadius,
                    starType.MovementEdgePadding,
                    starType.MaxCenterY));
            }
        }

        void UpdateAmbientReactions(MasterDatabase masters, TuningParameterMaster tuning)
        {
            foreach (var ambientObject in ambientObjects)
            {
                var touched = IsTouchedByAnyLittlePerson(ambientObject);
                var type = masters.GetAmbientObjectType(ambientObject.Kind);
                var effectMaster = masters.VisualEffects.Get(type.VisualEffectMasterId);

                if (ambientObject.Kind == AmbientObjectKind.Cloud)
                {
                    if (touched)
                    {
                        ambientObject.MarkCloudTouched(tuning.RainLingerSeconds);
                    }

                    if (ambientObject.IsReacting)
                    {
                        RefreshOrCreateVisualEffect(
                            effectMaster.Id,
                            ambientObject.Id,
                            RainPosition(ambientObject),
                            EffectSize(ambientObject, effectMaster),
                            0f,
                            Mathf.Max(0.12f, ambientObject.ReactionSeconds));
                    }
                }
                else if (ambientObject.Kind == AmbientObjectKind.Star &&
                         touched &&
                         ambientObject.TryTriggerStar(effectMaster.DurationSeconds, tuning.StarCooldownSeconds))
                {
                    RefreshOrCreateVisualEffect(
                        effectMaster.Id,
                        ambientObject.Id,
                        ambientObject.Position,
                        EffectSize(ambientObject, effectMaster),
                        0f,
                        effectMaster.DurationSeconds);
                }
            }
        }

        bool IsTouchedByAnyLittlePerson(AmbientObject ambientObject)
        {
            foreach (var person in littlePeople)
            {
                if (ambientObject.IsTouchedBy(person))
                {
                    return true;
                }
            }

            return false;
        }

        void RefreshOrCreateVisualEffect(
            int visualEffectMasterId,
            int sourceObjectId,
            Vector2 position,
            Vector2 size,
            float angleDegrees,
            float durationSeconds)
        {
            foreach (var visualEffect in visualEffects)
            {
                if (visualEffect.VisualEffectMasterId == visualEffectMasterId &&
                    visualEffect.SourceObjectId == sourceObjectId)
                {
                    visualEffect.Refresh(position, size, angleDegrees, durationSeconds);
                    return;
                }
            }

            visualEffects.Add(new VisualEffectInstance(
                nextVisualEffectId++,
                visualEffectMasterId,
                sourceObjectId,
                position,
                size,
                angleDegrees,
                durationSeconds));
        }

        void AdvanceVisualEffects(float deltaTime)
        {
            for (var i = visualEffects.Count - 1; i >= 0; i--)
            {
                visualEffects[i].Advance(deltaTime);
                if (visualEffects[i].IsExpired)
                {
                    visualEffects.RemoveAt(i);
                }
            }
        }

        static Vector2 RainPosition(AmbientObject ambientObject)
        {
            return Clamp01(ambientObject.Position + new Vector2(0f, ambientObject.Size.y * 0.45f));
        }

        static Vector2 EffectSize(AmbientObject ambientObject, VisualEffectMaster effectMaster)
        {
            if (effectMaster.Kind == VisualEffectKind.RainColumn)
            {
                // 雨の発生位置(RainPositionと同じ基準点)から画面下端(正規化y=1.0)までの
                // 距離を雨柱の高さにする。固定値(旧: effectMaster.DefaultSize.y)だと、
                // 雲の高さに関わらず常に同じ短い範囲でループしてしまい、地面まで届かなかった。
                var originY = RainPosition(ambientObject).y;
                var heightToGround = Mathf.Max(0.05f, 1f - originY);

                return new Vector2(
                    Mathf.Max(effectMaster.DefaultSize.x, ambientObject.Size.x * 0.75f),
                    heightToGround);
            }

            return effectMaster.DefaultSize;
        }

        static Vector2 AmbientSpawnPosition(AmbientObjectKind kind, int index)
        {
            if (kind == AmbientObjectKind.Cloud)
            {
                switch (index % 3)
                {
                    case 0:
                        return new Vector2(0.30f, 0.24f);
                    case 1:
                        return new Vector2(0.70f, 0.31f);
                    default:
                        return new Vector2(0.50f, 0.27f);
                }
            }

            return new Vector2(0.84f, 0.12f);
        }

        static Vector2 AmbientVelocity(Vector2 baseVelocity, int index)
        {
            var xSign = index % 2 == 0 ? 1f : -1f;
            var ySign = index % 3 == 0 ? 1f : -1f;
            return new Vector2(baseVelocity.x * xSign, baseVelocity.y * ySign);
        }

        static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }
    }
}
