using System.Collections.Generic;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Plants
{
    internal sealed class PlantView : MonoBehaviour
    {
        SpriteRenderer stem;
        readonly List<SpriteRenderer> leaves = new();
        readonly List<SpriteRenderer> veins = new();
        SpriteRenderer flower;
        SpriteRenderer flowerCenter;

        public void Initialize()
        {
            stem = CreateRenderer("Stem", RuntimeSpriteFactory.Circle, 6);
            flower = CreateRenderer("Flower", RuntimeSpriteFactory.Star, 8);
            flowerCenter = CreateRenderer("FlowerCenter", RuntimeSpriteFactory.Circle, 9);
        }

        public void Render(PlantModel plant, Vector3 rootWorld, Vector3 bloomWorld, float unitsPerPixel, PlantSettings settings)
        {
            transform.position = rootWorld;
            var wilt = plant.WiltProgress01;
            var bloomLocal = bloomWorld - rootWorld;
            if (bloomLocal.sqrMagnitude < 0.000001f) bloomLocal = Vector3.up * 0.0005f;

            var stemHeight = Mathf.Max(0.0005f, bloomLocal.magnitude);
            var stemWidth = Mathf.Max(0.004f, settings.StemWidthPx * unitsPerPixel);
            var stemColor = Color.Lerp(new Color(0.35f, 0.82f, 0.42f), new Color(0.62f, 0.5f, 0.28f), wilt);
            stem.transform.localPosition = bloomLocal * 0.5f;
            stem.transform.localScale = new Vector3(stemWidth, stemHeight, 1f);
            stem.transform.localRotation = Quaternion.FromToRotation(Vector3.up, bloomLocal.normalized);
            stem.color = stemColor;

            RenderLeaves(bloomLocal, unitsPerPixel, settings, wilt, stemColor);

            var showFlower = plant.CurrentStage is PlantStage.Blooming or PlantStage.Wilting;
            flower.enabled = showFlower;
            flowerCenter.enabled = showFlower;
            if (!showFlower) return;

            var open = Mathf.Lerp(1f, 0.45f, wilt);
            var size = Mathf.Max(0.004f, settings.FlowerSizePx * unitsPerPixel);
            var color = Color.Lerp(new Color(1f, 0.55f, 0.75f), new Color(0.55f, 0.42f, 0.32f), wilt);
            color.a = Mathf.Lerp(1f, 0.4f, wilt);
            flower.transform.localPosition = bloomLocal;
            flower.transform.localScale = Vector3.one * size * open;
            flower.transform.localRotation = Quaternion.identity;
            flower.color = color;
            flowerCenter.transform.localPosition = bloomLocal;
            flowerCenter.transform.localScale = Vector3.one * size * 0.42f * open;
            flowerCenter.transform.localRotation = Quaternion.identity;
            flowerCenter.color = Color.Lerp(Color.white, color, 0.35f);
        }

        void RenderLeaves(Vector3 bloomLocal, float unitsPerPixel, PlantSettings settings, float wilt, Color stemColor)
        {
            EnsureLeafRenderers(Mathf.Max(0, settings.LeafCount));
            for (var i = 0; i < leaves.Count; i++)
            {
                var show = LeafLayout.TryGet(i, bloomLocal, unitsPerPixel, settings, out var layout);
                leaves[i].enabled = show;
                veins[i].enabled = show;
                if (!show) continue;

                var rotation = Quaternion.FromToRotation(Vector3.up, layout.Direction);
                var color = Color.Lerp(new Color(0.24f, 0.76f, 0.34f, settings.LeafAlpha), new Color(0.55f, 0.44f, 0.24f, settings.LeafAlpha * 0.66f), wilt);
                color = Color.Lerp(color, Color.white, i % 2 == 0 ? 0.05f : 0.12f);
                color.a = Mathf.Lerp(settings.LeafAlpha, settings.LeafAlpha * 0.52f, wilt);
                leaves[i].transform.localPosition = layout.LocalPosition;
                leaves[i].transform.localScale = new Vector3(layout.WidthWorld * layout.Scale, layout.LengthWorld * layout.Scale, 1f);
                leaves[i].transform.localRotation = rotation;
                leaves[i].color = color;

                var veinColor = Color.Lerp(new Color(0.78f, 1f, 0.58f, settings.LeafVeinAlpha), stemColor, 0.25f);
                veinColor.a = Mathf.Lerp(settings.LeafVeinAlpha, settings.LeafVeinAlpha * 0.25f, wilt);
                veins[i].transform.localPosition = layout.LocalPosition + layout.Direction * layout.LengthWorld * layout.Scale * 0.38f;
                veins[i].transform.localScale = new Vector3(Mathf.Max(0.001f, layout.WidthWorld * 0.08f * layout.Scale), Mathf.Max(0.002f, layout.LengthWorld * 0.58f * layout.Scale), 1f);
                veins[i].transform.localRotation = rotation;
                veins[i].color = veinColor;
            }
        }

        void EnsureLeafRenderers(int count)
        {
            while (leaves.Count < count)
            {
                var number = leaves.Count + 1;
                leaves.Add(CreateRenderer($"Leaf {number}", RuntimeSpriteFactory.Leaf, 7));
                veins.Add(CreateRenderer($"Leaf Vein {number}", RuntimeSpriteFactory.Circle, 8));
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
