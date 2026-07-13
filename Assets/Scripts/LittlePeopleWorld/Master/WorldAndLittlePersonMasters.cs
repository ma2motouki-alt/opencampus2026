using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class WorldPresetMaster
    {
        public int Id { get; }
        public string Name { get; }
        public int InitialLittlePersonCount { get; }
        public int DefaultArchetypeId { get; }
        public int DefaultBehaviorProfileId { get; }
        public Color BackgroundColor { get; }

        public WorldPresetMaster(
            int id,
            string name,
            int initialLittlePersonCount,
            int defaultArchetypeId,
            int defaultBehaviorProfileId,
            Color backgroundColor)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            InitialLittlePersonCount = Math.Max(1, initialLittlePersonCount);
            DefaultArchetypeId = defaultArchetypeId;
            DefaultBehaviorProfileId = defaultBehaviorProfileId;
            BackgroundColor = backgroundColor;
        }
    }
    public sealed class LittlePersonArchetypeMaster
    {
        public int Id { get; }
        public string Name { get; }
        public Color BodyColor { get; }
        public float Size { get; }
        public float MoveSpeed { get; }
        public float Curiosity { get; }
        public float Fear { get; }

        public LittlePersonArchetypeMaster(
            int id,
            string name,
            Color bodyColor,
            float size,
            float moveSpeed,
            float curiosity,
            float fear)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            BodyColor = bodyColor;
            Size = Mathf.Max(0.005f, size);
            MoveSpeed = Mathf.Max(0.001f, moveSpeed);
            Curiosity = Mathf.Clamp01(curiosity);
            Fear = Mathf.Clamp01(fear);
        }
    }
    public sealed class BehaviorProfileMaster
    {
        public int Id { get; }
        public string Name { get; }
        public float WanderTurnInterval { get; }
        public float SteeringResponsiveness { get; }
        public float NeighborSeparationRadius { get; }
        public float NeighborSeparationStrength { get; }

        public BehaviorProfileMaster(
            int id,
            string name,
            float wanderTurnInterval,
            float steeringResponsiveness,
            float neighborSeparationRadius,
            float neighborSeparationStrength)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            WanderTurnInterval = Mathf.Max(0.1f, wanderTurnInterval);
            SteeringResponsiveness = Mathf.Max(0.1f, steeringResponsiveness);
            NeighborSeparationRadius = Mathf.Max(0.001f, neighborSeparationRadius);
            NeighborSeparationStrength = Mathf.Max(0f, neighborSeparationStrength);
        }
    }
}
