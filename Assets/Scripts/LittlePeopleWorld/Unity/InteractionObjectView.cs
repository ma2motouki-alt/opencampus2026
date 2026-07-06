using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class InteractionObjectView : MonoBehaviour
    {
        SpriteRenderer shadowRenderer;
        SpriteRenderer edgeRenderer;
        SpriteRenderer fieldRenderer;
        MeshFilter contourFillFilter;
        MeshRenderer contourFillRenderer;
        Mesh contourFillMesh;
        readonly List<Vector2> contourFillPoints = new();
        LineRenderer contourRenderer;

        public int SourceObjectId { get; private set; }

        public void Initialize()
        {
            fieldRenderer = CreateRenderer("Field", RuntimeSpriteFactory.Circle, -8);
            shadowRenderer = CreateRenderer("Shadow", RuntimeSpriteFactory.Circle, -2);
            edgeRenderer = CreateRenderer("Edge", RuntimeSpriteFactory.Circle, 5);
            CreateContourFillRenderer("Contour Fill", 3);
            contourRenderer = CreateContourRenderer("Contour", 7);
        }

        public void Render(
            InteractionObject interactionObject,
            InteractionField field,
            InteractionObjectTypeMaster typeMaster,
            NormalizedScreenMapper mapper,
            bool debugEnabled,
            bool isSelected)
        {
            SourceObjectId = interactionObject.Id;
            transform.position = mapper.ToWorld(interactionObject.Position);
            transform.rotation = Quaternion.Euler(0f, 0f, -interactionObject.AngleDegrees);

            var objectScale = mapper.ToWorldScale(interactionObject.Size);
            var isBar = interactionObject.Kind == InteractionObjectKind.BarProp;
            var objectSprite = isBar ? RuntimeSpriteFactory.Square : RuntimeSpriteFactory.Circle;
            shadowRenderer.sprite = objectSprite;
            edgeRenderer.sprite = objectSprite;
            var canRenderContourKind = interactionObject.Kind == InteractionObjectKind.Hand ||
                                       interactionObject.Kind == InteractionObjectKind.BarProp;
            var shouldRenderContour = canRenderContourKind &&
                                      interactionObject.ShapeKind == InteractionShapeKind.Contour &&
                                      interactionObject.ContourPoints.Count >= 3;

            shadowRenderer.enabled = !shouldRenderContour;
            edgeRenderer.enabled = !shouldRenderContour;
            shadowRenderer.transform.localScale = objectScale;
            edgeRenderer.transform.localScale = objectScale * 1.08f;

            var shadowColor = Color.black;
            shadowColor.a = interactionObject.Kind == InteractionObjectKind.Hand ? 0.32f : 0.18f;
            shadowRenderer.color = shadowColor;

            var edgeColor = typeMaster.DebugColor;
            if (isSelected)
            {
                edgeColor = Color.Lerp(edgeColor, Color.white, 0.35f);
            }

            edgeColor.a = isSelected ? 0.88f : interactionObject.State == InteractionObjectState.Dragging ? 0.78f : 0.48f;
            edgeRenderer.color = edgeColor;

            RenderContourFill(interactionObject, mapper, shouldRenderContour, isSelected);
            RenderContour(interactionObject, typeMaster, mapper, shouldRenderContour);

            fieldRenderer.enabled = debugEnabled;
            if (debugEnabled)
            {
                fieldRenderer.sprite = isBar ? RuntimeSpriteFactory.Square : RuntimeSpriteFactory.Circle;
                var fieldScale = isBar
                    ? mapper.ToWorldScale(new Vector2(interactionObject.Size.x + field.Radius * 1.6f, Mathf.Max(interactionObject.Size.y + field.Radius * 1.25f, interactionObject.Size.y * 2.6f)))
                    : Vector3.one * mapper.ToWorldRadius(field.Radius) * 2f;
                fieldRenderer.transform.localScale = fieldScale;
                var fieldColor = typeMaster.DebugColor;
                fieldColor.a = isSelected ? 0.18f : 0.1f;
                fieldRenderer.color = fieldColor;
            }
        }

        void RenderContourFill(
            InteractionObject interactionObject,
            NormalizedScreenMapper mapper,
            bool shouldRenderContour,
            bool isSelected)
        {
            if (!shouldRenderContour || contourFillRenderer == null || contourFillMesh == null)
            {
                if (contourFillRenderer != null)
                {
                    contourFillRenderer.enabled = false;
                }

                contourFillMesh?.Clear();
                return;
            }

            contourFillPoints.Clear();
            for (var i = 0; i < interactionObject.ContourPoints.Count; i++)
            {
                var world = mapper.ToWorld(interactionObject.ContourPoints[i]);
                var local = transform.InverseTransformPoint(world);
                contourFillPoints.Add(new Vector2(local.x, local.y));
            }

            if (!ContourTriangulator.TryBuildMesh(contourFillPoints, contourFillMesh))
            {
                contourFillRenderer.enabled = false;
                return;
            }

            contourFillRenderer.enabled = true;
            var fillColor = Color.white;
            fillColor.a = interactionObject.Kind == InteractionObjectKind.Hand ? 0.68f : 0.52f;
            if (isSelected)
            {
                fillColor.a = Mathf.Min(0.82f, fillColor.a + 0.12f);
            }

            contourFillRenderer.material.color = fillColor;
        }

        void RenderContour(
            InteractionObject interactionObject,
            InteractionObjectTypeMaster typeMaster,
            NormalizedScreenMapper mapper,
            bool shouldRenderContour)
        {
            contourRenderer.enabled = shouldRenderContour;
            if (!shouldRenderContour)
            {
                contourRenderer.positionCount = 0;
                return;
            }

            var pointCount = interactionObject.ContourPoints.Count;
            contourRenderer.positionCount = pointCount + 1;
            for (var i = 0; i < pointCount; i++)
            {
                contourRenderer.SetPosition(i, mapper.ToWorld(interactionObject.ContourPoints[i]));
            }
            contourRenderer.SetPosition(pointCount, mapper.ToWorld(interactionObject.ContourPoints[0]));

            var color = typeMaster.DebugColor;
            color.a = 0.88f;
            contourRenderer.startColor = color;
            contourRenderer.endColor = color;
            contourRenderer.widthMultiplier = Mathf.Max(0.015f, mapper.ToWorldRadius(0.0028f));
        }

        void CreateContourFillRenderer(string name, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            contourFillMesh = new Mesh { name = "Interaction Contour Fill Mesh" };
            contourFillFilter = child.AddComponent<MeshFilter>();
            contourFillFilter.sharedMesh = contourFillMesh;

            contourFillRenderer = child.AddComponent<MeshRenderer>();
            contourFillRenderer.sortingOrder = sortingOrder;
            contourFillRenderer.material = new Material(Shader.Find("Sprites/Default"));
            contourFillRenderer.material.color = new Color(1f, 1f, 1f, 0.68f);
            contourFillRenderer.enabled = false;
        }

        SpriteRenderer CreateRenderer(string name, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        LineRenderer CreateContourRenderer(string name, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.loop = true;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.alignment = LineAlignment.View;
            renderer.numCornerVertices = 2;
            renderer.numCapVertices = 2;
            renderer.sortingOrder = sortingOrder;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.enabled = false;
            return renderer;
        }

        static class ContourTriangulator
        {
            const float Epsilon = 0.00001f;
            static readonly List<Vector2> CleanPoints = new();
            static readonly List<int> Indices = new();
            static readonly List<int> Triangles = new();

            public static bool TryBuildMesh(IReadOnlyList<Vector2> sourcePoints, Mesh mesh)
            {
                mesh.Clear();
                CleanPoints.Clear();
                Triangles.Clear();

                for (var i = 0; i < sourcePoints.Count; i++)
                {
                    var point = sourcePoints[i];
                    if (CleanPoints.Count > 0 && (CleanPoints[CleanPoints.Count - 1] - point).sqrMagnitude < Epsilon)
                    {
                        continue;
                    }

                    CleanPoints.Add(point);
                }

                if (CleanPoints.Count > 2 &&
                    (CleanPoints[0] - CleanPoints[CleanPoints.Count - 1]).sqrMagnitude < Epsilon)
                {
                    CleanPoints.RemoveAt(CleanPoints.Count - 1);
                }

                if (CleanPoints.Count < 3)
                {
                    return false;
                }

                if (SignedArea(CleanPoints) < 0f)
                {
                    CleanPoints.Reverse();
                }

                if (!TryTriangulate(CleanPoints, Triangles))
                {
                    mesh.Clear();
                    return false;
                }

                var vertices = new Vector3[CleanPoints.Count];
                for (var i = 0; i < CleanPoints.Count; i++)
                {
                    vertices[i] = new Vector3(CleanPoints[i].x, CleanPoints[i].y, 0f);
                }

                mesh.vertices = vertices;
                mesh.triangles = Triangles.ToArray();
                mesh.RecalculateBounds();
                return true;
            }

            static bool TryTriangulate(IReadOnlyList<Vector2> points, List<int> triangles)
            {
                Indices.Clear();
                for (var i = 0; i < points.Count; i++)
                {
                    Indices.Add(i);
                }

                var guard = points.Count * points.Count;
                while (Indices.Count > 3 && guard-- > 0)
                {
                    var earFound = false;
                    for (var i = 0; i < Indices.Count; i++)
                    {
                        var previousIndex = Indices[(i - 1 + Indices.Count) % Indices.Count];
                        var currentIndex = Indices[i];
                        var nextIndex = Indices[(i + 1) % Indices.Count];

                        if (!IsEar(points, previousIndex, currentIndex, nextIndex, Indices))
                        {
                            continue;
                        }

                        triangles.Add(previousIndex);
                        triangles.Add(currentIndex);
                        triangles.Add(nextIndex);
                        Indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }

                    if (!earFound)
                    {
                        return false;
                    }
                }

                if (Indices.Count != 3)
                {
                    return false;
                }

                triangles.Add(Indices[0]);
                triangles.Add(Indices[1]);
                triangles.Add(Indices[2]);
                return triangles.Count >= 3;
            }

            static bool IsEar(
                IReadOnlyList<Vector2> points,
                int previousIndex,
                int currentIndex,
                int nextIndex,
                IReadOnlyList<int> polygonIndices)
            {
                var a = points[previousIndex];
                var b = points[currentIndex];
                var c = points[nextIndex];
                if (Cross(b - a, c - a) <= Epsilon)
                {
                    return false;
                }

                for (var i = 0; i < polygonIndices.Count; i++)
                {
                    var index = polygonIndices[i];
                    if (index == previousIndex || index == currentIndex || index == nextIndex)
                    {
                        continue;
                    }

                    if (IsPointInTriangle(points[index], a, b, c))
                    {
                        return false;
                    }
                }

                return true;
            }

            static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
            {
                return Cross(b - a, point - a) >= -Epsilon &&
                       Cross(c - b, point - b) >= -Epsilon &&
                       Cross(a - c, point - c) >= -Epsilon;
            }

            static float SignedArea(IReadOnlyList<Vector2> points)
            {
                var area = 0f;
                for (var i = 0; i < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Count];
                    area += a.x * b.y - b.x * a.y;
                }

                return area * 0.5f;
            }

            static float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }
    }
}
