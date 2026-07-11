using System;
using System.Collections.Generic;
using LittlePeopleWorld.Application;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Input;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public enum InteractionInputProviderMode
    {
        Mouse = 1,
        UdpRealSense = 2
    }

    public sealed class LittlePeopleWorldController : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] InteractionInputProviderMode inputProviderMode = InteractionInputProviderMode.Mouse;
        [SerializeField] MouseInputProviderBehaviour mouseInputProvider;
        [SerializeField] UdpRealSenseInputProviderBehaviour udpRealSenseInputProvider;
        [SerializeField] int worldPresetId = 1;
        [SerializeField] float worldHeight = 10f;
        [SerializeField] bool showHelpOverlay = true;
        [SerializeField] bool enableRecognitionMaskAnimation = true;
        [SerializeField] WorldSpaceMaskAnimationController maskAnimationController;
        [SerializeField] bool enableAudioLayers = true;
        [SerializeField] WorldAudioController audioController;
        [Header("Little Person Plant Look")]
        [SerializeField] bool enableLittlePersonPlantLook = true;
        [SerializeField] float littlePersonPlantLookRadius = 0.95f;
        [Header("Little Person Leaf Hang")]
        [SerializeField] bool enableLittlePersonLeafHang = true;
        [SerializeField] float littlePersonLeafHangTouchRadius = 0.45f;
        [SerializeField] float littlePersonLeafDropTouchRadius = 0.45f;
        [SerializeField] bool enableDevelopmentLeafHangClick = true;
        [Header("Development Rain")]
        [SerializeField] bool enableDevelopmentClickRain = true;
        [SerializeField] float developmentRainDurationSeconds = 2.0f;
        [SerializeField] float developmentRainWidth = 0.08f;

        readonly List<LittlePersonView> littlePersonViews = new();
        readonly Dictionary<int, InteractionObjectView> interactionObjectViews = new();
        readonly Dictionary<int, WalkableSurfaceView> walkableSurfaceViews = new();
        readonly Dictionary<int, PropObstacleView> propObstacleViews = new();
        readonly Dictionary<int, AmbientObjectView> ambientObjectViews = new();
        readonly Dictionary<int, VisualEffectView> visualEffectViews = new();
        readonly Dictionary<int, RainbowView> rainbowViews = new();
        readonly HashSet<Guid> plantLookPausedPersonIds = new();
        readonly HashSet<LeafHangSlot> occupiedLeafHangSlots = new();

        MasterDatabase masters;
        World world;
        LittlePeopleWorldOrchestrator orchestrator;
        NormalizedScreenMapper mapper;
        Transform littlePeopleRoot;
        Transform objectsRoot;
        Transform surfacesRoot;
        Transform obstaclesRoot;
        Transform ambientRoot;
        Transform effectsRoot;
        Transform rainbowRoot;
        IInteractionInputProvider activeInputProvider;

        public MasterDatabase Masters => masters;
        public World World => world;
        public IInteractionInputProvider InputProvider => activeInputProvider ?? mouseInputProvider;

        void Awake()
        {
            EnsureRuntime();
        }

        void Start()
        {
            BuildWorld();
        }

        void Update()
        {
            EnsureRuntime();
            if (world == null)
            {
                BuildWorld();
            }

            world.SetDisplayAspect(mapper.WorldWidth / mapper.WorldHeight);
            world.SetActiveBloomCount(
                enableRecognitionMaskAnimation && maskAnimationController != null
                    ? maskAnimationController.ActiveBloomCount : 0);
            world.SetMovementPausedLittlePeople(plantLookPausedPersonIds);
            var inputProvider = InputProvider;
            orchestrator.AdvanceFrame(Time.deltaTime, inputProvider?.InteractionObjects ?? Array.Empty<InteractionObject>());
            world = orchestrator.World;
            HandleDevelopmentClickRain();

            SyncLittlePeopleViews();
            SyncInteractionObjectViews();
            SyncWalkableSurfaceViews();
            SyncRainbowViews();
            SyncPropObstacleViews();
            SyncAmbientObjectViews();
            SyncRecognitionMaskAnimation();
            SyncVisualEffectViews();
            SyncAudioLayers();
        }

        void HandleDevelopmentClickRain()
        {
            if (!enableDevelopmentClickRain || world == null || masters == null || mapper == null || targetCamera == null)
            {
                return;
            }

            if (!UnityEngine.Input.GetMouseButtonDown(1) || IsDevelopmentLeafHangClick())
            {
                return;
            }

            var mousePosition = UnityEngine.Input.mousePosition;
            if (mousePosition.x < 0f || mousePosition.x > Screen.width ||
                mousePosition.y < 0f || mousePosition.y > Screen.height)
            {
                return;
            }

            var worldPosition = targetCamera.ScreenToWorldPoint(new Vector3(
                mousePosition.x,
                mousePosition.y,
                Mathf.Max(0.001f, -targetCamera.transform.position.z)));
            var normalizedPosition = mapper.ToNormalized(worldPosition);
            world.TriggerDevelopmentRain(masters, normalizedPosition, developmentRainWidth, developmentRainDurationSeconds);
        }

        void OnGUI()
        {
            var inputProvider = InputProvider;
            if (inputProvider == null || !inputProvider.DebugEnabled || !showHelpOverlay)
            {
                return;
            }

            var text =
                "Little People World MVP\n" +
                $"Input: {InputProviderLabel()}\n" +
                "1 Hand  2 Round  3 Bar\n" +
                "Click/Drag place and move  Wheel resize  R rotate  Delete remove  D debug\n" +
                "Right click: development rain  Shift+Right click: leaf hang/drop debug\n" +
                $"Objects: {world?.InteractionObjects.Count ?? 0}  Surfaces: {world?.WalkableSurfaces.Count ?? 0}  Obstacles: {world?.PropObstacles.Count ?? 0}  Ambient: {world?.AmbientObjects.Count ?? 0}  Effects: {world?.VisualEffects.Count ?? 0}  People: {world?.LittlePeople.Count ?? 0}";

            var rainOcclusionDebugText = maskAnimationController != null
                ? maskAnimationController.RainOcclusionDebugText
                : string.Empty;
            if (!string.IsNullOrEmpty(rainOcclusionDebugText))
            {
                text += "\n" + rainOcclusionDebugText;
            }

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                normal = { textColor = new Color(0.85f, 1f, 0.95f, 1f) }
            };

            var lineCount = 1;
            foreach (var character in text)
            {
                if (character == '\n')
                {
                    lineCount++;
                }
            }

            GUI.Box(new Rect(16f, 16f, 560f, 28f + lineCount * 17f), text, style);
        }

        void EnsureRuntime()
        {
            if (masters == null)
            {
                masters = MasterDatabase.CreateDefault();
            }

            if (orchestrator == null)
            {
                orchestrator = new LittlePeopleWorldOrchestrator(masters);
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                targetCamera = cameraObject.AddComponent<Camera>();
                targetCamera.tag = "MainCamera";
            }

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = worldHeight * 0.5f;
            targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            targetCamera.transform.rotation = Quaternion.identity;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = masters.WorldPresets.Get(worldPresetId).BackgroundColor;
            mapper = new NormalizedScreenMapper(targetCamera, worldHeight);
            EnsureAudioListener();

            EnsureInputProviders();
        }

        void EnsureAudioListener()
        {
            if (targetCamera == null || FindFirstObjectByType<AudioListener>() != null)
            {
                return;
            }

            targetCamera.gameObject.AddComponent<AudioListener>();
        }

        void EnsureInputProviders()
        {
            if (mouseInputProvider == null)
            {
                mouseInputProvider = GetComponent<MouseInputProviderBehaviour>();
            }

            if (mouseInputProvider == null)
            {
                mouseInputProvider = gameObject.AddComponent<MouseInputProviderBehaviour>();
            }

            mouseInputProvider.Initialize(masters, targetCamera);

            if (inputProviderMode == InteractionInputProviderMode.UdpRealSense)
            {
                if (udpRealSenseInputProvider == null)
                {
                    udpRealSenseInputProvider = GetComponent<UdpRealSenseInputProviderBehaviour>();
                }

                if (udpRealSenseInputProvider == null)
                {
                    udpRealSenseInputProvider = gameObject.AddComponent<UdpRealSenseInputProviderBehaviour>();
                }
            }

            activeInputProvider = inputProviderMode == InteractionInputProviderMode.UdpRealSense && udpRealSenseInputProvider != null
                ? udpRealSenseInputProvider
                : mouseInputProvider;

            if (mouseInputProvider != null)
            {
                mouseInputProvider.enabled = ReferenceEquals(activeInputProvider, mouseInputProvider);
            }

            if (udpRealSenseInputProvider != null)
            {
                udpRealSenseInputProvider.enabled = ReferenceEquals(activeInputProvider, udpRealSenseInputProvider);
            }
        }

        void EnsureRecognitionMaskAnimation()
        {
            if (maskAnimationController == null)
            {
                maskAnimationController = GetComponent<WorldSpaceMaskAnimationController>();
            }

            if (maskAnimationController == null)
            {
                maskAnimationController = gameObject.AddComponent<WorldSpaceMaskAnimationController>();
            }
        }

        void EnsureAudioController()
        {
            if (audioController == null)
            {
                audioController = GetComponent<WorldAudioController>();
            }

            if (audioController == null)
            {
                audioController = gameObject.AddComponent<WorldAudioController>();
            }
        }

        void BuildWorld()
        {
            world = orchestrator.CreateWorld(worldPresetId);
            plantLookPausedPersonIds.Clear();
            littlePeopleRoot = CreateRoot("Little People");
            objectsRoot = CreateRoot("Interaction Objects");
            surfacesRoot = CreateRoot("Walkable Surfaces");
            obstaclesRoot = CreateRoot("Prop Obstacles");
            ambientRoot = CreateRoot("Ambient Objects");
            effectsRoot = CreateRoot("Visual Effects");
            rainbowRoot = CreateRoot("Rainbows");
            EnsureRecognitionMaskAnimation();
            EnsureAudioController();
            BuildLittlePersonViews();
        }

        Transform CreateRoot(string rootName)
        {
            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        void BuildLittlePersonViews()
        {
            foreach (var view in littlePersonViews)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            littlePersonViews.Clear();

            foreach (var person in world.LittlePeople)
            {
                var viewObject = new GameObject("Little Person");
                viewObject.transform.SetParent(littlePeopleRoot, false);
                var view = viewObject.AddComponent<LittlePersonView>();
                view.Initialize();
                littlePersonViews.Add(view);
            }
        }

        void SyncLittlePeopleViews()
        {
            plantLookPausedPersonIds.Clear();
            occupiedLeafHangSlots.Clear();
            foreach (var existingView in littlePersonViews)
            {
                if (existingView != null && existingView.IsHangingFromLeaf)
                {
                    occupiedLeafHangSlots.Add(new LeafHangSlot(
                        existingView.HangingPlantId,
                        existingView.HangingLeafIndex));
                }
            }

            if (maskAnimationController != null)
            {
                maskAnimationController.PrepareSpatialQueries(mapper);
            }

            var hasDevelopmentPersonTouch = TryGetDevelopmentPersonTouchWorldPosition(out var developmentPersonTouchWorldPosition);
            for (var i = 0; i < world.LittlePeople.Count && i < littlePersonViews.Count; i++)
            {
                var person = world.LittlePeople[i];
                var archetype = masters.LittlePersonArchetypes.Get(person.ArchetypeId);
                var personWorldPosition = mapper.ToWorld(person.Position);
                var view = littlePersonViews[i];
                var plantLookTargetId = -1;
                var plantLookTargetWorld = Vector3.zero;
                var hasPlantLookTarget =
                    enableLittlePersonPlantLook &&
                    maskAnimationController != null &&
                    maskAnimationController.TryGetNearestPlantLookTarget(
                        personWorldPosition,
                        littlePersonPlantLookRadius,
                        out plantLookTargetId,
                        out plantLookTargetWorld);
                var personTouched =
                    enableLittlePersonLeafHang &&
                    view.IsLookingAtPlant &&
                    IsPlantInteractionTouched(
                        view.TouchWorldPosition,
                        littlePersonLeafHangTouchRadius,
                        hasDevelopmentPersonTouch,
                        developmentPersonTouchWorldPosition);
                var leafHangSlot = default(LeafHangSlot);
                var leafHangTargetWorld = Vector3.zero;
                var leafHangLeft = false;
                var hasLeafHangTarget =
                    personTouched &&
                    maskAnimationController != null &&
                    maskAnimationController.TryGetHighestAvailableLeafHangTarget(
                        view.PlantLookTargetId,
                        view.TouchWorldPosition,
                        occupiedLeafHangSlots,
                        out leafHangSlot,
                        out leafHangTargetWorld,
                        out leafHangLeft);
                var leafDropTouched =
                    enableLittlePersonLeafHang &&
                    view.IsHangingFromLeaf &&
                    IsPlantInteractionTouched(
                        view.TouchWorldPosition,
                        littlePersonLeafDropTouchRadius,
                        hasDevelopmentPersonTouch,
                        developmentPersonTouchWorldPosition);
                var currentLeafHangWorld = Vector3.zero;
                var hangingLeafTargetAvailable =
                    !view.IsHangingFromLeaf ||
                    (maskAnimationController != null &&
                     maskAnimationController.TryGetLeafHangTarget(
                         view.HangingPlantId,
                         view.HangingLeafIndex,
                         out currentLeafHangWorld));
                view.Render(
                    person,
                    archetype,
                    mapper,
                    hasPlantLookTarget,
                    plantLookTargetId,
                    plantLookTargetWorld,
                    hasLeafHangTarget,
                    leafHangSlot.PlantId,
                    leafHangSlot.LeafIndex,
                    leafHangTargetWorld,
                    leafHangLeft,
                    leafDropTouched,
                    hangingLeafTargetAvailable,
                    currentLeafHangWorld);
                if (view.IsPlantInteractionLocked)
                {
                    plantLookPausedPersonIds.Add(person.Id);
                }

                if (view.IsHangingFromLeaf)
                {
                    occupiedLeafHangSlots.Add(new LeafHangSlot(view.HangingPlantId, view.HangingLeafIndex));
                }
            }
        }

        void SyncInteractionObjectViews()
        {
            var liveIds = new HashSet<int>();

            foreach (var interactionObject in world.InteractionObjects)
            {
                liveIds.Add(interactionObject.Id);
                if (!interactionObjectViews.TryGetValue(interactionObject.Id, out var view))
                {
                    var viewObject = new GameObject($"Interaction Object {interactionObject.Id}");
                    viewObject.transform.SetParent(objectsRoot, false);
                    view = viewObject.AddComponent<InteractionObjectView>();
                    view.Initialize();
                    interactionObjectViews.Add(interactionObject.Id, view);
                }

                var field = FindField(interactionObject.Id);
                var typeMaster = masters.GetObjectType(interactionObject.Kind);
                var debugEnabled = IsDebugEnabled();
                view.Render(
                    interactionObject,
                    field,
                    typeMaster,
                    mapper,
                    debugEnabled,
                    interactionObject.Id == SelectedObjectId());
            }

            RemoveDeadInteractionViews(liveIds);
        }

        void SyncWalkableSurfaceViews()
        {
            var liveIds = new HashSet<int>();

            foreach (var surface in world.WalkableSurfaces)
            {
                liveIds.Add(surface.Id);
                if (!walkableSurfaceViews.TryGetValue(surface.Id, out var view))
                {
                    var viewObject = new GameObject($"Walkable Surface {surface.Id}");
                    viewObject.transform.SetParent(surfacesRoot, false);
                    view = viewObject.AddComponent<WalkableSurfaceView>();
                    view.Initialize();
                    walkableSurfaceViews.Add(surface.Id, view);
                }

                view.Render(surface, mapper, IsDebugEnabled());
            }

            RemoveDeadWalkableSurfaceViews(liveIds);
        }

        void SyncPropObstacleViews()
        {
            var liveIds = new HashSet<int>();

            foreach (var obstacle in world.PropObstacles)
            {
                liveIds.Add(obstacle.Id);
                if (!propObstacleViews.TryGetValue(obstacle.Id, out var view))
                {
                    var viewObject = new GameObject($"Prop Obstacle {obstacle.Id}");
                    viewObject.transform.SetParent(obstaclesRoot, false);
                    view = viewObject.AddComponent<PropObstacleView>();
                    view.Initialize();
                    propObstacleViews.Add(obstacle.Id, view);
                }

                view.Render(obstacle, mapper, IsDebugEnabled());
            }

            RemoveDeadPropObstacleViews(liveIds);
        }

        void SyncAmbientObjectViews()
        {
            var liveIds = new HashSet<int>();

            foreach (var ambientObject in world.AmbientObjects)
            {
                liveIds.Add(ambientObject.Id);
                if (!ambientObjectViews.TryGetValue(ambientObject.Id, out var view))
                {
                    var viewObject = new GameObject($"Ambient Object {ambientObject.Id}");
                    viewObject.transform.SetParent(ambientRoot, false);
                    view = viewObject.AddComponent<AmbientObjectView>();
                    view.Initialize();
                    ambientObjectViews.Add(ambientObject.Id, view);
                }

                var typeMaster = masters.GetAmbientObjectType(ambientObject.Kind);
                view.Render(ambientObject, typeMaster, mapper, IsDebugEnabled());
            }

            RemoveDeadAmbientViews(liveIds);
        }

        void SyncRainbowViews()
        {
            var liveIds = new HashSet<int>();

            foreach (var rainbow in world.Rainbows)
            {
                liveIds.Add(rainbow.Id);
                if (!rainbowViews.TryGetValue(rainbow.Id, out var view))
                {
                    var viewObject = new GameObject($"Rainbow {rainbow.Id}");
                    viewObject.transform.SetParent(rainbowRoot, false);
                    view = viewObject.AddComponent<RainbowView>();
                    view.Initialize();
                    rainbowViews.Add(rainbow.Id, view);
                }

                view.Render(rainbow, mapper);
            }

            RemoveDeadRainbowViews(liveIds);
        }

        void SyncVisualEffectViews()
        {
            var liveIds = new HashSet<int>();

            foreach (var visualEffect in world.VisualEffects)
            {
                liveIds.Add(visualEffect.Id);
                if (!visualEffectViews.TryGetValue(visualEffect.Id, out var view))
                {
                    var viewObject = new GameObject($"Visual Effect {visualEffect.Id}");
                    viewObject.transform.SetParent(effectsRoot, false);
                    view = viewObject.AddComponent<VisualEffectView>();
                    view.Initialize();
                    visualEffectViews.Add(visualEffect.Id, view);
                }

                var effectMaster = masters.VisualEffects.Get(visualEffect.VisualEffectMasterId);
                var rainVisibleHeightRatio = maskAnimationController != null
                    ? maskAnimationController.GetRainVisibleHeightRatio(visualEffect)
                    : 1f;
                view.Render(visualEffect, effectMaster, mapper, rainVisibleHeightRatio);
            }

            RemoveDeadVisualEffectViews(liveIds);
        }

        void SyncRecognitionMaskAnimation()
        {
            if (!enableRecognitionMaskAnimation)
            {
                if (maskAnimationController != null)
                {
                    maskAnimationController.SetVisible(false);
                }

                return;
            }

            EnsureRecognitionMaskAnimation();
            maskAnimationController.Render(world, masters, mapper, Time.deltaTime, IsDebugEnabled());
        }

        void SyncAudioLayers()
        {
            if (!enableAudioLayers)
            {
                if (audioController != null)
                {
                    audioController.SetAudioActive(false);
                }

                return;
            }

            EnsureAudioController();
            var plantGrowthActive = maskAnimationController != null && maskAnimationController.HasGrowingPlants;
            var plantSpawnSequence = maskAnimationController != null ? maskAnimationController.PlantSpawnSequence : 0;
            var plantBloomSequence = maskAnimationController != null ? maskAnimationController.PlantBloomSequence : 0;
            var flowerBurstSequence = maskAnimationController != null ? maskAnimationController.FlowerBurstSequence : 0;
            audioController.UpdateAudio(
                world,
                masters,
                Time.deltaTime,
                plantGrowthActive,
                plantSpawnSequence,
                plantBloomSequence,
                flowerBurstSequence);
        }

        InteractionField FindField(int sourceObjectId)
        {
            foreach (var field in world.InteractionFields)
            {
                if (field.SourceObjectId == sourceObjectId)
                {
                    return field;
                }
            }

            return null;
        }

        bool IsDebugEnabled()
        {
            return InputProvider != null && InputProvider.DebugEnabled;
        }

        bool IsPlantInteractionTouched(
            Vector3 littlePersonWorldPosition,
            float touchRadius,
            bool hasDevelopmentTouch,
            Vector3 developmentTouchWorldPosition)
        {
            var safeTouchRadius = Mathf.Max(0f, touchRadius);
            if (hasDevelopmentTouch &&
                (developmentTouchWorldPosition - littlePersonWorldPosition).sqrMagnitude <= safeTouchRadius * safeTouchRadius)
            {
                return true;
            }

            return maskAnimationController != null &&
                   maskAnimationController.IsInputNearWorldPosition(
                       world.InteractionObjects,
                       littlePersonWorldPosition,
                       safeTouchRadius);
        }

        bool TryGetDevelopmentPersonTouchWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = default;
            if (!enableDevelopmentLeafHangClick || !IsDevelopmentLeafHangClick() || targetCamera == null || mapper == null)
            {
                return false;
            }

            var mousePosition = UnityEngine.Input.mousePosition;
            if (mousePosition.x < 0f || mousePosition.x > Screen.width ||
                mousePosition.y < 0f || mousePosition.y > Screen.height)
            {
                return false;
            }

            worldPosition = targetCamera.ScreenToWorldPoint(new Vector3(
                mousePosition.x,
                mousePosition.y,
                Mathf.Max(0.001f, -targetCamera.transform.position.z)));
            return true;
        }

        static bool IsDevelopmentLeafHangClick()
        {
            return UnityEngine.Input.GetMouseButtonDown(1) &&
                   (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift));
        }

        int SelectedObjectId()
        {
            var inputProvider = InputProvider;
            return mouseInputProvider != null && ReferenceEquals(inputProvider, mouseInputProvider)
                ? mouseInputProvider.SelectedObjectId
                : -1;
        }

        string InputProviderLabel()
        {
            if (InputProvider is UdpRealSenseInputProviderBehaviour udpProvider)
            {
                var age = float.IsPositiveInfinity(udpProvider.PacketAgeSeconds)
                    ? "no packet"
                    : $"{udpProvider.PacketAgeSeconds:0.00}s ago";
                var error = string.IsNullOrEmpty(udpProvider.LastError)
                    ? string.Empty
                    : $"  Error: {udpProvider.LastError}";
                return $"UDP RealSense :{udpProvider.ListenPort}  Frame: {udpProvider.LastFrame}  Packet: {age}{error}";
            }

            if (ReferenceEquals(InputProvider, mouseInputProvider))
            {
                return "Mouse";
            }

            return "None";
        }

        void RemoveDeadInteractionViews(HashSet<int> liveIds)
        {
            var deadIds = new List<int>();
            foreach (var pair in interactionObjectViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (interactionObjectViews[id] != null)
                {
                    Destroy(interactionObjectViews[id].gameObject);
                }

                interactionObjectViews.Remove(id);
            }
        }

        void RemoveDeadWalkableSurfaceViews(HashSet<int> liveIds)
        {
            var deadIds = new List<int>();
            foreach (var pair in walkableSurfaceViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (walkableSurfaceViews[id] != null)
                {
                    Destroy(walkableSurfaceViews[id].gameObject);
                }

                walkableSurfaceViews.Remove(id);
            }
        }

        void RemoveDeadPropObstacleViews(HashSet<int> liveIds)
        {
            var deadIds = new List<int>();
            foreach (var pair in propObstacleViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (propObstacleViews[id] != null)
                {
                    Destroy(propObstacleViews[id].gameObject);
                }

                propObstacleViews.Remove(id);
            }
        }

        void RemoveDeadAmbientViews(HashSet<int> liveIds)
        {
            var deadIds = new List<int>();
            foreach (var pair in ambientObjectViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (ambientObjectViews[id] != null)
                {
                    Destroy(ambientObjectViews[id].gameObject);
                }

                ambientObjectViews.Remove(id);
            }
        }
        void RemoveDeadRainbowViews(HashSet<int> liveIds)
        {
            var deadIds = new List<int>();
            foreach (var pair in rainbowViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (rainbowViews[id] != null)
                {
                    Destroy(rainbowViews[id].gameObject);
                }

                rainbowViews.Remove(id);
            }
        }


        void RemoveDeadVisualEffectViews(HashSet<int> liveIds)
        {
            var deadIds = new List<int>();
            foreach (var pair in visualEffectViews)
            {
                if (!liveIds.Contains(pair.Key))
                {
                    deadIds.Add(pair.Key);
                }
            }

            foreach (var id in deadIds)
            {
                if (visualEffectViews[id] != null)
                {
                    Destroy(visualEffectViews[id].gameObject);
                }

                visualEffectViews.Remove(id);
            }
        }
    }
}
