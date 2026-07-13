using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation.Fairy
{
    internal sealed class FairyView
    {
        public Transform Root;
        public SpriteRenderer Glow;
        public SpriteRenderer LeftWing;
        public SpriteRenderer RightWing;
        public SpriteRenderer Body;
        public SpriteRenderer Head;
    }

    internal sealed class FairyRenderer
    {
        readonly Transform root;

        public FairyRenderer(Transform root)
        {
            this.root = root;
        }

        public FairyView Create(int index, Color color, FairySettings settings)
        {
            var particleRoot = new GameObject($"particle_{index}").transform;
            particleRoot.SetParent(root, false);
            var view = new FairyView
            {
                Root = particleRoot,
                Glow = CreateRenderer(particleRoot, "Glow", -4),
                LeftWing = CreateRenderer(particleRoot, "Left Wing", 9),
                RightWing = CreateRenderer(particleRoot, "Right Wing", 9),
                Body = CreateRenderer(particleRoot, "Body", 10),
                Head = CreateRenderer(particleRoot, "Head", 11)
            };
            view.Head.transform.localPosition = new Vector3(settings.Size * 0.12f, settings.Size * 0.16f);
            view.Body.transform.localScale = Vector3.one * settings.Size;
            view.Head.transform.localScale = Vector3.one * settings.Size * 0.58f;
            view.Body.color = color;
            view.Head.color = Color.Lerp(color, Color.white, 0.35f);
            return view;
        }

        public void Destroy(FairyView view)
        {
            if (view?.Root != null) Object.Destroy(view.Root.gameObject);
        }

        public void Render(FairyView view, Vector3 worldPosition, Vector2 velocity, Color bodyColor, int index, FairySettings settings)
        {
            view.Root.position = worldPosition;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                var angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                view.Root.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            var pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 2.2f + index * 0.7f);
            var glow = Color.Lerp(bodyColor, Color.white, 0.5f);
            glow.a = pulse * 0.4f;
            view.Glow.color = glow;
            view.Glow.transform.localScale = Vector3.one * settings.Size * (2f + pulse * 0.6f);
            RenderWings(view, bodyColor, index, settings);
        }

        void RenderWings(FairyView view, Color bodyColor, int index, FairySettings settings)
        {
            view.LeftWing.enabled = settings.ShowWings;
            view.RightWing.enabled = settings.ShowWings;
            if (!settings.ShowWings) return;

            var flap = 0.5f + 0.5f * Mathf.Sin(Time.time * settings.WingFlapSpeed + index * 0.83f);
            var width = settings.Size * settings.WingWidthRatio * Mathf.Lerp(0.8f, 1.08f, flap);
            var length = settings.Size * settings.WingLengthRatio * Mathf.Lerp(1.08f, 0.86f, flap);
            var tilt = Mathf.Lerp(18f, 42f, flap);
            var color = Color.Lerp(bodyColor, Color.white, 0.78f);
            color.a = settings.WingAlpha * Mathf.Lerp(0.72f, 1f, flap);
            view.LeftWing.color = color;
            view.RightWing.color = color;
            view.LeftWing.transform.localPosition = new Vector3(settings.Size * settings.WingBackOffsetRatio, settings.Size * settings.WingSideOffsetRatio);
            view.RightWing.transform.localPosition = new Vector3(settings.Size * settings.WingBackOffsetRatio, -settings.Size * settings.WingSideOffsetRatio);
            view.LeftWing.transform.localScale = new Vector3(width, length, 1f);
            view.RightWing.transform.localScale = new Vector3(width, length, 1f);
            view.LeftWing.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            view.RightWing.transform.localRotation = Quaternion.Euler(0f, 0f, -tilt);
        }

        static SpriteRenderer CreateRenderer(Transform parent, string name, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.Circle;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
