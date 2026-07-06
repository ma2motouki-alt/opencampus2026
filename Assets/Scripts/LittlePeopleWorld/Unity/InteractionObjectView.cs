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
        LineRenderer contourRenderer;

        public int SourceObjectId { get; private set; }

        public void Initialize()
        {
            fieldRenderer = CreateRenderer("Field", RuntimeSpriteFactory.Circle, -8);
            shadowRenderer = CreateRenderer("Shadow", RuntimeSpriteFactory.Circle, -2);
            edgeRenderer = CreateRenderer("Edge", RuntimeSpriteFactory.Circle, 5);
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
    }
}
