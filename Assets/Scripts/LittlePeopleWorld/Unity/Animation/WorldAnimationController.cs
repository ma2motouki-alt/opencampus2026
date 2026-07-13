using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using LittlePeopleWorld.Unity.Animation.Fairy;
using LittlePeopleWorld.Unity.Animation.Plants;
using LittlePeopleWorld.Unity.Animation.Rain;
using UnityEngine;

namespace LittlePeopleWorld.Unity.Animation
{
    internal sealed class WorldAnimationController
    {
        readonly RecognitionMask mask = new();
        readonly Transform owner;
        readonly SpriteRenderer maskRenderer;
        readonly Transform fairyRoot;
        readonly Transform plantRoot;
        readonly FairySimulation fairies;
        readonly PlantSystem plants;
        readonly RainOcclusionSystem rainOcclusion = new();
        readonly RainPlantInteraction rainPlants;
        Texture2D maskTexture;
        Color32[] maskPixels;
        NormalizedScreenMapper mapper;
        bool showMask;
        bool transparentBackground;
        Color32 onColor;
        Color32 offColor;

        public WorldAnimationController(Transform owner)
        {
            this.owner = owner;
            maskRenderer = CreateRoot("Recognition Binary Mask").gameObject.AddComponent<SpriteRenderer>();
            maskRenderer.sortingOrder = -10;
            fairyRoot = CreateRoot("Mask Particles");
            plantRoot = CreateRoot("Mask Plants");
            plants = new PlantSystem(plantRoot);
            fairies = new FairySimulation(new FairyRenderer(fairyRoot));
            rainPlants = new RainPlantInteraction(rainOcclusion);
        }

        public bool HasActivePlants => plants.HasActivePlants;
        public bool HasGrowingPlants => plants.HasGrowingPlants;
        public int ActiveBloomCount => plants.ActiveBloomCount;
        public int PlantSpawnSequence => plants.SpawnSequence;
        public int PlantBloomSequence => plants.BloomSequence;
        public int FlowerBurstSequence => fairies.FlowerBurstSequence;
        public string RainOcclusionDebugText => rainOcclusion.DebugText;

        public void ConfigureMask(int width, int height, int minimumBlobPixels, bool includeHands, bool visible, bool transparent, Color32 maskOn, Color32 maskOff)
        {
            mask.Configure(width, height, minimumBlobPixels, includeHands);
            showMask = visible;
            transparentBackground = transparent;
            onColor = maskOn;
            offColor = maskOff;
            EnsureMaskTexture();
        }

        public void Render(
            World world,
            MasterDatabase masters,
            NormalizedScreenMapper screenMapper,
            float deltaTime,
            bool debugEnabled,
            int blurRadius,
            FairySettings fairySettings,
            PlantSettings plantSettings,
            bool enableRainPlants,
            float rainFallSpeed,
            float groundY,
            bool enableRainOcclusion,
            int rainProbeRadius,
            int rainTopPadding,
            bool showRainDebug,
            bool enableVisualOcclusion,
            float visualSmoothing,
            int minimumVisualHeight)
        {
            if (world == null || masters == null || screenMapper == null)
            {
                SetVisible(false);
                return;
            }

            mapper = screenMapper;
            mask.Rebuild(world.InteractionObjects, blurRadius);
            RenderMask();
            plants.Configure(plantSettings, mask, mapper);
            fairies.Configure(fairySettings, plantSettings, mask, plants, mapper);
            rainOcclusion.Configure(mask, enableRainOcclusion, enableVisualOcclusion, rainProbeRadius, rainTopPadding, visualSmoothing, minimumVisualHeight, groundY);
            rainPlants.Configure(mask, plants, rainFallSpeed, groundY);

            var dt = Mathf.Clamp(deltaTime, 0f, 0.05f);
            fairies.Advance(world.InteractionObjects, dt);
            fairies.TriggerCloudRain(world, masters, dt);
            rainOcclusion.BeginFrame();
            rainPlants.Advance(world, masters, dt, enableRainPlants);
            rainOcclusion.FinishFrame(showRainDebug);
            plants.Advance(dt);
            SetVisible(true);
        }

