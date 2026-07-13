using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Input
{
    static class InteractionProtocolParser
    {
        public static SensorFrameDto ParseFrame(string json)
        {
            var frame = JsonUtility.FromJson<SensorFrameDto>(json);
            return frame ?? throw new FormatException("UDP JSON root was empty.");
        }

        public static InteractionObjectKind ParseKind(string rawKind)
        {
            var value = Normalize(rawKind);
            return value switch
            {
                "hand" => InteractionObjectKind.Hand,
                "round" => InteractionObjectKind.RoundProp,
                "circle" => InteractionObjectKind.RoundProp,
                "round_prop" => InteractionObjectKind.RoundProp,
                "mask_stroke" => InteractionObjectKind.MaskStroke,
                "block" => InteractionObjectKind.BlockProp,
                "block_prop" => InteractionObjectKind.BlockProp,
                // Retired bar input remains readable as a hand contour.
                "bar" => InteractionObjectKind.Hand,
                "stick" => InteractionObjectKind.Hand,
                "bar_prop" => InteractionObjectKind.Hand,
                _ => InteractionObjectKind.Hand
            };
        }

        public static InteractionObjectState ParseState(string rawState)
        {
            return Normalize(rawState) switch
            {
                "placing" => InteractionObjectState.Placing,
                "dragging" => InteractionObjectState.Dragging,
                _ => InteractionObjectState.Placed
            };
        }

        public static bool IsRemoved(string rawState)
        {
            return Normalize(rawState) == "removed";
        }

        public static Vector2 ParseSize(InteractionObjectKind kind, float width, float height)
        {
            var fallback = kind == InteractionObjectKind.MaskStroke
                ? new Vector2(0.12f, 0.026f)
                : new Vector2(0.12f, 0.12f);
            return new Vector2(
                width > 0.0001f ? width : fallback.x,
                height > 0.0001f ? height : fallback.y);
        }

        public static InteractionShapeKind ParseShape(string rawShape, PointDto[] points)
        {
            return Normalize(rawShape) == "contour" && points != null && points.Length >= 3
                ? InteractionShapeKind.Contour
                : InteractionShapeKind.Primitive;
        }

        public static IReadOnlyList<Vector2> ParsePoints(PointDto[] source)
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

        static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_");
        }
    }

    [Serializable]
    sealed class SensorFrameDto
    {
        public long frame;
        public float timestamp;
        public InteractionObjectDto[] objects;
    }

    [Serializable]
    sealed class InteractionObjectDto
    {
        public int id;
        public string kind;
        public string type;
        public string shape;
        public float x;
        public float y;
        public float w;
        public float h;
        public float angle;
        public float height;
        public string state;
        public PointDto[] points;
    }

    [Serializable]
    sealed class PointDto
    {
        public float x;
        public float y;
    }
}
