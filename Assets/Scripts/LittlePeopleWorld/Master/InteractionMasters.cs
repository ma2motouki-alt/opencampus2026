using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class ReactionMaster
    {
        public int Id { get; }
        public string Name { get; }
        public LittlePersonBehaviorKind BehaviorKind { get; }
        public LittlePersonEmotion Emotion { get; }
        public float DurationSeconds { get; }

        public ReactionMaster(
            int id,
            string name,
            LittlePersonBehaviorKind behaviorKind,
            LittlePersonEmotion emotion,
            float durationSeconds)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            BehaviorKind = behaviorKind;
            Emotion = emotion;
            DurationSeconds = Mathf.Max(0.05f, durationSeconds);
        }
    }
    public sealed class ReactionConditionMaster
    {
        public int Id { get; }
        public int ReactionMasterId { get; }
        public InteractionObjectKind ObjectKind { get; }
        public float TriggerDistance { get; }

        public ReactionConditionMaster(int id, int reactionMasterId, InteractionObjectKind objectKind, float triggerDistance)
        {
            Id = id;
            ReactionMasterId = reactionMasterId;
            ObjectKind = objectKind;
            TriggerDistance = Mathf.Max(0.001f, triggerDistance);
        }
    }
    public sealed class InteractionObjectTypeMaster
    {
        public int Id { get; }
        public InteractionObjectKind Kind { get; }
        public string Name { get; }
        public Vector2 DefaultSize { get; }
        public float DefaultHeight { get; }
        public int InteractionFieldMasterId { get; }
        public Color DebugColor { get; }

        public InteractionObjectTypeMaster(
            int id,
            InteractionObjectKind kind,
            string name,
            Vector2 defaultSize,
            float defaultHeight,
            int interactionFieldMasterId,
            Color debugColor)
        {
            Id = id;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DefaultSize = new Vector2(Mathf.Max(0.01f, defaultSize.x), Mathf.Max(0.01f, defaultSize.y));
            DefaultHeight = Mathf.Clamp01(defaultHeight);
            InteractionFieldMasterId = interactionFieldMasterId;
            DebugColor = debugColor;
        }
    }
    public sealed class InteractionFieldMaster
    {
        public int Id { get; }
        public InteractionFieldKind Kind { get; }
        public string Name { get; }
        public float Radius { get; }
        public float Strength { get; }
        public float EdgePadding { get; }

        public InteractionFieldMaster(
            int id,
            InteractionFieldKind kind,
            string name,
            float radius,
            float strength,
            float edgePadding)
        {
            Id = id;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Radius = Mathf.Max(0.001f, radius);
            Strength = Mathf.Max(0f, strength);
            EdgePadding = Mathf.Max(0f, edgePadding);
        }
    }
}
