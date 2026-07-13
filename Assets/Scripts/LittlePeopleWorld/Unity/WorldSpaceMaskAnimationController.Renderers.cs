using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed partial class WorldSpaceMaskAnimationController
    {
        void EnsureMaskSprite()
        {
            if (maskRenderer == null || maskTexture == null)
            {
                return;
            }

            if (maskRenderer.sprite == null || maskRenderer.sprite.texture != maskTexture)
            {
                var pixelsPerUnit = MaskH / Mathf.Max(0.001f, mapper.WorldHeight);
                maskRenderer.sprite = Sprite.Create(
                    maskTexture,
                    new Rect(0, 0, MaskW, MaskH),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
            }

            maskRenderer.transform.position = Vector3.zero;
            var naturalWidth = maskRenderer.sprite.bounds.size.x;
            var naturalHeight = maskRenderer.sprite.bounds.size.y;
            maskRenderer.transform.localScale = new Vector3(
                mapper.WorldWidth / Mathf.Max(0.001f, naturalWidth),
                mapper.WorldHeight / Mathf.Max(0.001f, naturalHeight),
                1f);
        }

        void RenderMaskTexture()
        {
            var off = transparentMaskBackground ? new Color32(0, 0, 0, 0) : maskOffColor;

            for (var y = 0; y < MaskH; y++)
            {
                var sourceRow = y * MaskW;
                for (var x = 0; x < MaskW; x++)
                {
                    pixels[sourceRow + x] = effectiveMask[sourceRow + x] ? maskOnColor : off;
                }
            }

            maskTexture.SetPixels32(pixels);
            maskTexture.Apply(false);
        }

        void RenderParticle(Particle particle, int index)
        {
            particle.Root.position = MaskToWorld(particle.Pos);

            if (particle.Vel.sqrMagnitude > 0.0001f)
            {
                var angle = Mathf.Atan2(particle.Vel.y, particle.Vel.x) * Mathf.Rad2Deg;
                particle.Root.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            var pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 2.2f + index * 0.7f);
            var glowColor = Color.Lerp(particle.BodyColor, Color.white, 0.5f);
            glowColor.a = pulse * 0.4f;
            particle.GlowRenderer.color = glowColor;
            particle.GlowRenderer.transform.localScale = Vector3.one * particleSize * (2.0f + pulse * 0.6f);

            RenderParticleWings(particle, index);
        }

        void RenderParticleWings(Particle particle, int index)
        {
            if (particle.LeftWingRenderer == null || particle.RightWingRenderer == null)
            {
                return;
            }

            particle.LeftWingRenderer.enabled = showParticleWings;
            particle.RightWingRenderer.enabled = showParticleWings;
            if (!showParticleWings)
            {
                return;
            }

            var wingSideOffset = particleSize * particleWingSideOffsetRatio;
            var wingBackOffset = particleSize * particleWingBackOffsetRatio;
            var flap = 0.5f + 0.5f * Mathf.Sin(Time.time * particleWingFlapSpeed + index * 0.83f);
            var wingWidth = particleSize * particleWingWidthRatio * Mathf.Lerp(0.8f, 1.08f, flap);
            var wingLength = particleSize * particleWingLengthRatio * Mathf.Lerp(1.08f, 0.86f, flap);
            var wingTilt = Mathf.Lerp(18f, 42f, flap);
            var wingColor = Color.Lerp(particle.BodyColor, Color.white, 0.78f);
            wingColor.a = particleWingAlpha * Mathf.Lerp(0.72f, 1f, flap);

            particle.LeftWingRenderer.color = wingColor;
            particle.RightWingRenderer.color = wingColor;
            particle.LeftWingRenderer.transform.localPosition = new Vector3(wingBackOffset, wingSideOffset, 0f);
            particle.RightWingRenderer.transform.localPosition = new Vector3(wingBackOffset, -wingSideOffset, 0f);
            particle.LeftWingRenderer.transform.localScale = new Vector3(wingWidth, wingLength, 1f);
            particle.RightWingRenderer.transform.localScale = new Vector3(wingWidth, wingLength, 1f);
            particle.LeftWingRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, wingTilt);
            particle.RightWingRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -wingTilt);
        }

        void SyncPlantViews()
        {
            var liveIds = new HashSet<int>();
            var worldUnitsPerMaskPx = mapper.WorldHeight / Mathf.Max(1f, MaskH - 1f);

            foreach (var plant in plants)
            {
                liveIds.Add(plant.Id);
                if (!plantViews.TryGetValue(plant.Id, out var view))
                {
                    var viewObject = new GameObject($"plant_{plant.Id}");
                    viewObject.transform.SetParent(plantRoot, false);
                    view = viewObject.AddComponent<PlantViewRuntime>();
                    view.Initialize();
                    plantViews[plant.Id] = view;
                }

                var rootWorldPosition = MaskToWorld(plant.Position);
                var bloomWorldPosition = MaskToWorld(plant.BloomPosition);
                view.Render(
                    plant,
                    rootWorldPosition,
                    bloomWorldPosition,
                    worldUnitsPerMaskPx,
                    plantStemWidthPx,
                    plantFlowerSizePx,
                    plantLeafCount,
                    plantLeafLengthPx,
                    plantLeafWidthPx,
                    plantLeafStartRatio,
                    plantLeafEndRatio,
                    plantLeafAngleDegrees,
                    plantLeafAlpha,
                    plantLeafVeinAlpha);
            }

            var deadIds = new List<int>();
            foreach (var pair in plantViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (plantViews[id] != null)
                {
                    Destroy(plantViews[id].gameObject);
                }

                plantViews.Remove(id);
            }
        }

        static SpriteRenderer CreateRenderer(Transform root, string name, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        sealed class PlantViewRuntime : MonoBehaviour
        {
            SpriteRenderer stemGlowRenderer;
            SpriteRenderer stemRenderer;
            readonly List<SpriteRenderer> leafRenderers = new();
            readonly List<SpriteRenderer> leafVeinRenderers = new();
            SpriteRenderer flowerGlowRenderer;
            SpriteRenderer flowerRenderer;
            SpriteRenderer flowerCenterRenderer;

            public void Initialize()
            {
                stemGlowRenderer = CreateRenderer("StemGlow", RuntimeSpriteFactory.Circle, -3);
                stemRenderer = CreateRenderer("Stem", RuntimeSpriteFactory.Circle, 6);
                flowerGlowRenderer = CreateRenderer("FlowerGlow", RuntimeSpriteFactory.Circle, 7);
                flowerRenderer = CreateRenderer("Flower", RuntimeSpriteFactory.Star, 8);
                flowerCenterRenderer = CreateRenderer("FlowerCenter", RuntimeSpriteFactory.Circle, 9);
            }

            public void Render(
                PlantModel plant,
                Vector3 rootWorldPosition,
                Vector3 bloomWorldPosition,
                float worldUnitsPerMaskPx,
                float stemWidthPx,
                float flowerSizePx,
                int leafCount,
                float leafLengthPx,
                float leafWidthPx,
                float leafStartRatio,
                float leafEndRatio,
                float leafAngleDegrees,
                float leafAlpha,
                float leafVeinAlpha)
            {
                transform.position = rootWorldPosition;

                var wilt = plant.WiltProgress01;
                var bloomLocalPosition = bloomWorldPosition - rootWorldPosition;
                if (bloomLocalPosition.sqrMagnitude < 0.000001f)
                {
                    bloomLocalPosition = Vector3.up * 0.0005f;
                }

                var stemHeightWorld = Mathf.Max(0.0005f, bloomLocalPosition.magnitude);
                var stemWidthWorld = Mathf.Max(0.004f, stemWidthPx * worldUnitsPerMaskPx);
                var stemCenter = bloomLocalPosition * 0.5f;
                var stemColor = Color.Lerp(new Color(0.35f, 0.82f, 0.42f, 1f), new Color(0.62f, 0.5f, 0.28f, 1f), wilt);

                stemRenderer.transform.localPosition = stemCenter;
                stemRenderer.transform.localScale = new Vector3(stemWidthWorld, stemHeightWorld, 1f);
                stemRenderer.transform.localRotation = Quaternion.FromToRotation(Vector3.up, bloomLocalPosition.normalized);
                stemRenderer.color = stemColor;

                // 茎のglow(発光ハロー)。茎本体と同じ位置・向きで、太さと丈をひと回り大きくして重ねる。
                stemGlowRenderer.transform.localPosition = stemCenter;
                stemGlowRenderer.transform.localScale = new Vector3(stemWidthWorld * 3.2f, stemHeightWorld * 1.05f, 1f);
                stemGlowRenderer.transform.localRotation = stemRenderer.transform.localRotation;
                var stemGlowColor = Color.Lerp(stemColor, Color.white, 0.5f);
                stemGlowColor.a = Mathf.Lerp(0.16f, 0.04f, wilt);
                stemGlowRenderer.color = stemGlowColor;

                RenderLeaves(
                    plant,
                    bloomLocalPosition,
                    worldUnitsPerMaskPx,
                    leafCount,
                    leafLengthPx,
                    leafWidthPx,
                    leafStartRatio,
                    leafEndRatio,
                    leafAngleDegrees,
                    leafAlpha,
                    leafVeinAlpha,
                    wilt,
                    stemColor);

                var showFlower = plant.CurrentStage is PlantStage.Blooming or PlantStage.Wilting;
                flowerRenderer.enabled = showFlower;
                flowerCenterRenderer.enabled = showFlower;
                flowerGlowRenderer.enabled = showFlower;
                if (!showFlower)
                {
                    return;
                }

                var bloomOpen = Mathf.Lerp(1f, 0.45f, wilt);
                var flowerBaseScale = Mathf.Max(0.004f, flowerSizePx * worldUnitsPerMaskPx);
                var flowerColor = Color.Lerp(new Color(1f, 0.55f, 0.75f, 1f), new Color(0.55f, 0.42f, 0.32f, 1f), wilt);
                flowerColor.a = Mathf.Lerp(1f, 0.4f, wilt);

                flowerRenderer.transform.localPosition = bloomLocalPosition;
                flowerRenderer.transform.localScale = Vector3.one * flowerBaseScale * bloomOpen;
                flowerRenderer.transform.localRotation = Quaternion.identity;
                flowerRenderer.color = flowerColor;

                flowerCenterRenderer.transform.localPosition = bloomLocalPosition;
                flowerCenterRenderer.transform.localScale = Vector3.one * flowerBaseScale * 0.42f * bloomOpen;
                flowerCenterRenderer.transform.localRotation = Quaternion.identity;
                flowerCenterRenderer.color = Color.Lerp(Color.white, flowerColor, 0.35f);

                // 花のglow(発光ハロー)。開閉(bloomOpen)には連動させず、常に一定サイズで淡く発光させる。
                flowerGlowRenderer.transform.localPosition = bloomLocalPosition;
                flowerGlowRenderer.transform.localScale = Vector3.one * flowerBaseScale * 2.4f;
                flowerGlowRenderer.transform.localRotation = Quaternion.identity;
                var flowerGlowColor = Color.Lerp(flowerColor, Color.white, 0.55f);
                flowerGlowColor.a = Mathf.Lerp(0.32f, 0.05f, wilt);
                flowerGlowRenderer.color = flowerGlowColor;
            }

            void RenderLeaves(
                PlantModel plant,
                Vector3 bloomLocalPosition,
                float worldUnitsPerMaskPx,
                int leafCount,
                float leafLengthPx,
                float leafWidthPx,
                float leafStartRatio,
                float leafEndRatio,
                float leafAngleDegrees,
                float leafAlpha,
                float leafVeinAlpha,
                float wilt,
                Color stemColor)
            {
                var count = Mathf.Max(0, leafCount);
                EnsureLeafRenderers(count);

                var stemLength = bloomLocalPosition.magnitude;
                var canShowLeaves = count > 0 && stemLength > 0.0005f;
                var baseLeafLengthWorld = Mathf.Max(0.004f, leafLengthPx * worldUnitsPerMaskPx);
                var growthScale = Mathf.Clamp01(stemLength / Mathf.Max(0.0005f, baseLeafLengthWorld * 2.2f));

                for (var i = 0; i < leafRenderers.Count; i++)
                {
                    var leafRenderer = leafRenderers[i];
                    var veinRenderer = leafVeinRenderers[i];
                    var show = canShowLeaves && i < count && growthScale > 0.05f;
                    leafRenderer.enabled = show;
                    veinRenderer.enabled = show;
                    if (!show)
                    {
                        continue;
                    }

                    if (!TryGetLeafTargetInfo(
                            i,
                            count,
                            bloomLocalPosition,
                            worldUnitsPerMaskPx,
                            leafLengthPx,
                            leafWidthPx,
                            leafStartRatio,
                            leafEndRatio,
                            leafAngleDegrees,
                            out var leaf))
                    {
                        leafRenderer.enabled = false;
                        veinRenderer.enabled = false;
                        continue;
                    }

                    var localPosition = leaf.LocalPosition;
                    var leafDirection = leaf.Direction;
                    var leafRotation = Quaternion.FromToRotation(Vector3.up, leafDirection);
                    var renderLeafLengthWorld = leaf.LengthWorld;
                    var renderLeafWidthWorld = leaf.WidthWorld;
                    var leafScale = leaf.Scale;

                    var leafColor = Color.Lerp(new Color(0.24f, 0.76f, 0.34f, leafAlpha), new Color(0.55f, 0.44f, 0.24f, leafAlpha * 0.66f), wilt);
                    leafColor = Color.Lerp(leafColor, Color.white, i % 2 == 0 ? 0.05f : 0.12f);
                    leafColor.a = Mathf.Lerp(leafAlpha, leafAlpha * 0.52f, wilt);

                    leafRenderer.transform.localPosition = localPosition;
                    leafRenderer.transform.localScale = new Vector3(renderLeafWidthWorld * leafScale, renderLeafLengthWorld * leafScale, 1f);
                    leafRenderer.transform.localRotation = leafRotation;
                    leafRenderer.color = leafColor;

                    var veinColor = Color.Lerp(new Color(0.78f, 1f, 0.58f, leafVeinAlpha), stemColor, 0.25f);
                    veinColor.a = Mathf.Lerp(leafVeinAlpha, leafVeinAlpha * 0.25f, wilt);
                    veinRenderer.transform.localPosition = localPosition + leafDirection * renderLeafLengthWorld * leafScale * 0.38f;
                    veinRenderer.transform.localScale = new Vector3(
                        Mathf.Max(0.001f, renderLeafWidthWorld * 0.08f * leafScale),
                        Mathf.Max(0.002f, renderLeafLengthWorld * 0.58f * leafScale),
                        1f);
                    veinRenderer.transform.localRotation = leafRotation;
                    veinRenderer.color = veinColor;
                }
            }

            void EnsureLeafRenderers(int count)
            {
                while (leafRenderers.Count < count)
                {
                    var index = leafRenderers.Count + 1;
                    leafRenderers.Add(CreateRenderer($"Leaf {index}", RuntimeSpriteFactory.Leaf, 7));
                    leafVeinRenderers.Add(CreateRenderer($"Leaf Vein {index}", RuntimeSpriteFactory.Circle, 8));
                }
            }

            SpriteRenderer CreateRenderer(string rendererName, Sprite sprite, int sortingOrder)
            {
                var child = new GameObject(rendererName);
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
                return renderer;
            }
        }
    }
}
