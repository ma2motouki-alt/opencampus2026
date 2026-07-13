using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed partial class WorldSpaceMaskAnimationController
    {
        void EnsureBuffers()
        {
            var length = MaskW * MaskH;
            if (mask != null && mask.Length == length)
            {
                return;
            }

            mask = new bool[length];
            effectiveMask = new bool[length];
            visited = new bool[length];
            componentSizeMap = new int[length];
            field = new float[length];
            fieldTmp = new float[length];
            pixels = new Color32[length];
            cellCounts = new int[CellsX * CellsY];
            cellMaxComponentSize = new int[CellsX * CellsY];

            maskTexture = new Texture2D(MaskW, MaskH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        void BuildMaskFromInteractionObjects(IReadOnlyList<InteractionObject> objects)
        {
            Array.Clear(mask, 0, mask.Length);

            foreach (var interactionObject in objects)
            {
                if (!ShouldPaintMask(interactionObject))
                {
                    continue;
                }

                FillPolygon(interactionObject.ContourPoints);
            }
        }

        bool ShouldPaintMask(InteractionObject interactionObject)
        {
            if (interactionObject == null ||
                interactionObject.ShapeKind != InteractionShapeKind.Contour ||
                interactionObject.ContourPoints.Count < 3)
            {
                return false;
            }

            return interactionObject.Kind == InteractionObjectKind.Hand && includeHandContours;
        }

        void FillPolygon(IReadOnlyList<Vector2> normalizedPoints)
        {
            var polygon = new Vector2[normalizedPoints.Count];
            var minX = MaskW - 1;
            var minY = MaskH - 1;
            var maxX = 0;
            var maxY = 0;

            for (var i = 0; i < normalizedPoints.Count; i++)
            {
                var point = NormalizedToMaskPx(normalizedPoints[i]);
                polygon[i] = point;
                minX = Mathf.Min(minX, Mathf.FloorToInt(point.x));
                minY = Mathf.Min(minY, Mathf.FloorToInt(point.y));
                maxX = Mathf.Max(maxX, Mathf.CeilToInt(point.x));
                maxY = Mathf.Max(maxY, Mathf.CeilToInt(point.y));
            }

            minX = Mathf.Clamp(minX, 0, MaskW - 1);
            minY = Mathf.Clamp(minY, 0, MaskH - 1);
            maxX = Mathf.Clamp(maxX, 0, MaskW - 1);
            maxY = Mathf.Clamp(maxY, 0, MaskH - 1);

            for (var y = minY; y <= maxY; y++)
            {
                var row = y * MaskW;
                for (var x = minX; x <= maxX; x++)
                {
                    if (IsPointInsidePolygon(polygon, new Vector2(x + 0.5f, y + 0.5f)))
                    {
                        mask[row + x] = true;
                    }
                }
            }
        }

        // 点から多角形までの最短距離。点が多角形の内側にある場合は0を返す。
        static float DistancePointToPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (IsPointInsidePolygon(polygon, point))
            {
                return 0f;
            }

            var minDistance = float.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var distance = DistancePointToSegment(point, a, b);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            return minDistance;
        }

        static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return Vector2.Distance(point, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            var closest = a + segment * t;
            return Vector2.Distance(point, closest);
        }

        void RecomputeEffectiveMask()
        {
            Array.Clear(effectiveMask, 0, effectiveMask.Length);
            Array.Clear(visited, 0, visited.Length);
            Array.Clear(componentSizeMap, 0, componentSizeMap.Length);
            effectiveWhiteCount = 0;

            for (var start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || visited[start])
                {
                    continue;
                }

                floodStack.Clear();
                componentBuffer.Clear();
                floodStack.Add(start);
                visited[start] = true;

                while (floodStack.Count > 0)
                {
                    var index = floodStack[floodStack.Count - 1];
                    floodStack.RemoveAt(floodStack.Count - 1);
                    componentBuffer.Add(index);

                    var x = index % MaskW;
                    var y = index / MaskW;
                    TryVisitNeighbor(x + 1, y);
                    TryVisitNeighbor(x - 1, y);
                    TryVisitNeighbor(x, y + 1);
                    TryVisitNeighbor(x, y - 1);
                }

                if (componentBuffer.Count < minBlobPixels)
                {
                    continue;
                }

                foreach (var index in componentBuffer)
                {
                    effectiveMask[index] = true;
                    componentSizeMap[index] = componentBuffer.Count;
                }

                effectiveWhiteCount += componentBuffer.Count;
            }
        }

        void TryVisitNeighbor(int x, int y)
        {
            if (x < 0 || x >= MaskW || y < 0 || y >= MaskH)
            {
                return;
            }

            var index = y * MaskW + x;
            if (!mask[index] || visited[index])
            {
                return;
            }

            visited[index] = true;
            floodStack.Add(index);
        }

        void RebuildCellCounts()
        {
            Array.Clear(cellCounts, 0, cellCounts.Length);
            Array.Clear(cellMaxComponentSize, 0, cellMaxComponentSize.Length);

            for (var y = 0; y < MaskH; y++)
            {
                var row = y * MaskW;
                var cellY = y * CellsY / MaskH;
                for (var x = 0; x < MaskW; x++)
                {
                    if (!effectiveMask[row + x])
                    {
                        continue;
                    }

                    var cellX = x * CellsX / MaskW;
                    var cellIndex = cellY * CellsX + cellX;
                    cellCounts[cellIndex]++;
                    if (componentSizeMap[row + x] > cellMaxComponentSize[cellIndex])
                    {
                        cellMaxComponentSize[cellIndex] = componentSizeMap[row + x];
                    }
                }
            }
        }

        void RebuildField()
        {
            var radius = Mathf.Clamp(blurRadius, 1, 32);
            var inv = 1f / (2 * radius + 1);

            for (var y = 0; y < MaskH; y++)
            {
                var row = y * MaskW;
                for (var x = 0; x < MaskW; x++)
                {
                    var sum = 0f;
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var xx = Mathf.Clamp(x + dx, 0, MaskW - 1);
                        if (effectiveMask[row + xx])
                        {
                            sum += 1f;
                        }
                    }

                    fieldTmp[row + x] = sum * inv;
                }
            }

            for (var x = 0; x < MaskW; x++)
            {
                for (var y = 0; y < MaskH; y++)
                {
                    var sum = 0f;
                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        var yy = Mathf.Clamp(y + dy, 0, MaskH - 1);
                        sum += fieldTmp[yy * MaskW + x];
                    }

                    field[y * MaskW + x] = sum * inv;
                }
            }
        }

        float SampleField(Vector2 point)
        {
            var x = Mathf.Clamp(point.x, 0f, MaskW - 1.001f);
            var y = Mathf.Clamp(point.y, 0f, MaskH - 1.001f);
            var x0 = (int)x;
            var y0 = (int)y;
            var fx = x - x0;
            var fy = y - y0;
            var index = y0 * MaskW + x0;

            var v00 = field[index];
            var v10 = field[index + 1];
            var v01 = field[index + MaskW];
            var v11 = field[index + MaskW + 1];
            return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
        }

        Vector2 FieldGradient(Vector2 point)
        {
            const float d = 1.5f;
            var gx = SampleField(point + new Vector2(d, 0f)) - SampleField(point - new Vector2(d, 0f));
            var gy = SampleField(point + new Vector2(0f, d)) - SampleField(point - new Vector2(0f, d));
            return new Vector2(gx, gy) / (2f * d);
        }

        Vector2 NormalizedToMaskPx(Vector2 normalized)
        {
            return new Vector2(
                Mathf.Clamp01(normalized.x) * (MaskW - 1),
                (1f - Mathf.Clamp01(normalized.y)) * (MaskH - 1));
        }

        Vector2 WorldToMaskPx(Vector3 worldPosition)
        {
            return NormalizedToMaskPx(mapper.ToNormalized(worldPosition));
        }

        float WorldRadiusToMaskRadius(float worldRadius)
        {
            var worldUnitsPerMaskPx = mapper.WorldHeight / Mathf.Max(1f, MaskH - 1f);
            return Mathf.Max(0.5f, worldRadius / Mathf.Max(0.0001f, worldUnitsPerMaskPx));
        }

        Vector2 MaskPxToNormalized(Vector2 maskPx)
        {
            return new Vector2(
                Mathf.Clamp01(maskPx.x / Mathf.Max(1f, MaskW - 1)),
                1f - Mathf.Clamp01(maskPx.y / Mathf.Max(1f, MaskH - 1)));
        }

        Vector3 MaskToWorld(Vector2 maskPx)
        {
            return mapper.ToWorld(MaskPxToNormalized(maskPx));
        }

        static bool IsPointInsidePolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                if ((pi.y > point.y) != (pj.y > point.y) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / Mathf.Max(0.000001f, pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
