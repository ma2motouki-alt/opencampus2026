using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class LittlePersonView : MonoBehaviour
    {
        const string ResourceRoot = "LittlePeople/";
        const float SpritePixelsPerUnit = 1024f;

        [Header("Sprite Animation")]
        [SerializeField] float frameSeconds = 0.18f;
        [SerializeField] float spriteHeightMultiplier = 2.8f;
        [SerializeField] bool flipAlongMovement = true;
        [SerializeField] float edgeVisualOffsetNormalized = 0.03f;
        [SerializeField] float startledPulseScale = 0.12f;
        [SerializeField] float fallingSpinDegrees = 10f;

        SpriteRenderer glowRenderer;
        SpriteRenderer spriteRenderer;
        float animationSeed;

        static readonly Dictionary<int, Sprite[]> spriteCache = new();

        public void Initialize()
        {
            animationSeed = Random.Range(0f, 100f);
            glowRenderer = CreateRenderer("Glow", RuntimeSpriteFactory.Circle, -4);
            spriteRenderer = CreateRenderer("Sprite", RuntimeSpriteFactory.Circle, 11);
        }

        public void Render(LittlePerson person, LittlePersonArchetypeMaster archetype, NormalizedScreenMapper mapper)
        {
            transform.position = mapper.ToWorld(person.Position) + EdgeVisualOffset(person, mapper);

            var unit = mapper.ToWorldRadius(archetype.Size);
            var targetHeight = Mathf.Max(0.001f, unit * spriteHeightMultiplier);
            var sprites = GetSpriteSet(archetype.Id);
            var hasSprite = sprites[0] != null || sprites[1] != null;
            var frameIndex = ShouldAnimate(person) ? CurrentFrameIndex(person) : 0;
            var sprite = sprites[frameIndex] != null ? sprites[frameIndex] : sprites[0];

            spriteRenderer.sprite = hasSprite ? sprite : RuntimeSpriteFactory.Circle;
            spriteRenderer.color = hasSprite ? Color.white : archetype.BodyColor;
            spriteRenderer.flipX = flipAlongMovement && ShouldFlipX(person);

            var pulse = person.Emotion == LittlePersonEmotion.Startled
                ? 1f + startledPulseScale * (0.5f + 0.5f * Mathf.Sin((Time.time + animationSeed) * 14f))
                : 1f;
            var spriteScale = hasSprite ? ScaleForTargetHeight(spriteRenderer.sprite, targetHeight) : unit;
            spriteRenderer.transform.localPosition = Vector3.zero;
            spriteRenderer.transform.localRotation = Quaternion.identity;
            spriteRenderer.transform.localScale = Vector3.one * spriteScale * pulse;

            var glowColor = GlowColor(person, archetype.BodyColor);
            glowColor.a = GlowAlpha(person);
            glowRenderer.color = glowColor;
            glowRenderer.transform.localPosition = hasSprite
                ? new Vector3(0f, targetHeight * 0.42f, 0f)
                : Vector3.zero;
            glowRenderer.transform.localRotation = Quaternion.identity;
            glowRenderer.transform.localScale = Vector3.one * unit * GlowScale(person);

            transform.rotation = Quaternion.Euler(0f, 0f, RotationDegrees(person));
        }

        int CurrentFrameIndex(LittlePerson person)
        {
            var offset = (person.PreferenceSeed & 0xffff) * 0.001f + animationSeed;
            var safeFrameSeconds = Mathf.Max(0.03f, frameSeconds);
            return Mathf.FloorToInt((Time.time + offset) / safeFrameSeconds) % 2;
        }

        bool ShouldAnimate(LittlePerson person)
        {
            if (person.CurrentBehavior == LittlePersonBehaviorKind.Falling)
            {
                return false;
            }

            return person.Velocity.sqrMagnitude > 0.000001f ||
                   person.CurrentBehavior == LittlePersonBehaviorKind.TransferToSurface ||
                   person.CurrentBehavior == LittlePersonBehaviorKind.SurfaceWalk ||
                   person.CurrentBehavior == LittlePersonBehaviorKind.RideSurface;
        }

        float RotationDegrees(LittlePerson person)
        {
            if (person.CurrentBehavior == LittlePersonBehaviorKind.EdgeWalk)
            {
                return EdgeGroundRotationDegrees(person.Position);
            }

            if (person.CurrentBehavior == LittlePersonBehaviorKind.Falling)
            {
                return EdgeGroundRotationDegrees(person.Position) +
                       Mathf.Sin((Time.time + animationSeed) * 4f) * fallingSpinDegrees;
            }

            if (person.Velocity.sqrMagnitude > 0.000001f)
            {
                var worldVelocity = NormalizedVelocityToWorldDirection(person.Velocity);
                return Mathf.Atan2(worldVelocity.y, worldVelocity.x) * Mathf.Rad2Deg - 90f;
            }

            return EdgeGroundRotationDegrees(person.Position);
        }

        static float EdgeGroundRotationDegrees(Vector2 normalizedPosition)
        {
            var top = normalizedPosition.y;
            var bottom = 1f - normalizedPosition.y;
            var left = normalizedPosition.x;
            var right = 1f - normalizedPosition.x;
            var min = Mathf.Min(Mathf.Min(top, bottom), Mathf.Min(left, right));

            if (Mathf.Approximately(min, top))
            {
                return 180f;
            }

            if (Mathf.Approximately(min, left))
            {
                return -90f;
            }

            if (Mathf.Approximately(min, right))
            {
                return 90f;
            }

            return 0f;
        }

        Vector3 EdgeVisualOffset(LittlePerson person, NormalizedScreenMapper mapper)
        {
            if (person.CurrentBehavior != LittlePersonBehaviorKind.EdgeWalk)
            {
                return Vector3.zero;
            }

            var direction = EdgeOutwardDirection(person.Position);
            return new Vector3(
                direction.x * mapper.WorldWidth * edgeVisualOffsetNormalized,
                -direction.y * mapper.WorldHeight * edgeVisualOffsetNormalized,
                0f);
        }

        static Vector2 EdgeOutwardDirection(Vector2 normalizedPosition)
        {
            var top = normalizedPosition.y;
            var bottom = 1f - normalizedPosition.y;
            var left = normalizedPosition.x;
            var right = 1f - normalizedPosition.x;
            var min = Mathf.Min(Mathf.Min(top, bottom), Mathf.Min(left, right));

            if (Mathf.Approximately(min, top))
            {
                return new Vector2(0f, -1f);
            }

            if (Mathf.Approximately(min, left))
            {
                return new Vector2(-1f, 0f);
            }

            if (Mathf.Approximately(min, right))
            {
                return new Vector2(1f, 0f);
            }

            return new Vector2(0f, 1f);
        }

        bool ShouldFlipX(LittlePerson person)
        {
            if (person.Velocity.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            var rotation = RotationDegrees(person);
            var worldVelocity = NormalizedVelocityToWorldDirection(person.Velocity);
            var inverse = Quaternion.Euler(0f, 0f, -rotation);
            var localVelocity = inverse * new Vector3(worldVelocity.x, worldVelocity.y, 0f);
            return localVelocity.x < 0f;
        }

        static Vector2 NormalizedVelocityToWorldDirection(Vector2 normalizedVelocity)
        {
            return new Vector2(normalizedVelocity.x, -normalizedVelocity.y);
        }

        static float ScaleForTargetHeight(Sprite sprite, float targetHeight)
        {
            if (sprite == null || sprite.bounds.size.y <= 0.0001f)
            {
                return targetHeight;
            }

            return targetHeight / sprite.bounds.size.y;
        }

        static Sprite[] GetSpriteSet(int archetypeId)
        {
            if (spriteCache.TryGetValue(archetypeId, out var sprites))
            {
                return sprites;
            }

            var prefix = SpritePrefix(archetypeId);
            sprites = new[]
            {
                LoadSprite($"{prefix}1"),
                LoadSprite($"{prefix}2")
            };
            spriteCache.Add(archetypeId, sprites);
            return sprites;
        }

        static Sprite LoadSprite(string resourceName)
        {
            var texture = Resources.Load<Texture2D>(ResourceRoot + resourceName);
            if (texture == null)
            {
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0f),
                SpritePixelsPerUnit);
        }

        static string SpritePrefix(int archetypeId)
        {
            switch (archetypeId)
            {
                case 1:
                    return "blue";
                case 2:
                    return "green";
                case 3:
                    return "red";
                case 4:
                    return "yellow";
                default:
                    return "blue";
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
