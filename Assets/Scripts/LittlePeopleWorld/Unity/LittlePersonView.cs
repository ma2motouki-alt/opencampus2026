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

        [Header("Plant Look")]
        [SerializeField] float plantLookChancePerSecond = 0.38f;
        [SerializeField] Vector2 plantLookDurationSeconds = new Vector2(1f, 12f);
        [SerializeField] float plantLookCooldownSeconds = 2.0f;

        [Header("Leaf Hang")]
        [SerializeField] Vector2 leafHangDurationSeconds = new Vector2(10f, 20f);
        [SerializeField] float leafHangCooldownSeconds = 2.5f;
        [SerializeField] float leafHangFrameSeconds = 0.18f;
        [SerializeField] float leafHangSpriteDownOffsetRatio = 0.72f;
        [SerializeField] float leafDropDurationSeconds = 0.45f;
        [SerializeField] float leafDropArcHeightWorld = 0.35f;
        [SerializeField] float leafDropRetouchArmSeconds = 0.2f;

        SpriteRenderer glowRenderer;
        SpriteRenderer spriteRenderer;
        float animationSeed;
        float plantLookTimer;
        float plantLookCooldownTimer;
        bool plantLookLeft;
        int plantLookTargetId = -1;
        float leafHangTimer;
        float leafHangCooldownTimer;
        bool leafHangLeft;
        Vector3 leafHangWorldPosition;
        int hangingPlantId = -1;
        int hangingLeafIndex = -1;
        float leafDropTimer;
        Vector3 leafDropStartWorldPosition;
        Vector3 leafDropEndWorldPosition;
        float leafDropRetouchTimer;
        bool leafDropTouchWasPresent;

        static readonly Dictionary<int, SpriteSet> spriteCache = new();

        public bool IsLookingAtPlant => plantLookTimer > 0f;
        public bool IsHangingFromLeaf => leafHangTimer > 0f;
        public bool IsDroppingFromLeaf => leafDropTimer > 0f;
        public bool IsPlantInteractionLocked => IsLookingAtPlant || IsHangingFromLeaf || IsDroppingFromLeaf;
        public int PlantLookTargetId => IsLookingAtPlant ? plantLookTargetId : -1;
        public int HangingPlantId => IsHangingFromLeaf ? hangingPlantId : -1;
        public int HangingLeafIndex => IsHangingFromLeaf ? hangingLeafIndex : -1;
        public Vector3 TouchWorldPosition => spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.bounds.center
            : transform.position;
        public Vector3 LeafInteractionWorldPosition => IsDroppingFromLeaf
            ? LeafDropWorldPosition()
            : IsHangingFromLeaf ? leafHangWorldPosition : transform.position;

        public void Initialize()
        {
            animationSeed = Random.Range(0f, 100f);
            glowRenderer = CreateRenderer("Glow", RuntimeSpriteFactory.Circle, -4);
            spriteRenderer = CreateRenderer("Sprite", RuntimeSpriteFactory.Circle, 11);
        }

        public void Render(
            LittlePerson person,
            LittlePersonArchetypeMaster archetype,
            NormalizedScreenMapper mapper,
            bool plantLookCandidate = false,
            int plantLookCandidateId = -1,
            Vector3 plantLookTargetWorld = default,
            bool leafHangCandidate = false,
            int leafHangPlantId = -1,
            int leafHangLeafIndex = -1,
            Vector3 leafHangTargetWorld = default,
            bool leafHangLeftCandidate = false,
            bool leafDropTouched = false,
            bool hangingLeafTargetAvailable = true,
            Vector3 currentLeafHangWorldPosition = default)
        {
            var baseWorldPosition = mapper.ToWorld(person.Position) + EdgeVisualOffset(person, mapper);
            var unit = mapper.ToWorldRadius(archetype.Size);
            var targetHeight = Mathf.Max(0.001f, unit * spriteHeightMultiplier);
            AdvancePlantInteraction(
                person,
                baseWorldPosition,
                plantLookCandidate,
                plantLookCandidateId,
                plantLookTargetWorld,
                leafHangCandidate,
                leafHangPlantId,
                leafHangLeafIndex,
                leafHangTargetWorld,
                leafHangLeftCandidate,
                leafDropTouched,
                hangingLeafTargetAvailable,
                currentLeafHangWorldPosition);

            var isDroppingFromLeaf = leafDropTimer > 0f;
            var isHangingFromLeaf = leafHangTimer > 0f;
            var isLookingAtPlant = plantLookTimer > 0f && !isHangingFromLeaf && !isDroppingFromLeaf;
            transform.position = isDroppingFromLeaf
                ? LeafDropWorldPosition()
                : isHangingFromLeaf ? leafHangWorldPosition : baseWorldPosition;

            var sprites = GetSpriteSet(archetype.Id);
            var sprite = isDroppingFromLeaf || isHangingFromLeaf
                ? sprites.HangFrame(CurrentLeafHangFrameIndex(), leafHangLeft)
                : isLookingAtPlant
                    ? (plantLookLeft ? sprites.LookLeft : sprites.LookRight)
                    : sprites.WalkFrame(CurrentFrameIndex(person), ShouldAnimate(person));
            var hasSprite = sprite != null;

            spriteRenderer.sprite = hasSprite ? sprite : RuntimeSpriteFactory.Circle;
            spriteRenderer.color = hasSprite ? Color.white : archetype.BodyColor;
            spriteRenderer.flipX = !isLookingAtPlant && !isHangingFromLeaf && !isDroppingFromLeaf && flipAlongMovement && ShouldFlipX(person);

            var pulse = person.Emotion == LittlePersonEmotion.Startled
                ? 1f + startledPulseScale * (0.5f + 0.5f * Mathf.Sin((Time.time + animationSeed) * 14f))
                : 1f;
            var spriteScale = hasSprite ? ScaleForTargetHeight(spriteRenderer.sprite, targetHeight) : unit;
            spriteRenderer.transform.localPosition = isHangingFromLeaf || isDroppingFromLeaf
                ? new Vector3(0f, -targetHeight * leafHangSpriteDownOffsetRatio, 0f)
                : Vector3.zero;
            spriteRenderer.transform.localRotation = isDroppingFromLeaf
                ? Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 10f + animationSeed) * fallingSpinDegrees)
                : Quaternion.identity;
            spriteRenderer.transform.localScale = Vector3.one * spriteScale * pulse;

            var glowColor = GlowColor(person, archetype.BodyColor);
            glowColor.a = GlowAlpha(person);
            glowRenderer.color = glowColor;
            glowRenderer.transform.localPosition = hasSprite
                ? new Vector3(0f, targetHeight * 0.42f, 0f)
                : Vector3.zero;
            glowRenderer.transform.localRotation = Quaternion.identity;
            glowRenderer.transform.localScale = Vector3.one * unit * GlowScale(person);

            transform.rotation = isHangingFromLeaf || isDroppingFromLeaf
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, RotationDegrees(person));
        }

        void AdvancePlantInteraction(
            LittlePerson person,
            Vector3 baseWorldPosition,
            bool plantLookCandidate,
            int plantLookCandidateId,
            Vector3 plantLookTargetWorld,
            bool leafHangCandidate,
            int leafHangPlantId,
            int leafHangLeafIndex,
            Vector3 leafHangTargetWorld,
            bool leafHangLeftCandidate,
            bool leafDropTouched,
            bool hangingLeafTargetAvailable,
            Vector3 currentLeafHangWorldPosition)
        {
            var deltaTime = Mathf.Max(0f, Time.deltaTime);
            leafHangCooldownTimer = Mathf.Max(0f, leafHangCooldownTimer - deltaTime);

            if (leafDropTimer > 0f)
            {
                leafDropTimer = Mathf.Max(0f, leafDropTimer - deltaTime);
                if (leafDropTimer <= 0f)
                {
                    leafHangCooldownTimer = leafHangCooldownSeconds;
                    hangingPlantId = -1;
                    hangingLeafIndex = -1;
                }

                return;
            }

            if (leafHangTimer > 0f)
            {
                plantLookTimer = 0f;
                plantLookTargetId = -1;
                if (!hangingLeafTargetAvailable)
                {
                    StartLeafDrop(baseWorldPosition);
                    return;
                }
                leafHangWorldPosition = currentLeafHangWorldPosition;

                leafDropRetouchTimer = Mathf.Max(0f, leafDropRetouchTimer - deltaTime);
                if (!leafDropTouched)
                {
                    leafDropTouchWasPresent = false;
                }

                if (leafDropTouched && !leafDropTouchWasPresent && leafDropRetouchTimer <= 0f)
                {
                    leafDropTouchWasPresent = true;
                    StartLeafDrop(baseWorldPosition);
                    return;
                }

                leafDropTouchWasPresent = leafDropTouched;

                leafHangTimer = Mathf.Max(0f, leafHangTimer - deltaTime);
                if (leafHangTimer <= 0f)
                {
                    leafHangCooldownTimer = leafHangCooldownSeconds;
                    hangingPlantId = -1;
                    hangingLeafIndex = -1;
                }

                return;
            }

            plantLookTimer = Mathf.Max(0f, plantLookTimer - deltaTime);
            plantLookCooldownTimer = Mathf.Max(0f, plantLookCooldownTimer - deltaTime);
            if (plantLookTimer <= 0f)
            {
                plantLookTargetId = -1;
            }

            if (plantLookTimer > 0f)
            {
                if (leafHangCandidate && leafHangCooldownTimer <= 0f)
                {
                    StartLeafHang(leafHangPlantId, leafHangLeafIndex, leafHangTargetWorld, leafHangLeftCandidate);
                }

                return;
            }

            if (plantLookCooldownTimer > 0f ||
                !plantLookCandidate ||
                person.CurrentBehavior != LittlePersonBehaviorKind.EdgeWalk)
            {
                return;
            }

            var chance = Mathf.Max(0f, plantLookChancePerSecond) * deltaTime;
            if (Random.value > chance)
            {
                return;
            }

            plantLookTargetId = plantLookCandidateId;
            plantLookLeft = IsLookingFromWorldLeft(baseWorldPosition, plantLookTargetWorld);
            var minDuration = Mathf.Max(0.05f, Mathf.Min(plantLookDurationSeconds.x, plantLookDurationSeconds.y));
            var maxDuration = Mathf.Max(minDuration, Mathf.Max(plantLookDurationSeconds.x, plantLookDurationSeconds.y));
            plantLookTimer = Random.Range(minDuration, maxDuration);
            plantLookCooldownTimer = plantLookCooldownSeconds;
        }

        void StartLeafHang(int plantId, int leafIndex, Vector3 targetWorldPosition, bool useLeftSprite)
        {
            hangingPlantId = plantId;
            hangingLeafIndex = leafIndex;
            leafHangWorldPosition = targetWorldPosition;
            leafHangLeft = useLeftSprite;
            var minDuration = Mathf.Max(0.05f, Mathf.Min(leafHangDurationSeconds.x, leafHangDurationSeconds.y));
            var maxDuration = Mathf.Max(minDuration, Mathf.Max(leafHangDurationSeconds.x, leafHangDurationSeconds.y));
            leafHangTimer = Random.Range(minDuration, maxDuration);
            leafDropTimer = 0f;
            plantLookTimer = 0f;
            plantLookTargetId = -1;
            leafDropRetouchTimer = Mathf.Max(0f, leafDropRetouchArmSeconds);
            leafDropTouchWasPresent = true;
        }

        void StartLeafDrop(Vector3 endWorldPosition)
        {
            leafDropStartWorldPosition = leafHangWorldPosition;
            leafDropEndWorldPosition = endWorldPosition;
            leafDropTimer = Mathf.Max(0.05f, leafDropDurationSeconds);
            hangingPlantId = -1;
            hangingLeafIndex = -1;
            leafHangTimer = 0f;
            plantLookTimer = 0f;
            leafDropTouchWasPresent = false;
        }

        Vector3 LeafDropWorldPosition()
        {
            var duration = Mathf.Max(0.05f, leafDropDurationSeconds);
            var progress = 1f - Mathf.Clamp01(leafDropTimer / duration);
            var eased = progress * progress * (3f - 2f * progress);
            var arc = Mathf.Sin(progress * Mathf.PI) * leafDropArcHeightWorld;
            return Vector3.Lerp(leafDropStartWorldPosition, leafDropEndWorldPosition, eased) + Vector3.up * arc;
        }

        static bool IsLookingFromWorldLeft(Vector3 baseWorldPosition, Vector3 targetWorldPosition)
        {
            return baseWorldPosition.x < targetWorldPosition.x;
        }

        int CurrentFrameIndex(LittlePerson person)
        {
            var offset = (person.PreferenceSeed & 0xffff) * 0.001f + animationSeed;
            var safeFrameSeconds = Mathf.Max(0.03f, frameSeconds);
            return Mathf.FloorToInt((Time.time + offset) / safeFrameSeconds) % 2;
        }

        int CurrentLeafHangFrameIndex()
        {
            var safeFrameSeconds = Mathf.Max(0.03f, leafHangFrameSeconds);
            return Mathf.FloorToInt((Time.time + animationSeed) / safeFrameSeconds) % 2;
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

        static SpriteSet GetSpriteSet(int archetypeId)
        {
            if (spriteCache.TryGetValue(archetypeId, out var sprites))
            {
                return sprites;
            }

            var prefix = SpritePrefix(archetypeId);
            sprites = new SpriteSet(
                LoadSprite($"{prefix}1"),
                LoadSprite($"{prefix}2"),
                LoadSprite($"{prefix}_up_left"),
                LoadSprite($"{prefix}_up_right"),
                LoadSprite($"{prefix}_hang1_left"),
                LoadSprite($"{prefix}_hang2_left"),
                LoadSprite($"{prefix}_hang1_right"),
                LoadSprite($"{prefix}_hang2_right"),
                LoadSprite($"{prefix}_hang1"),
                LoadSprite($"{prefix}_hang2"));
            spriteCache.Add(archetypeId, sprites);
            return sprites;
        }

        static Sprite LoadSprite(string resourceName)
        {
            var texture = Resources.Load<Texture2D>(ResourceRoot + resourceName);
#if UNITY_EDITOR
            if (texture == null)
            {
                texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/People/{resourceName}.png");
            }
#endif

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

        sealed class SpriteSet
        {
            readonly Sprite walkA;
            readonly Sprite walkB;
            readonly Sprite hangLeftA;
            readonly Sprite hangLeftB;
            readonly Sprite hangRightA;
            readonly Sprite hangRightB;
            public readonly Sprite LookLeft;
            public readonly Sprite LookRight;

            public SpriteSet(
                Sprite walkA,
                Sprite walkB,
                Sprite lookLeft,
                Sprite lookRight,
                Sprite hangLeftA,
                Sprite hangLeftB,
                Sprite hangRightA,
                Sprite hangRightB,
                Sprite hangAnyA,
                Sprite hangAnyB)
            {
                this.walkA = walkA;
                this.walkB = walkB;
                LookLeft = lookLeft != null ? lookLeft : walkA;
                LookRight = lookRight != null ? lookRight : walkA;
                this.hangLeftA = hangLeftA != null ? hangLeftA : hangAnyA != null ? hangAnyA : LookLeft;
                this.hangLeftB = hangLeftB != null ? hangLeftB : hangAnyB != null ? hangAnyB : this.hangLeftA;
                this.hangRightA = hangRightA != null ? hangRightA : hangAnyA != null ? hangAnyA : LookRight;
                this.hangRightB = hangRightB != null ? hangRightB : hangAnyB != null ? hangAnyB : this.hangRightA;
            }

            public Sprite WalkFrame(int frameIndex, bool animate)
            {
                if (!animate)
                {
                    return walkA != null ? walkA : walkB;
                }

                return frameIndex % 2 == 0
                    ? walkA != null ? walkA : walkB
                    : walkB != null ? walkB : walkA;
            }

            public Sprite HangFrame(int frameIndex, bool left)
            {
                var frameA = left ? hangLeftA : hangRightA;
                var frameB = left ? hangLeftB : hangRightB;
                return frameIndex % 2 == 0
                    ? frameA != null ? frameA : frameB
                    : frameB != null ? frameB : frameA;
            }
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
