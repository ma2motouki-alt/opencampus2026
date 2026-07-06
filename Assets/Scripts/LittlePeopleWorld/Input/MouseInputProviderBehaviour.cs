using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Input
{
    public sealed class MouseInputProviderBehaviour : MonoBehaviour, IInteractionInputProvider
    {
        readonly List<InteractionObject> interactionObjects = new();
        MasterDatabase masters;
        Camera targetCamera;
        InteractionObjectKind currentKind = InteractionObjectKind.Hand;
        int nextId = 1;
        int selectedObjectId = -1;

        // --- 何もしないモード(観察モード)。trueの間はマウス入力を一切処理しない ---
        bool inputEnabled = true;

        // --- 自由曲線（連続配置）用に追加した変数 ---
        Vector2 lastDrawPosition;
        bool isDrawingLine = false;
        
        // ▼ 調整項目 ▼
        readonly float drawInterval = 0.015f;      // 前回の0.04fから小さくし、より細かく配置します
        readonly float thicknessScale = 0.5f;      // 線の太さ（元の棒の半分の太さにします）
        readonly float overlapMultiplier = 1.25f;  // 隙間から落ちないように少し長めにする係数
        readonly float lineLifeTimeSeconds = 10f;  // 線が消えるまでの秒数

        // 10秒で消すためのタイマー管理クラス
        class TemporaryLine
        {
            public InteractionObject Obj;
            public float ExpiryTime;
        }
        readonly List<TemporaryLine> temporaryLines = new();

        public IReadOnlyList<InteractionObject> InteractionObjects => interactionObjects;
        public bool DebugEnabled { get; private set; } = false;
        public int SelectedObjectId => selectedObjectId;

        // 現在「何もしないモード」かどうかを外部(UIなど)からも参照できるように公開
        public bool InputEnabled => inputEnabled;

        public void Initialize(MasterDatabase masterDatabase, Camera camera)
        {
            masters = masterDatabase;
            targetCamera = camera;
        }

        void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        void Update()
        {
            if (masters == null)
            {
                masters = MasterDatabase.CreateDefault();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            UpdateModeKeys();
            UpdateMouse();
            UpdateEditKeys();
            UpdateTemporaryObjects(); // ▼ 追加: 10秒経過した線を消す処理
        }

        void UpdateModeKeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
            {
                currentKind = InteractionObjectKind.Hand;
                inputEnabled = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
            {
                currentKind = InteractionObjectKind.RoundProp;
                inputEnabled = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
            {
                currentKind = InteractionObjectKind.BarProp;
                inputEnabled = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4))
            {
                // 何もしないモード: クリックしても配置・選択・ドラッグ・削除いずれも発生しない
                inputEnabled = false;

                // モード切替時に選択状態や描画中フラグが残らないようにクリア
                isDrawingLine = false;
                selectedObjectId = -1;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                DebugEnabled = !DebugEnabled;
            }
        }

        void UpdateMouse()
        {
            if (!inputEnabled)
            {
                // 何もしないモードでは、マウス入力を一切処理せずに抜ける
                return;
            }

            var normalized = GetMouseNormalized();

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                var selectedIndex = FindObjectIndex(normalized);
                if (selectedIndex < 0)
                {
                    if (currentKind == InteractionObjectKind.BarProp)
                    {
                        // 棒の場合はお絵かきモード開始
                        isDrawingLine = true;
                        lastDrawPosition = normalized;
                        selectedObjectId = -1;
                    }
                    else
                    {
                        selectedIndex = CreateObject(normalized);
                        selectedObjectId = interactionObjects[selectedIndex].Id;
                    }
                }
                else
                {
                    selectedObjectId = interactionObjects[selectedIndex].Id;
                }
            }

            if (UnityEngine.Input.GetMouseButton(0))
            {
                if (isDrawingLine && currentKind == InteractionObjectKind.BarProp)
                {
                    // お絵かきモード：ドラッグ中に連続して棒を生成
                    var distance = Vector2.Distance(lastDrawPosition, normalized);
                    if (distance >= drawInterval)
                    {
                        CreateConnectedBar(lastDrawPosition, normalized, distance);
                        lastDrawPosition = normalized;
                    }
                }
                else
                {
                    var activeIndex = FindObjectIndexById(selectedObjectId);
                    if (activeIndex >= 0)
                    {
                        interactionObjects[activeIndex].MoveTo(normalized, Time.time);
                    }
                }
            }

            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                isDrawingLine = false;

                var activeIndex = FindObjectIndexById(selectedObjectId);
                if (activeIndex >= 0)
                {
                    interactionObjects[activeIndex].Release();
                }
            }

            var scroll = UnityEngine.Input.mouseScrollDelta.y;
            var scrollActiveIndex = FindObjectIndexById(selectedObjectId);
            if (Mathf.Abs(scroll) > 0.001f && scrollActiveIndex >= 0)
            {
                interactionObjects[scrollActiveIndex].Resize(scroll * 0.012f);
            }

            if (!UnityEngine.Input.GetMouseButton(0))
            {
                ReleaseInactiveDragObjects();
            }
        }

        void UpdateEditKeys()
        {
            if (!inputEnabled)
            {
                // 何もしないモードでは回転・削除も無効
                return;
            }

            var activeIndex = FindObjectIndexById(selectedObjectId);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R) && activeIndex >= 0)
            {
                var direction = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift) ? -1f : 1f;
                interactionObjects[activeIndex].Rotate(direction * 15f);
            }

            if (IsDeleteRequested() && activeIndex >= 0)
            {
                interactionObjects.RemoveAt(activeIndex);
                selectedObjectId = -1;
            }
        }

        // --- 追加: 寿命が来た線を削除する処理 ---
        void UpdateTemporaryObjects()
        {
            // リストの要素を削除するので、後ろから前に向かってループします
            for (int i = temporaryLines.Count - 1; i >= 0; i--)
            {
                if (Time.time >= temporaryLines[i].ExpiryTime)
                {
                    // 寿命が来たら、ゲームの世界から消す
                    interactionObjects.Remove(temporaryLines[i].Obj);
                    // 監視リストからも消す
                    temporaryLines.RemoveAt(i);
                }
            }
        }

        int CreateObject(Vector2 normalized)
        {
            var type = masters.GetObjectType(currentKind);
            var obj = new InteractionObject(
                nextId++,
                currentKind,
                normalized,
                type.DefaultSize,
                currentKind == InteractionObjectKind.BarProp ? 20f : 0f,
                type.DefaultHeight,
                InteractionObjectState.Placing);
            interactionObjects.Add(obj);
            return interactionObjects.Count - 1;
        }

        void CreateConnectedBar(Vector2 start, Vector2 end, float distance)
        {
            var type = masters.GetObjectType(currentKind);
            
            var center = (start + end) / 2f;
            var direction = end - start;
            var angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 長さはマウスの移動距離、太さはデフォルトの太さ × thicknessScale（0.5）
            var size = new Vector2(distance * overlapMultiplier, type.DefaultSize.y * thicknessScale);

            var obj = new InteractionObject(
                nextId++,
                currentKind,
                center,
                size,
                angleDegrees,
                type.DefaultHeight,
                InteractionObjectState.Placed 
            );
            
            interactionObjects.Add(obj);

            // 10秒後に消えるように、生成したオブジェクトと消滅時間を登録しておく
            temporaryLines.Add(new TemporaryLine 
            { 
                Obj = obj, 
                ExpiryTime = Time.time + lineLifeTimeSeconds 
            });
        }

        bool IsDeleteRequested()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Delete) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Backspace) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.X);
        }

        void ReleaseInactiveDragObjects()
        {
            for (var i = 0; i < interactionObjects.Count; i++)
            {
                if (interactionObjects[i].State != InteractionObjectState.Placed)
                {
                    interactionObjects[i].Release();
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
            if (objectId < 0)
            {
                return -1;
            }

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
    }
}
