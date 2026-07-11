using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class AmbientObjectView : MonoBehaviour
    {
        readonly List<SpriteRenderer> cloudRenderers = new();
        readonly List<SpriteRenderer> sunRayRenderers = new();
        SpriteRenderer bodyRenderer;
        SpriteRenderer glowRenderer;
        SpriteRenderer contactRenderer;

        public int SourceObjectId { get; private set; }

        public void Initialize()
        {
            contactRenderer = CreateRenderer("Contact", RuntimeSpriteFactory.Circle, -9);
            glowRenderer = CreateRenderer("Glow", RuntimeSpriteFactory.Circle, -3);
            bodyRenderer = CreateRenderer("Body", RuntimeSpriteFactory.Star, 2);

            for (var i = 0; i < 5; i++)
            {
                cloudRenderers.Add(CreateRenderer($"Cloud {i + 1}", RuntimeSpriteFactory.Circle, 2));
            }

            for (var i = 0; i < 8; i++)
            {
                sunRayRenderers.Add(CreateRenderer($"Sun Ray {i + 1}", RuntimeSpriteFactory.Circle, 1));
            }
        }

        public void Render(
            AmbientObject ambientObject,
            AmbientObjectTypeMaster typeMaster,
            NormalizedScreenMapper mapper,
            bool debugEnabled)
        {
            SourceObjectId = ambientObject.Id;
            transform.position = mapper.ToWorld(ambientObject.Position);
            transform.rotation = Quaternion.identity;

            contactRenderer.enabled = debugEnabled;
            if (debugEnabled)
            {
                contactRenderer.transform.localScale = Vector3.one * mapper.ToWorldRadius(ambientObject.ContactRadius) * 2f;
                contactRenderer.color = new Color(typeMaster.Color.r, typeMaster.Color.g, typeMaster.Color.b, 0.08f);
            }

            if (ambientObject.Kind == AmbientObjectKind.Cloud)
            {
                RenderCloud(ambientObject, typeMaster, mapper);
            }
            else
            {
                RenderSun(ambientObject, typeMaster, mapper);
            }
        }

        void RenderCloud(AmbientObject ambientObject, AmbientObjectTypeMaster typeMaster, NormalizedScreenMapper mapper)
        {
            bodyRenderer.enabled = false;
            foreach (var renderer in sunRayRenderers)
            {
                renderer.enabled = false;
            }


            var baseScale = mapper.ToWorldScale(ambientObject.Size);
            var color = typeMaster.Color;
            color.a = ambientObject.State == AmbientObjectState.Reacting ? 0.95f : 0.72f;

            var glowColor = Color.Lerp(typeMaster.Color, Color.white, 0.45f);
            glowColor.a = ambientObject.State == AmbientObjectState.Reacting ? 0.26f : 0.12f;
            glowRenderer.enabled = true;
            glowRenderer.sprite = RuntimeSpriteFactory.Circle;
            glowRenderer.transform.localScale = new Vector3(baseScale.x * 1.25f, baseScale.y * 1.65f, 1f);
            glowRenderer.color = glowColor;

            for (var i = 0; i < cloudRenderers.Count; i++)
            {
                var renderer = cloudRenderers[i];
                renderer.enabled = true;
                renderer.color = color;
            }

            SetCloudPart(0, new Vector2(-0.30f, 0.02f), new Vector2(0.48f, 0.62f), baseScale);
            SetCloudPart(1, new Vector2(-0.05f, -0.08f), new Vector2(0.55f, 0.78f), baseScale);
            SetCloudPart(2, new Vector2(0.22f, 0.00f), new Vector2(0.50f, 0.64f), baseScale);
            SetCloudPart(3, new Vector2(0.03f, 0.14f), new Vector2(0.74f, 0.45f), baseScale);
            SetCloudPart(4, new Vector2(-0.18f, 0.16f), new Vector2(0.44f, 0.38f), baseScale);
        }

        void RenderSun(AmbientObject ambientObject, AmbientObjectTypeMaster typeMaster, NormalizedScreenMapper mapper)
        {
            foreach (var renderer in cloudRenderers)
            {
                renderer.enabled = false;
            }

            var scale = mapper.ToWorldScale(ambientObject.Size);
            var color = typeMaster.Color;
            if (ambientObject.State == AmbientObjectState.Cooldown)
            {
                color = Color.Lerp(color, Color.gray, 0.45f);
                color.a = 0.42f;
            }

            bodyRenderer.enabled = true;
            bodyRenderer.sprite = RuntimeSpriteFactory.Circle;
            bodyRenderer.transform.localPosition = Vector3.zero;
            bodyRenderer.transform.localScale = scale * 0.72f;
            bodyRenderer.color = color;

            var rayColor = Color.Lerp(color, Color.white, 0.28f);
            for (var i = 0; i < sunRayRenderers.Count; i++)
            {
                var angleDegrees = i * 360f / sunRayRenderers.Count;
                var direction = Quaternion.Euler(0f, 0f, angleDegrees) * Vector3.up;
                var ray = sunRayRenderers[i];
                ray.enabled = true;
                ray.transform.localPosition = direction * scale.x * 0.62f;
                ray.transform.localScale = new Vector3(scale.x * 0.12f, scale.y * 0.42f, 1f);
                ray.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
                ray.color = rayColor;
            }

            var pulse = 1f + Mathf.Sin(Time.time * 2.0f + ambientObject.Id) * 0.04f;
            var glowColor = Color.Lerp(typeMaster.Color, Color.white, 0.6f);
            glowColor.a = ambientObject.State == AmbientObjectState.Reacting ? 0.34f : 0.16f;
            glowRenderer.enabled = true;
            glowRenderer.sprite = RuntimeSpriteFactory.Circle;
            glowRenderer.transform.localScale = scale * 2.0f * pulse;
            glowRenderer.color = glowColor;
        }

        void SetCloudPart(int index, Vector2 offset, Vector2 scaleMultiplier, Vector3 baseScale)
        {
            var renderer = cloudRenderers[index];
            renderer.transform.localPosition = new Vector3(offset.x * baseScale.x, offset.y * baseScale.y, 0f);
            renderer.transform.localScale = new Vector3(baseScale.x * scaleMultiplier.x, baseScale.y * scaleMultiplier.y, 1f);
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
