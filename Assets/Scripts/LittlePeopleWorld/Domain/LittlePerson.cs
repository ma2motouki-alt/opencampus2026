using System;
using System.Collections.Generic;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Domain
{
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
    public enum LittlePersonEmotion
    {
        Calm,
        Curious,
        Startled
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
}
