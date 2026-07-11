using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class RainbowView : MonoBehaviour
    {
        static readonly Color[] BandColors =
        {
            new(1f, 0.25f, 0.30f, 1f),
            new(1f, 0.55f, 0.20f, 1f),
            new(1f, 0.90f, 0.25f, 1f),
            new(0.35f, 0.90f, 0.42f, 1f),
            new(0.25f, 0.82f, 0.95f, 1f),
            new(0.30f, 0.48f, 1f, 1f),
            new(0.68f, 0.36f, 0.95f, 1f)
        };

        readonly List<LineRenderer> bandRenderers = new();
        readonly List<Material> runtimeMaterials = new();

        public int SourceRainbowId { get; private set; }

        public void Initialize()
        {
            for (var i = 0; i < BandColors.Length; i++)
            {
                var child = new GameObject($"Rainbow Band {i + 1}");
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.loop = false;
                renderer.alignment = LineAlignment.View;
                renderer.textureMode = LineTextureMode.Stretch;
                renderer.numCornerVertices = 3;
                renderer.numCapVertices = 2;
                renderer.sortingOrder = 1 - i;
                var material = new Material(Shader.Find("Sprites/Default"));
                renderer.material = material;
                runtimeMaterials.Add(material);
                bandRenderers.Add(renderer);
            }
        }

        public void Render(RainbowInstance rainbow, NormalizedScreenMapper mapper)
        {
            SourceRainbowId = rainbow.Id;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            var bandWidth = Mathf.Max(0.02f, mapper.ToWorldRadius(0.008f));
            var alpha = rainbow.Opacity * 0.78f;
            for (var bandIndex = 0; bandIndex < bandRenderers.Count; bandIndex++)
            {
                var renderer = bandRenderers[bandIndex];
                renderer.enabled = alpha > 0.001f;
                renderer.widthMultiplier = bandWidth;
                var color = BandColors[bandIndex];
                color.a = alpha;
                renderer.startColor = color;
                renderer.endColor = color;
                renderer.positionCount = rainbow.PathPoints.Count;

                var verticalOffset = Vector3.down * bandIndex * bandWidth * 0.82f;
                for (var pointIndex = 0; pointIndex < rainbow.PathPoints.Count; pointIndex++)
                {
                    renderer.SetPosition(
                        pointIndex,
                        mapper.ToWorld(rainbow.PathPoints[pointIndex]) + verticalOffset);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var material in runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }
    }
}
