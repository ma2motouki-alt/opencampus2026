using System;
using System.Collections.Generic;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Domain
{
    public enum RainbowState
    {
        Appearing,
        Active,
        Fading,
        Expired
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
}
