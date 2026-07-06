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

        public int SourceObjectId { get; private set; }

        public void Initialize()
        {
            fieldRenderer = CreateRenderer("Field", RuntimeSpriteFactory.Circle, -8);
            shadowRenderer = CreateRenderer("Shadow", RuntimeSpriteFactory.Circle, -2);
            edgeRenderer = CreateRenderer("Edge", RuntimeSpriteFactory.Circle, 5);
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

        SpriteRenderer CreateRenderer(string name, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
