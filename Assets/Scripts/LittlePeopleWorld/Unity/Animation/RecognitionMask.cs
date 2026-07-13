using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation
{
    internal sealed class RecognitionMask
    {
        const int CellsX = 16;
        const int CellsY = 9;

        readonly List<int> floodStack = new();
        readonly List<int> componentBuffer = new();
        int width;
        int height;
        int minBlobPixels;
        bool includeHandContours;
        bool[] raw;
        bool[] effective;
        bool[] visited;
        int[] componentSizeMap;
        int[] cellCounts = Array.Empty<int>();
        int[] cellMaxComponentSize = Array.Empty<int>();
        float[] field;
        float[] fieldTmp;

        public int Width => width;
        public int Height => height;
        public int EffectiveWhiteCount { get; private set; }
        public IReadOnlyList<bool> EffectivePixels => effective;

        public void Configure(int requestedWidth, int requestedHeight, int requestedMinBlobPixels, bool paintHands)
        {
            var nextWidth = Mathf.Clamp(requestedWidth, 16, 1024);
            var nextHeight = Mathf.Clamp(requestedHeight, 16, 1024);
            minBlobPixels = Mathf.Max(1, requestedMinBlobPixels);
            includeHandContours = paintHands;
            if (raw != null && width == nextWidth && height == nextHeight) return;

            width = nextWidth;
            height = nextHeight;
            var length = width * height;
            raw = new bool[length];
            effective = new bool[length];
            visited = new bool[length];
            componentSizeMap = new int[length];
            field = new float[length];
            fieldTmp = new float[length];
            cellCounts = new int[CellsX * CellsY];
            cellMaxComponentSize = new int[CellsX * CellsY];
        }

        public void Rebuild(IReadOnlyList<InteractionObject> objects, int blurRadius)
        {
            if (raw == null) return;
            Array.Clear(raw, 0, raw.Length);
            if (objects != null)
            {
                foreach (var interactionObject in objects)
                {
                    if (!ShouldPaint(interactionObject)) continue;
                    FillPolygon(interactionObject.ContourPoints);
                }
            }

            RecomputeEffective();
            RebuildField(Mathf.Max(0, blurRadius));
            RebuildCells();
        }

        bool ShouldPaint(InteractionObject interactionObject)
        {
            return interactionObject != null &&
                   interactionObject.ShapeKind == InteractionShapeKind.Contour &&
                   interactionObject.ContourPoints.Count >= 3 &&
                   (includeHandContours || interactionObject.Kind != InteractionObjectKind.Hand);
        }

        void FillPolygon(IReadOnlyList<Vector2> points)
        {
            var polygon = new Vector2[points.Count];
            var minX = width - 1;
            var maxX = 0;
            var minY = height - 1;
            var maxY = 0;
            for (var i = 0; i < points.Count; i++)
            {
                polygon[i] = NormalizedToPixel(points[i]);
                minX = Mathf.Min(minX, Mathf.FloorToInt(polygon[i].x));
                maxX = Mathf.Max(maxX, Mathf.CeilToInt(polygon[i].x));
                minY = Mathf.Min(minY, Mathf.FloorToInt(polygon[i].y));
                maxY = Mathf.Max(maxY, Mathf.CeilToInt(polygon[i].y));
            }

            minX = Mathf.Clamp(minX, 0, width - 1);
            maxX = Mathf.Clamp(maxX, 0, width - 1);
            minY = Mathf.Clamp(minY, 0, height - 1);
            maxY = Mathf.Clamp(maxY, 0, height - 1);
            for (var y = minY; y <= maxY; y++)
            {
                var row = y * width;
                for (var x = minX; x <= maxX; x++)
                {
                    if (IsPointInsidePolygon(polygon, new Vector2(x + 0.5f, y + 0.5f))) raw[row + x] = true;
                }
            }
        }

        void RecomputeEffective()
        {
            Array.Clear(effective, 0, effective.Length);
            Array.Clear(visited, 0, visited.Length);
            Array.Clear(componentSizeMap, 0, componentSizeMap.Length);
            EffectiveWhiteCount = 0;

            for (var index = 0; index < raw.Length; index++)
            {
                if (!raw[index] || visited[index]) continue;
                floodStack.Clear();
                componentBuffer.Clear();
                floodStack.Add(index);
                visited[index] = true;
                while (floodStack.Count > 0)
                {
                    var last = floodStack.Count - 1;
                    var current = floodStack[last];
                    floodStack.RemoveAt(last);
                    componentBuffer.Add(current);
                    var x = current % width;
                    var y = current / width;
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }

                var size = componentBuffer.Count;
                foreach (var pixel in componentBuffer) componentSizeMap[pixel] = size;
                if (size < minBlobPixels) continue;
                foreach (var pixel in componentBuffer) effective[pixel] = true;
                EffectiveWhiteCount += size;
            }
        }

        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            var index = y * width + x;
            if (!raw[index] || visited[index]) return;
            visited[index] = true;
            floodStack.Add(index);
        }

        void RebuildField(int blurRadius)
        {
            for (var i = 0; i < field.Length; i++) field[i] = effective[i] ? 1f : 0f;
            if (blurRadius <= 0) return;

            for (var y = 0; y < height; y++)
            {
                var sum = 0f;
                for (var x = -blurRadius; x <= blurRadius; x++) sum += field[y * width + Mathf.Clamp(x, 0, width - 1)];
                for (var x = 0; x < width; x++)
                {
                    fieldTmp[y * width + x] = sum / (blurRadius * 2 + 1);
                    sum -= field[y * width + Mathf.Clamp(x - blurRadius, 0, width - 1)];
                    sum += field[y * width + Mathf.Clamp(x + blurRadius + 1, 0, width - 1)];
                }
            }

            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                for (var y = -blurRadius; y <= blurRadius; y++) sum += fieldTmp[Mathf.Clamp(y, 0, height - 1) * width + x];
                for (var y = 0; y < height; y++)
                {
                    field[y * width + x] = sum / (blurRadius * 2 + 1);
                    sum -= fieldTmp[Mathf.Clamp(y - blurRadius, 0, height - 1) * width + x];
                    sum += fieldTmp[Mathf.Clamp(y + blurRadius + 1, 0, height - 1) * width + x];
                }
            }
        }

        void RebuildCells()
        {
            Array.Clear(cellCounts, 0, cellCounts.Length);
            Array.Clear(cellMaxComponentSize, 0, cellMaxComponentSize.Length);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = y * width + x;
                    if (!effective[pixel]) continue;
                    var cx = Mathf.Clamp(x * CellsX / width, 0, CellsX - 1);
                    var cy = Mathf.Clamp(y * CellsY / height, 0, CellsY - 1);
                    var cell = cy * CellsX + cx;
                    cellCounts[cell]++;
                    cellMaxComponentSize[cell] = Mathf.Max(cellMaxComponentSize[cell], componentSizeMap[pixel]);
                }
            }
        }

        public float SampleField(Vector2 point)
        {
            if (field == null) return 0f;
            var x = Mathf.Clamp(point.x, 0f, width - 1f);
            var y = Mathf.Clamp(point.y, 0f, height - 1f);
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var x1 = Mathf.Min(x0 + 1, width - 1);
            var y1 = Mathf.Min(y0 + 1, height - 1);
            var tx = x - x0;
            var ty = y - y0;
            return Mathf.Lerp(
                Mathf.Lerp(field[y0 * width + x0], field[y0 * width + x1], tx),
                Mathf.Lerp(field[y1 * width + x0], field[y1 * width + x1], tx), ty);
        }

        public Vector2 FieldGradient(Vector2 point)
        {
            return new Vector2(
                SampleField(point + Vector2.right) - SampleField(point + Vector2.left),
                SampleField(point + Vector2.up) - SampleField(point + Vector2.down)) * 0.5f;
        }

        public bool IsSet(int x, int y)
        {
            return effective != null && x >= 0 && x < width && y >= 0 && y < height && effective[y * width + x];
        }

        public Vector2 FindNearestCluster(Vector2 position, Vector2 jitter)
        {
            if (cellCounts.Length == 0) return position;
            var cellWidth = (float)width / CellsX;
            var cellHeight = (float)height / CellsY;
            var bestDistance = float.MaxValue;
            var target = position;
            for (var cy = 0; cy < CellsY; cy++)
            {
                for (var cx = 0; cx < CellsX; cx++)
                {
                    var cell = cy * CellsX + cx;
                    if (cellCounts[cell] <= 0) continue;
                    var center = new Vector2((cx + 0.5f) * cellWidth, (cy + 0.5f) * cellHeight) + jitter;
                    var score = (center - position).sqrMagnitude / Mathf.Max(1f, cellMaxComponentSize[cell]);
                    if (score >= bestDistance) continue;
                    bestDistance = score;
                    target = center;
                }
            }
            return target;
        }

        public Vector2 NormalizedToPixel(Vector2 normalized) => new(
            Mathf.Clamp01(normalized.x) * (width - 1),
            (1f - Mathf.Clamp01(normalized.y)) * (height - 1));

        public Vector2 PixelToNormalized(Vector2 pixel) => new(
            Mathf.Clamp01(pixel.x / Mathf.Max(1f, width - 1f)),
            1f - Mathf.Clamp01(pixel.y / Mathf.Max(1f, height - 1f)));

        public Vector2 WorldToPixel(Vector3 world, NormalizedScreenMapper mapper) => NormalizedToPixel(mapper.ToNormalized(world));
        public Vector3 PixelToWorld(Vector2 pixel, NormalizedScreenMapper mapper) => mapper.ToWorld(PixelToNormalized(pixel));
        public float WorldRadiusToPixel(float radius, NormalizedScreenMapper mapper) => radius / Mathf.Max(0.0001f, mapper.WorldHeight) * (height - 1f);

        public static float DistancePointToPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (polygon == null || polygon.Count < 2) return float.MaxValue;
            if (IsPointInsidePolygon(polygon, point)) return 0f;
            var best = float.MaxValue;
            for (var i = 0; i < polygon.Count; i++) best = Mathf.Min(best, DistancePointToSegment(point, polygon[i], polygon[(i + 1) % polygon.Count]));
            return best;
        }

        public static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= 0.000001f) return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSqr);
            return Vector2.Distance(point, a + segment * t);
        }

        static bool IsPointInsidePolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.000001f, b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
