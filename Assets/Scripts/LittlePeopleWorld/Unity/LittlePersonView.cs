using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class LittlePersonView : MonoBehaviour
    {
        SpriteRenderer glowRenderer;
        SpriteRenderer bodyRenderer;
        SpriteRenderer headRenderer;

        public void Initialize()
        {
            glowRenderer = CreateRenderer("Glow", RuntimeSpriteFactory.Circle, -4);
            bodyRenderer = CreateRenderer("Body", RuntimeSpriteFactory.Circle, 10);
            headRenderer = CreateRenderer("Head", RuntimeSpriteFactory.Circle, 11);
            headRenderer.transform.localPosition = new Vector3(0.12f, 0.16f, 0f);
        }

        public void Render(LittlePerson person, LittlePersonArchetypeMaster archetype, NormalizedScreenMapper mapper)
        {
            transform.position = mapper.ToWorld(person.Position);

            var bodySize = mapper.ToWorldRadius(archetype.Size);
            bodyRenderer.transform.localScale = Vector3.one * bodySize;
            headRenderer.transform.localScale = Vector3.one * bodySize * 0.58f;
            glowRenderer.transform.localScale = Vector3.one * bodySize * GlowScale(person);

            var bodyColor = archetype.BodyColor;
            bodyRenderer.color = bodyColor;
            headRenderer.color = Color.Lerp(bodyColor, Color.white, 0.35f);

            var glowColor = GlowColor(person, bodyColor);
            glowColor.a = GlowAlpha(person);
            glowRenderer.color = glowColor;

            if (person.Velocity.sqrMagnitude > 0.000001f)
            {
                var angle = Mathf.Atan2(-person.Velocity.y, person.Velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
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

        static Color GlowColor(LittlePerson person, Color fallback)
        {
            switch (person.CurrentBehavior)
            {
                case LittlePersonBehaviorKind.ClimbBar:
                case LittlePersonBehaviorKind.TransferToSurface:
                case LittlePersonBehaviorKind.SurfaceWalk:
                case LittlePersonBehaviorKind.RideSurface:
                    return new Color(0.36f, 1f, 0.95f, 1f);
                case LittlePersonBehaviorKind.Falling:
                    return new Color(0.7f, 0.85f, 1f, 1f);
            }

            switch (person.Emotion)
            {
                case LittlePersonEmotion.Curious:
                    return new Color(1f, 0.35f, 0.95f, 1f);
                case LittlePersonEmotion.Startled:
                    return new Color(0.25f, 0.45f, 1f, 1f);
                default:
                    return fallback;
            }
        }

        static float GlowScale(LittlePerson person)
        {
            switch (person.CurrentBehavior)
            {
                case LittlePersonBehaviorKind.ClimbBar:
                case LittlePersonBehaviorKind.TransferToSurface:
                case LittlePersonBehaviorKind.SurfaceWalk:
                case LittlePersonBehaviorKind.RideSurface:
                    return 2.25f;
                case LittlePersonBehaviorKind.Falling:
                    return 3.2f;
            }

            switch (person.Emotion)
            {
                case LittlePersonEmotion.Curious:
                    return 2.1f;
                case LittlePersonEmotion.Startled:
                    return 2.7f;
                default:
                    return 1.55f;
            }
        }

        static float GlowAlpha(LittlePerson person)
        {
            switch (person.CurrentBehavior)
            {
                case LittlePersonBehaviorKind.ClimbBar:
                case LittlePersonBehaviorKind.TransferToSurface:
                case LittlePersonBehaviorKind.SurfaceWalk:
                case LittlePersonBehaviorKind.RideSurface:
                    return 0.38f;
                case LittlePersonBehaviorKind.Falling:
                    return 0.55f;
            }

            switch (person.Emotion)
            {
                case LittlePersonEmotion.Curious:
                    return 0.32f;
                case LittlePersonEmotion.Startled:
                    return 0.42f;
                default:
                    return 0.16f;
            }
        }
    }
}