        public void PrepareSpatialQueries(NormalizedScreenMapper screenMapper)
        {
            if (screenMapper == null) return;
            mapper = screenMapper;
        }

        public bool TryGetNearestPlantLookTarget(Vector3 worldPosition, float radiusWorld, out int plantId, out Vector3 plantWorldPosition)
            => plants.TryGetNearestLookTarget(worldPosition, radiusWorld, out plantId, out plantWorldPosition);

        public bool TryGetHighestAvailableLeafHangTarget(int plantId, Vector3 personPosition, ISet<LeafHangSlot> occupied, out LeafHangSlot slot, out Vector3 hangPosition, out bool hangLeft)
            => plants.TryGetHighestAvailableLeaf(plantId, personPosition, occupied, out slot, out hangPosition, out hangLeft);

        public bool TryGetLeafHangTarget(int plantId, int leafIndex, out Vector3 worldPosition)
            => plants.TryGetLeafTarget(plantId, leafIndex, out worldPosition);

        public bool IsInputNearWorldPosition(IReadOnlyList<InteractionObject> objects, Vector3 worldPosition, float radiusWorld)
        {
            if (mapper == null || objects == null || radiusWorld <= 0f) return false;
            var pointPx = mask.WorldToPixel(worldPosition, mapper);
            var radiusPx = mask.WorldRadiusToPixel(radiusWorld, mapper);
            foreach (var interactionObject in objects)
            {
                if (interactionObject == null) continue;
                if (interactionObject.ShapeKind == InteractionShapeKind.Contour && interactionObject.ContourPoints.Count >= 3)
                {
                    var polygon = new Vector2[interactionObject.ContourPoints.Count];
                    for (var i = 0; i < polygon.Length; i++) polygon[i] = mask.NormalizedToPixel(interactionObject.ContourPoints[i]);
                    if (RecognitionMask.DistancePointToPolygon(polygon, pointPx) <= radiusPx) return true;
                    continue;
                }
                var objectWorld = mapper.ToWorld(interactionObject.Position);
                var objectRadius = mapper.ToWorldRadius(Mathf.Max(interactionObject.Size.x, interactionObject.Size.y) * 0.5f);
                var totalRadius = radiusWorld + objectRadius;
                if ((objectWorld - worldPosition).sqrMagnitude <= totalRadius * totalRadius) return true;
            }
            return false;
        }

        public float GetRainVisibleHeightRatio(VisualEffectInstance effect) => rainOcclusion.GetVisibleHeightRatio(effect);

        public void SetVisible(bool visible)
        {
            maskRenderer.enabled = visible && showMask;
            fairyRoot.gameObject.SetActive(visible);
            plantRoot.gameObject.SetActive(visible);
        }

        void EnsureMaskTexture()
        {
            if (maskTexture != null && maskTexture.width == mask.Width && maskTexture.height == mask.Height) return;
            if (maskTexture != null) Object.Destroy(maskTexture);
            maskTexture = new Texture2D(mask.Width, mask.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RecognitionMask"
            };
            maskPixels = new Color32[mask.Width * mask.Height];
            maskRenderer.sprite = Sprite.Create(maskTexture, new Rect(0, 0, mask.Width, mask.Height), new Vector2(0.5f, 0.5f), mask.Height);
        }

        void RenderMask()
        {
            EnsureMaskTexture();
            var off = transparentBackground ? new Color32(0, 0, 0, 0) : offColor;
            var effective = mask.EffectivePixels;
            for (var i = 0; i < maskPixels.Length; i++) maskPixels[i] = effective[i] ? onColor : off;
            maskTexture.SetPixels32(maskPixels);
            maskTexture.Apply(false);
            maskRenderer.transform.position = Vector3.zero;
            var bounds = maskRenderer.sprite.bounds.size;
            maskRenderer.transform.localScale = new Vector3(mapper.WorldWidth / Mathf.Max(0.001f, bounds.x), mapper.WorldHeight / Mathf.Max(0.001f, bounds.y), 1f);
        }

        Transform CreateRoot(string name)
        {
            var result = new GameObject(name).transform;
            result.SetParent(owner, false);
            return result;
        }
    }
}
