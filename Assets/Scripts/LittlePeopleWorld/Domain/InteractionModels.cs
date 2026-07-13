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
}
