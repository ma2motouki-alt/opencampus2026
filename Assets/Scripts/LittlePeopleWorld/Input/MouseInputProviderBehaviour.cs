using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Input
{
    public sealed class MouseInputProviderBehaviour : MonoBehaviour, IInteractionInputProvider
    {
        const float StrokeDrawInterval = 0.015f;
        const float StrokeThicknessScale = 0.5f;
        const float StrokeOverlapMultiplier = 1.25f;
        const float StrokeLifetimeSeconds = 10f;

        readonly List<InteractionObject> interactionObjects = new();
        readonly List<TemporaryStroke> temporaryStrokes = new();

        MasterDatabase masters;
        Camera targetCamera;
        InteractionObjectKind currentKind = InteractionObjectKind.Hand;
        Vector2 lastStrokePosition;
        int nextId = 1;
        int selectedObjectId = -1;
        bool inputEnabled = true;
        bool isDrawingStroke;

        public IReadOnlyList<InteractionObject> InteractionObjects => interactionObjects;
        public bool DebugEnabled { get; private set; }
        public int SelectedObjectId => selectedObjectId;
        public bool InputEnabled => inputEnabled;

        public void Initialize(MasterDatabase masterDatabase, Camera camera)
        {
            masters = masterDatabase;
            targetCamera = camera;
        }

        void Awake()
        {
            targetCamera ??= Camera.main;
        }

        void Update()
        {
            masters ??= MasterDatabase.CreateDefault();
            targetCamera ??= Camera.main;

            UpdateModeKeys();
            UpdateMouse();
            UpdateEditKeys();
            RemoveExpiredStrokes();
        }

        void UpdateModeKeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectMode(InteractionObjectKind.Hand);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectMode(InteractionObjectKind.RoundProp);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectMode(InteractionObjectKind.MaskStroke);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4))
            {
                inputEnabled = false;
                isDrawingStroke = false;
                selectedObjectId = -1;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                DebugEnabled = !DebugEnabled;
            }
        }

        void SelectMode(InteractionObjectKind kind)
        {
            currentKind = kind;
            inputEnabled = true;
        }

        void UpdateMouse()
        {
            if (!inputEnabled)
            {
                return;
            }

            var normalized = GetMouseNormalized();
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                BeginMouseInteraction(normalized);
            }

            if (UnityEngine.Input.GetMouseButton(0))
            {
                ContinueMouseInteraction(normalized);
            }

            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                EndMouseInteraction();
            }

            var activeIndex = FindObjectIndexById(selectedObjectId);
            var scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f && activeIndex >= 0)
            {
                interactionObjects[activeIndex].Resize(scroll * 0.012f);
            }

            if (!UnityEngine.Input.GetMouseButton(0))
            {
                ReleaseInactiveDragObjects();
            }
        }

        void BeginMouseInteraction(Vector2 normalized)
        {
            var selectedIndex = FindObjectIndex(normalized);
            if (selectedIndex >= 0)
            {
                selectedObjectId = interactionObjects[selectedIndex].Id;
                return;
            }

            if (currentKind == InteractionObjectKind.MaskStroke)
            {
                isDrawingStroke = true;
                lastStrokePosition = normalized;
                selectedObjectId = -1;
                return;
            }

            selectedIndex = CreateObject(normalized);
            selectedObjectId = interactionObjects[selectedIndex].Id;
        }

        void ContinueMouseInteraction(Vector2 normalized)
        {
            if (isDrawingStroke && currentKind == InteractionObjectKind.MaskStroke)
            {
                var distance = Vector2.Distance(lastStrokePosition, normalized);
                if (distance >= StrokeDrawInterval)
                {
                    CreateMaskStroke(lastStrokePosition, normalized, distance);
                    lastStrokePosition = normalized;
                }

                return;
            }

            var activeIndex = FindObjectIndexById(selectedObjectId);
            if (activeIndex >= 0)
            {
                interactionObjects[activeIndex].MoveTo(normalized, Time.time);
            }
        }

        void EndMouseInteraction()
        {
            isDrawingStroke = false;
            var activeIndex = FindObjectIndexById(selectedObjectId);
            if (activeIndex >= 0)
            {
                interactionObjects[activeIndex].Release();
            }
        }

        void UpdateEditKeys()
        {
            if (!inputEnabled)
            {
                return;
            }

            var activeIndex = FindObjectIndexById(selectedObjectId);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R) && activeIndex >= 0)
            {
                var reverse = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                interactionObjects[activeIndex].Rotate(reverse ? -15f : 15f);
            }

            if (IsDeleteRequested() && activeIndex >= 0)
            {
                interactionObjects.RemoveAt(activeIndex);
                selectedObjectId = -1;
            }
        }

        int CreateObject(Vector2 normalized)
        {
            var type = masters.GetObjectType(currentKind);
            interactionObjects.Add(new InteractionObject(
                nextId++,
                currentKind,
                normalized,
                type.DefaultSize,
                0f,
                type.DefaultHeight,
                InteractionObjectState.Placing));
            return interactionObjects.Count - 1;
        }

        void CreateMaskStroke(Vector2 start, Vector2 end, float distance)
        {
            var type = masters.GetObjectType(InteractionObjectKind.MaskStroke);
            var direction = end - start;
            var stroke = new InteractionObject(
                nextId++,
                InteractionObjectKind.MaskStroke,
                (start + end) * 0.5f,
                new Vector2(distance * StrokeOverlapMultiplier, type.DefaultSize.y * StrokeThicknessScale),
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                type.DefaultHeight,
                InteractionObjectState.Placed);

            interactionObjects.Add(stroke);
            temporaryStrokes.Add(new TemporaryStroke(stroke, Time.time + StrokeLifetimeSeconds));
        }

        void RemoveExpiredStrokes()
        {
            for (var i = temporaryStrokes.Count - 1; i >= 0; i--)
            {
                if (Time.time < temporaryStrokes[i].ExpiryTime)
                {
                    continue;
                }

                interactionObjects.Remove(temporaryStrokes[i].Object);
                temporaryStrokes.RemoveAt(i);
            }
        }

        static bool IsDeleteRequested()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Delete) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Backspace) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.X);
        }

        void ReleaseInactiveDragObjects()
        {
            foreach (var interactionObject in interactionObjects)
            {
                if (interactionObject.State != InteractionObjectState.Placed)
                {
                    interactionObject.Release();
                }
            }
        }

        int FindObjectIndex(Vector2 normalized)
        {
            var padding = masters.TuningParameters.Get(1).InputHitPadding;
            for (var i = interactionObjects.Count - 1; i >= 0; i--)
            {
                if (interactionObjects[i].Contains(normalized, padding))
                {
                    return i;
                }
            }

            return -1;
        }

        int FindObjectIndexById(int objectId)
        {
            for (var i = 0; i < interactionObjects.Count; i++)
            {
                if (interactionObjects[i].Id == objectId)
                {
                    return i;
                }
            }

            return -1;
        }

        Vector2 GetMouseNormalized()
        {
            if (targetCamera == null)
            {
                return new Vector2(0.5f, 0.5f);
            }

            var viewport = targetCamera.ScreenToViewportPoint(UnityEngine.Input.mousePosition);
            return new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(1f - viewport.y));
        }

        readonly struct TemporaryStroke
        {
            public TemporaryStroke(InteractionObject interactionObject, float expiryTime)
            {
                Object = interactionObject;
                ExpiryTime = expiryTime;
            }

            public InteractionObject Object { get; }
            public float ExpiryTime { get; }
        }
    }
}
