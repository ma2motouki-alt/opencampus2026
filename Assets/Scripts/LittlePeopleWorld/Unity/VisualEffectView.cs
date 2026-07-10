using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class VisualEffectView : MonoBehaviour
    {
        readonly Dictionary<VisualEffectKind, IVisualEffectRenderer> proceduralRenderers = new();
        readonly NullVisualEffectRenderer nullRenderer = new();
        readonly PrefabVisualEffectRenderer prefabRenderer = new();
        IVisualEffectRenderer activeRenderer;

        public int SourceEffectId { get; private set; }

        public void Initialize()
        {
            Register(new RainColumnEffectRenderer());
            Register(new StarBurstEffectRenderer());
            prefabRenderer.Initialize(transform);
            activeRenderer = nullRenderer;
        }

        public void Render(
            VisualEffectInstance effect,
            VisualEffectMaster master,
            NormalizedScreenMapper mapper,
            float rainVisibleHeightRatio = 1f)
        {
            SourceEffectId = effect.Id;
            transform.position = mapper.ToWorld(effect.Position);
            transform.rotation = Quaternion.Euler(0f, 0f, -effect.AngleDegrees);

            var renderer = SelectRenderer(master);
            if (!ReferenceEquals(activeRenderer, renderer))
            {
                activeRenderer?.Hide();
                activeRenderer = renderer;
            }

            activeRenderer.Render(effect, master, mapper, rainVisibleHeightRatio);
        }

        void Register(IVisualEffectRenderer renderer)
        {
            renderer.Initialize(transform);
            proceduralRenderers.Add(renderer.Kind, renderer);
        }

        IVisualEffectRenderer SelectRenderer(VisualEffectMaster master)
        {
            if (master.RenderMode == VisualEffectRenderMode.Prefab)
            {
                return prefabRenderer;
            }

            return proceduralRenderers.TryGetValue(master.Kind, out var renderer) ? renderer : nullRenderer;
        }
    }

    interface IVisualEffectRenderer
    {
        VisualEffectKind Kind { get; }
        void Initialize(Transform root);
        void Render(VisualEffectInstance effect, VisualEffectMaster master, NormalizedScreenMapper mapper, float rainVisibleHeightRatio);
        void Hide();
    }

    sealed class NullVisualEffectRenderer : IVisualEffectRenderer
    {
        public VisualEffectKind Kind => VisualEffectKind.SoftGlow;

        public void Initialize(Transform root)
        {
        }

        public void Render(VisualEffectInstance effect, VisualEffectMaster master, NormalizedScreenMapper mapper, float rainVisibleHeightRatio)
        {
        }

        public void Hide()
        {
        }
    }

    sealed class RainColumnEffectRenderer : IVisualEffectRenderer
    {
        const int DropCount = 18;
        readonly List<SpriteRenderer> renderers = new();

        public VisualEffectKind Kind => VisualEffectKind.RainColumn;

        public void Initialize(Transform root)
        {
            for (var i = 0; i < DropCount; i++)
            {
                // 四角の棒 → 涙(teardrop)型スプライトに変更
                renderers.Add(CreateRenderer(root, $"Rain {i + 1}", RuntimeSpriteFactory.Teardrop, 1));
            }

            Hide();
        }

        public void Render(VisualEffectInstance effect, VisualEffectMaster master, NormalizedScreenMapper mapper, float rainVisibleHeightRatio)
        {
            var size = mapper.ToWorldScale(effect.Size);
            var width = size.x;
            var fullHeight = size.y;
            var visibleHeight = fullHeight * Mathf.Clamp01(rainVisibleHeightRatio);
            var alpha = master.Alpha * Mathf.Clamp01(effect.RemainingSeconds / 0.18f);
            if (visibleHeight <= 0.0001f || alpha <= 0.0001f)
            {
                Hide();
                return;
            }

            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                renderer.enabled = true;

                var seed = Mathf.Repeat(i * 0.3819f, 1f);
                var x = (seed - 0.5f) * width;
                var phase = Mathf.Repeat(effect.AgeSeconds * master.PulseSpeed * 0.22f + i * 0.137f, 1f);
                var y = -phase * visibleHeight;
                // しずく1粒の大きさ。縦長(高さ>幅)にして水滴らしくする。
                // master.DropSizeScale で全体のサイズを独立して調整できる(既定1f)。
                var dropLength = Mathf.Lerp(fullHeight * 0.10f, fullHeight * 0.18f, Mathf.Repeat(seed * 3.1f, 1f)) * master.DropSizeScale;
                var dropWidth = dropLength * 0.42f;

                renderer.transform.localPosition = new Vector3(x, y, 0f);
                // Teardropは元々「上がとがり下が丸い」形状なので、そのままの向きで使う。
                // 落下方向(下向き)にとがった先端が来ないよう、回転は傾き演出だけにとどめる。
                renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(i * 1.7f) * 8f);
                renderer.transform.localScale = new Vector3(dropWidth, dropLength, 1f);

                var color = master.Color;
                color.a = alpha * Mathf.Lerp(0.55f, 1f, phase);
                renderer.color = color;
            }
        }

        public void Hide()
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
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
    }

    sealed class StarBurstEffectRenderer : IVisualEffectRenderer
    {
        const int RayCount = 16;
        readonly List<SpriteRenderer> renderers = new();

        public VisualEffectKind Kind => VisualEffectKind.StarBurst;

        public void Initialize(Transform root)
        {
            for (var i = 0; i < RayCount; i++)
            {
                renderers.Add(CreateRenderer(root, $"Star Ray {i + 1}", RuntimeSpriteFactory.Square, 7));
            }

            Hide();
        }

        public void Render(VisualEffectInstance effect, VisualEffectMaster master, NormalizedScreenMapper mapper, float rainVisibleHeightRatio)
        {
            var size = mapper.ToWorldScale(effect.Size);
            var radius = Mathf.Max(size.x, size.y) * 0.5f;
            var t = Mathf.SmoothStep(0f, 1f, effect.NormalizedAge);
            var alpha = master.Alpha * (1f - effect.NormalizedAge);

            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                renderer.enabled = true;

                var angle = 360f * i / renderers.Count + Mathf.Sin(i * 9.17f) * 8f;
                var radians = angle * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var length = Mathf.Lerp(radius * 0.18f, radius, t) * Mathf.Lerp(0.55f, 1.15f, Mathf.Repeat(i * 0.217f, 1f));
                var width = Mathf.Max(0.015f, radius * 0.035f);

                renderer.transform.localPosition = new Vector3(direction.x, direction.y, 0f) * length * 0.5f;
                renderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                renderer.transform.localScale = new Vector3(width, length, 1f);

                var color = Color.Lerp(master.Color, Color.white, Mathf.Repeat(i * 0.31f, 1f));
                color.a = alpha;
                renderer.color = color;
            }
        }

        public void Hide()
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = false;
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
    }

    sealed class PrefabVisualEffectRenderer : IVisualEffectRenderer
    {
        Transform root;
        GameObject instance;
        string currentAssetKey = string.Empty;

        public VisualEffectKind Kind => VisualEffectKind.SoftGlow;

        public void Initialize(Transform root)
        {
            this.root = root;
        }

        public void Render(VisualEffectInstance effect, VisualEffectMaster master, NormalizedScreenMapper mapper, float rainVisibleHeightRatio)
        {
            if (string.IsNullOrWhiteSpace(master.AssetKey))
            {
                Hide();
                return;
            }

            EnsureInstance(master.AssetKey);
            if (instance == null)
            {
                return;
            }

            instance.SetActive(true);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = mapper.ToWorldScale(effect.Size);
        }

        public void Hide()
        {
            if (instance != null)
            {
                instance.SetActive(false);
            }
        }

        void EnsureInstance(string assetKey)
        {
            if (instance != null && currentAssetKey == assetKey)
            {
                return;
            }

            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
                instance = null;
            }

            currentAssetKey = assetKey;
            var prefab = Resources.Load<GameObject>(assetKey);
            if (prefab == null)
            {
                return;
            }

            instance = UnityEngine.Object.Instantiate(prefab, root, false);
        }
    }
}
