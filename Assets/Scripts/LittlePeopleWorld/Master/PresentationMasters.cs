using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Master
{
    public sealed class VisualEffectMaster
    {
        public int Id { get; }
        public VisualEffectKind Kind { get; }
        public VisualEffectRenderMode RenderMode { get; }
        public string Name { get; }
        public Color Color { get; }
        public float PulseSpeed { get; }
        public float Alpha { get; }
        public Vector2 DefaultSize { get; }
        public float DurationSeconds { get; }
        public string AssetKey { get; }
        public float DropSizeScale { get; }

        public VisualEffectMaster(
            int id,
            VisualEffectKind kind,
            VisualEffectRenderMode renderMode,
            string name,
            Color color,
            float pulseSpeed,
            float alpha,
            Vector2 defaultSize,
            float durationSeconds,
            string assetKey,
            float dropSizeScale = 1f)
        {
            Id = id;
            Kind = kind;
            RenderMode = renderMode;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Color = color;
            PulseSpeed = Mathf.Max(0f, pulseSpeed);
            Alpha = Mathf.Clamp01(alpha);
            DefaultSize = new Vector2(Mathf.Max(0.001f, defaultSize.x), Mathf.Max(0.001f, defaultSize.y));
            DurationSeconds = Mathf.Max(0.001f, durationSeconds);
            AssetKey = assetKey ?? string.Empty;
            DropSizeScale = Mathf.Max(0.05f, dropSizeScale);
        }
    }
    public sealed class SoundCueMaster
    {
        public int Id { get; }
        public string Name { get; }
        public string ResourcePath { get; }

        public SoundCueMaster(int id, string name, string resourcePath)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ResourcePath = resourcePath ?? string.Empty;
        }
    }
}
