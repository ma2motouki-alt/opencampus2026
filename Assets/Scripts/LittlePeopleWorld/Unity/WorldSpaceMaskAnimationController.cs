using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class WorldSpaceMaskAnimationController : MonoBehaviour
    {
        const int CellsX = 16;
        const int CellsY = 9;

        [Header("Mask")]
        [SerializeField] int maskWidth = 256;
        [SerializeField] int maskHeight = 144;
        [SerializeField] bool includeHandContours = true;
        [SerializeField] bool includeBarContours = true;
        [SerializeField] bool showMask;
        [SerializeField] bool transparentMaskBackground;
        [SerializeField] Color32 maskOnColor = new(255, 255, 255, 255);
        [SerializeField] Color32 maskOffColor = new(8, 8, 10, 255);
        [SerializeField] int minBlobPixels = 40;

        [Header("Particles")]
        [SerializeField] int particleCount = 160;
        [SerializeField] float particleSize = 0.10f;
        [SerializeField] float speedPxPerSec = 55f;
        [SerializeField] float steerLerp = 6f;
        [SerializeField] Color[] particlePalette =
        {
            new(1.00f, 0.85f, 0.25f),
            new(1.00f, 0.45f, 0.85f),
            new(0.45f, 0.95f, 0.85f),
            new(0.75f, 0.55f, 1.00f),
            new(1.00f, 1.00f, 1.00f)
        };

        [Header("Particle Separation")]
        [SerializeField] float separationRadiusPx = 8f;
        [SerializeField] float separationGain = 380f;
        [SerializeField] float targetJitterPx = 9f;

        [Header("Mask Following")]
        [SerializeField] int blurRadius = 4;
        [SerializeField] float contourLevel = 0.5f;
        [SerializeField] float correctionGain = 5f;
        [SerializeField] float senseThreshold = 0.03f;
        [SerializeField] float penSenseRadiusPx = 70f;
        [SerializeField] float minPenAttractRadiusPx = 18f;
        [SerializeField] float penAttractRadiusPerSqrtPixel = 2.5f;

        [Header("Edge Walk")]
        [SerializeField] float edgeMarginPx = 7f;
        [SerializeField] float edgePullGain = 0.12f;

        [Header("Particle Cloud Rain")]
        [SerializeField] bool enableParticleCloudRain = true;
        [SerializeField] float particleCloudTouchRadius = 0.01f;
        [SerializeField] float particleCloudRainCooldownSeconds = 0.2f;

        [Header("Rain To Plants")]
        [SerializeField] bool enableRainPlants = true;
        [SerializeField] float rainFallSpeedPxPerSec = 40f;
        [SerializeField] float groundYPx = 3f;
        [SerializeField] int maxPlants = 10;
        [SerializeField] float plantMaxHeightPx = 46f;
        [SerializeField] float plantStemWidthPx = 2.0f;
        [SerializeField] float plantFlowerSizePx = 18f;
        [SerializeField] float plantSpawnMergingRadiusRatio = 0.5f;
        [SerializeField] float plantInfluenceRadiusRatio = 0.8f;
        [SerializeField] float plantClimbAttractRadiusRatio = 0.3f;
        // 登り中(IsClimbing)の粒を、通常移動より確実に花へ向かわせるための倍率。
        // 1.0だと通常移動と同じ強さのまま、値を上げるほど登りが速く・向きの追従も鋭くなる。
        [SerializeField] float plantClimbSpeedMultiplier = 2.0f;
        [SerializeField] float plantSeedlingDuration = 10f;
        [SerializeField] float plantGrowingDuration = 10f;
        [SerializeField] float plantWiltingStartDelay = 5f;
        [SerializeField] float plantWiltingDuration = 15f;

        [Header("Bloom Attraction")]
        [SerializeField] float bloomAttractRadiusRatio = 0.5f;
        [SerializeField] float bloomSphereRadiusRatio = 1.0f;
        [SerializeField] float bloomAttractForce = 300f;

        // 花のはじけ(A): rain_shape版はペン描画で変化したマスクピクセル数(burstMinChangedPixels)を
        // 閾値にしていたが、mainでは手の輪郭(Hand種別のInteractionObject)がそのまま入力になるため、
        // 「ラスタのノイズ的な単発ピクセル」を弾く必要がなく、bloomSphereRadius(既存の吸い付き範囲)への
        // 幾何学的な重なり判定だけで十分に安定する。そのため burstMinChangedPixels 相当のしきい値は
        // 廃止し、代わりに「手の輪郭が bloomSphereRadius 以内に入った瞬間」を発火条件にしている。
        [Header("Flower Burst")]
        [SerializeField] float burstInitialSpeedPxPerSec = 220f;
        [SerializeField] float burstFreeSeconds = 1.5f;
        [SerializeField] float burstReattachCooldownSeconds = 2.0f;

        readonly List<Particle> particles = new();
        readonly List<PlantModel> plants = new();
        readonly Dictionary<int, PlantViewRuntime> plantViews = new();
        readonly Dictionary<int, float> rainActiveSeconds = new();
        readonly Dictionary<int, int> rainLandedCounts = new();
        readonly Dictionary<int, float> particleCloudRainCooldowns = new();
        readonly List<int> floodStack = new();
        readonly List<int> componentBuffer = new();

        bool[] mask;
        bool[] effectiveMask;
        bool[] visited;
        int[] componentSizeMap;
        int[] cellCounts = Array.Empty<int>();
        int[] cellMaxComponentSize = Array.Empty<int>();
        float[] field;
        float[] fieldTmp;
        Color32[] pixels;
        Texture2D maskTexture;
        SpriteRenderer maskRenderer;
        Transform particleRoot;
        Transform plantRoot;
        NormalizedScreenMapper mapper;
        int effectiveWhiteCount;
        int nextPlantId = 1;

        int MaskW => Mathf.Clamp(maskWidth, 16, 1024);
        int MaskH => Mathf.Clamp(maskHeight, 16, 1024);

        public void Render(World world, MasterDatabase masters, NormalizedScreenMapper screenMapper, float deltaTime, bool debugEnabled)
        {
            if (world == null || masters == null || screenMapper == null)
            {
                SetVisible(false);
                return;
            }

            mapper = screenMapper;
            EnsureRuntime();

            BuildMaskFromInteractionObjects(world.InteractionObjects);
            RecomputeEffectiveMask();
            RebuildField();
            RebuildCellCounts();
            RenderMaskTexture();

            var dt = Mathf.Clamp(deltaTime, 0f, 0.05f);

            // 粒の操舵より前に判定する(はじけた粒がこのフレームから自由飛散状態になるようにするため)
            HandleFlowerBurst(world.InteractionObjects);

            AdvanceParticles(dt);
            TriggerCloudRainFromParticles(world, masters, dt);
            AdvanceRainPlants(world, masters, dt);
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            if (maskRenderer != null)
            {
                maskRenderer.enabled = visible && showMask;
            }

            if (particleRoot != null)
            {
                particleRoot.gameObject.SetActive(visible);
            }

            if (plantRoot != null)
            {
                plantRoot.gameObject.SetActive(visible);
            }
        }

        void EnsureRuntime()
        {
            EnsureRoots();
            EnsureBuffers();
            EnsureMaskSprite();
            EnsureParticles();
        }

        void EnsureRoots()
        {
            if (maskRenderer == null)
            {
                var maskObject = new GameObject("Recognition Binary Mask");
                maskObject.transform.SetParent(transform, false);
                maskRenderer = maskObject.AddComponent<SpriteRenderer>();
                maskRenderer.sortingOrder = -10;
            }

            if (particleRoot == null)
            {
                particleRoot = new GameObject("Mask Particles").transform;
                particleRoot.SetParent(transform, false);
            }

            if (plantRoot == null)
            {
                plantRoot = new GameObject("Mask Plants").transform;
                plantRoot.SetParent(transform, false);
            }
        }

        void EnsureBuffers()
        {
            var length = MaskW * MaskH;
            if (mask != null && mask.Length == length)
            {
                return;
            }

            mask = new bool[length];
            effectiveMask = new bool[length];
            visited = new bool[length];
            componentSizeMap = new int[length];
            field = new float[length];
            fieldTmp = new float[length];
            pixels = new Color32[length];
            cellCounts = new int[CellsX * CellsY];
            cellMaxComponentSize = new int[CellsX * CellsY];

            maskTexture = new Texture2D(MaskW, MaskH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

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

        void EnsureParticles()
        {
            var targetCount = Mathf.Clamp(particleCount, 0, 2000);
            while (particles.Count < targetCount)
            {
                particles.Add(CreateParticle(particles.Count));
            }

            while (particles.Count > targetCount)
            {
                var last = particles[particles.Count - 1];
                if (last.Root != null)
                {
                    Destroy(last.Root.gameObject);
                }

                particles.RemoveAt(particles.Count - 1);
            }
        }

        Particle CreateParticle(int index)
        {
            var root = new GameObject($"particle_{index}").transform;
            root.SetParent(particleRoot, false);

            var glow = CreateRenderer(root, "Glow", RuntimeSpriteFactory.Circle, -4);
            var body = CreateRenderer(root, "Body", RuntimeSpriteFactory.Circle, 10);
            var head = CreateRenderer(root, "Head", RuntimeSpriteFactory.Circle, 11);
            head.transform.localPosition = new Vector3(particleSize * 0.12f, particleSize * 0.16f, 0f);

            var palette = particlePalette is { Length: > 0 }
                ? particlePalette
                : new[] { Color.white };
            var color = palette[UnityEngine.Random.Range(0, palette.Length)];
            body.transform.localScale = Vector3.one * particleSize;
            head.transform.localScale = Vector3.one * particleSize * 0.58f;
            body.color = color;
            head.color = Color.Lerp(color, Color.white, 0.35f);

            return new Particle
            {
                Pos = new Vector2(UnityEngine.Random.Range(0f, MaskW), UnityEngine.Random.Range(0f, MaskH)),
                Vel = UnityEngine.Random.insideUnitCircle.normalized * speedPxPerSec,
                SpeedScale = UnityEngine.Random.Range(0.75f, 1.35f),
                Jitter = UnityEngine.Random.insideUnitCircle * targetJitterPx,
                JitterTimer = UnityEngine.Random.Range(0f, 2f),
                BodyColor = color,
                Root = root,
                GlowRenderer = glow,
                BodyRenderer = body,
                HeadRenderer = head
            };
        }

        void BuildMaskFromInteractionObjects(IReadOnlyList<InteractionObject> objects)
        {
            Array.Clear(mask, 0, mask.Length);

            foreach (var interactionObject in objects)
            {
                if (!ShouldPaintMask(interactionObject))
                {
                    continue;
                }

                FillPolygon(interactionObject.ContourPoints);
            }
        }

        bool ShouldPaintMask(InteractionObject interactionObject)
        {
            if (interactionObject == null ||
                interactionObject.ShapeKind != InteractionShapeKind.Contour ||
                interactionObject.ContourPoints.Count < 3)
            {
                return false;
            }

            return interactionObject.Kind switch
            {
                InteractionObjectKind.Hand => includeHandContours,
                InteractionObjectKind.BarProp => includeBarContours,
                _ => false
            };
        }

        void FillPolygon(IReadOnlyList<Vector2> normalizedPoints)
        {
            var polygon = new Vector2[normalizedPoints.Count];
            var minX = MaskW - 1;
            var minY = MaskH - 1;
            var maxX = 0;
            var maxY = 0;

            for (var i = 0; i < normalizedPoints.Count; i++)
            {
                var point = NormalizedToMaskPx(normalizedPoints[i]);
                polygon[i] = point;
                minX = Mathf.Min(minX, Mathf.FloorToInt(point.x));
                minY = Mathf.Min(minY, Mathf.FloorToInt(point.y));
                maxX = Mathf.Max(maxX, Mathf.CeilToInt(point.x));
                maxY = Mathf.Max(maxY, Mathf.CeilToInt(point.y));
            }

            minX = Mathf.Clamp(minX, 0, MaskW - 1);
            minY = Mathf.Clamp(minY, 0, MaskH - 1);
            maxX = Mathf.Clamp(maxX, 0, MaskW - 1);
            maxY = Mathf.Clamp(maxY, 0, MaskH - 1);

            for (var y = minY; y <= maxY; y++)
            {
                var row = y * MaskW;
                for (var x = minX; x <= maxX; x++)
                {
                    if (IsPointInsidePolygon(polygon, new Vector2(x + 0.5f, y + 0.5f)))
                    {
                        mask[row + x] = true;
                    }
                }
            }
        }

        // ================= 花のはじけ(A) =================
        //
        // rain_shape版はペン/消しゴムで変化したマスクピクセルが花の吸い付き範囲(bloomSphereRadius)
        // 内に一定数以上あれば発火していたが、mainでは手の輪郭(Hand種別のInteractionObject)が
        // 直接の入力になるため、「手の輪郭がbloomSphereRadius以内に入った瞬間」を発火条件とする。
        //
        // 「触れている間ずっと」ではなく「触れた瞬間」だけ発火させたいので、
        // 各植物ごとに前フレームの接触状態(PlantModel.HandTouchingBloom)を保持し、
        // false→true に変化した立ち上がりエッジでのみ BurstPlant を呼ぶ。
        void HandleFlowerBurst(IReadOnlyList<InteractionObject> objects)
        {
            if (plants.Count == 0)
            {
                return;
            }

            foreach (var plant in plants)
            {
                if (!plant.IsBloomable)
                {
                    // 開花していない植物には、はじけの接触状態を持たせない
                    plant.HandTouchingBloom = false;
                    continue;
                }

                var bloomPos = plant.BloomPosition;
                var bloomSphereRadius = Mathf.Max(4f, plant.HeightPx * bloomSphereRadiusRatio);
                var isTouchingNow = IsHandContourNearBloom(objects, bloomPos, bloomSphereRadius);

                if (isTouchingNow && !plant.HandTouchingBloom)
                {
                    BurstPlant(plant, bloomPos);
                }

                plant.HandTouchingBloom = isTouchingNow;
            }
        }

        // Hand種別・輪郭形状のInteractionObjectのうち、いずれかの輪郭がbloomPosから
        // bloomSphereRadius以内に重なっているかを判定する(1つでも重なっていればtrue)。
        bool IsHandContourNearBloom(IReadOnlyList<InteractionObject> objects, Vector2 bloomPosPx, float bloomSphereRadiusPx)
        {
            foreach (var interactionObject in objects)
            {
                if (interactionObject == null ||
                    interactionObject.Kind != InteractionObjectKind.Hand ||
                    interactionObject.ShapeKind != InteractionShapeKind.Contour ||
                    interactionObject.ContourPoints.Count < 3)
                {
                    continue;
                }

                var polygon = new Vector2[interactionObject.ContourPoints.Count];
                for (var i = 0; i < polygon.Length; i++)
                {
                    polygon[i] = NormalizedToMaskPx(interactionObject.ContourPoints[i]);
                }

                if (DistancePointToPolygon(polygon, bloomPosPx) <= bloomSphereRadiusPx)
                {
                    return true;
                }
            }

            return false;
        }

        // 点から多角形までの最短距離。点が多角形の内側にある場合は0を返す。
        static float DistancePointToPolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            if (IsPointInsidePolygon(polygon, point))
            {
                return 0f;
            }

            var minDistance = float.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var distance = DistancePointToSegment(point, a, b);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            return minDistance;
        }

        static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
            {
                return Vector2.Distance(point, a);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            var closest = a + segment * t;
            return Vector2.Distance(point, closest);
        }

        // 指定した花に現在吸い付いている粒を、花の中心から放射状に弾き飛ばす。
        // はじけた粒は一定時間「自由飛散状態」(BurstFreeTimer > 0)となり、操舵されず慣性で飛ぶ。
        // また一定時間(ReattachCooldown)は、はじけた本人はどの花にも再吸着できない。
        void BurstPlant(PlantModel plant, Vector2 bloomPos)
        {
            foreach (var particle in particles)
            {
                if (!particle.IsBloomAttached || particle.AttachedPlant != plant)
                {
                    continue;
                }

                var dir = particle.Pos - bloomPos;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : UnityEngine.Random.insideUnitCircle.normalized;

                particle.Vel = dir * burstInitialSpeedPxPerSec;
                particle.IsBloomAttached = false;
                particle.IsClimbing = false;
                particle.AttachedPlant = null;
                particle.BurstFreeTimer = burstFreeSeconds;
                particle.ReattachCooldown = burstReattachCooldownSeconds;
            }
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

        void AdvanceParticles(float dt)
        {
            var separation = new Vector2[particles.Count];
            ComputeSeparation(separation, dt);

            for (var i = 0; i < particles.Count; i++)
            {
                var particle = particles[i];
                UpdateJitter(particle, dt);
                UpdateParticle(particle, separation[i], dt);
                RenderParticle(particle, i);
            }
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
        }

        void UpdateJitter(Particle particle, float dt)
        {
            particle.JitterTimer -= dt;
            if (particle.JitterTimer > 0f)
            {
                return;
            }

            particle.Jitter = UnityEngine.Random.insideUnitCircle * targetJitterPx;
            particle.JitterTimer = UnityEngine.Random.Range(1.2f, 2.6f);
        }

        void UpdateParticle(Particle particle, Vector2 separationForce, float dt)
        {
            // タイマー類を減衰させる(状態に関わらず毎フレーム進める)
            if (particle.ReattachCooldown > 0f)
            {
                particle.ReattachCooldown = Mathf.Max(0f, particle.ReattachCooldown - dt);
            }

            // 0. 自由飛散状態(はじけた直後)。他の操舵より最優先で、分離力だけを受けて慣性で飛ぶ。
            if (particle.BurstFreeTimer > 0f)
            {
                particle.BurstFreeTimer = Mathf.Max(0f, particle.BurstFreeTimer - dt);

                particle.Vel += separationForce;
                particle.Pos += particle.Vel * dt;

                // 画面の縁で緩やかに跳ね返して、外へ突き抜けないようにする
                if (particle.Pos.x < 1f || particle.Pos.x > MaskW - 2f)
                {
                    particle.Vel.x *= -0.6f;
                }

                if (particle.Pos.y < 1f || particle.Pos.y > MaskH - 2f)
                {
                    particle.Vel.y *= -0.6f;
                }

                ClampParticlePosition(particle);
                return;
            }

            if (particle.IsBloomAttached && particle.AttachedPlant != null && particle.AttachedPlant.IsBloomable)
            {
                UpdateBloomAttached(particle, particle.AttachedPlant, separationForce, dt);
                return;
            }

            if (particle.IsBloomAttached)
            {
                particle.IsBloomAttached = false;
                particle.AttachedPlant = null;
            }

            PlantModel plant;
            if (particle.IsClimbing &&
                particle.AttachedPlant != null &&
                particle.AttachedPlant.CurrentStage != PlantStage.Dead)
            {
                plant = particle.AttachedPlant;
            }
            else
            {
                particle.IsClimbing = false;
                plant = FindNearestAlivePlantWithinInfluence(particle.Pos);
            }

            Vector2 desiredDirection;
            if (plant != null)
            {
                (desiredDirection, particle.IsClimbing) = ComputePlantApproach(particle, plant);
                particle.AttachedPlant = plant;
            }
            else
            {
                particle.AttachedPlant = null;
                if (effectiveWhiteCount == 0)
                {
                    desiredDirection = EdgeSteer(particle);
                }
                else
                {
                    var fieldValue = SampleField(particle.Pos);
                    desiredDirection = fieldValue >= senseThreshold
                        ? ContourSteer(particle, fieldValue)
                        : SeekNearestCluster(particle);
                }
            }

            var combined = desiredDirection.normalized + separationForce;
            if (combined.sqrMagnitude < 0.0001f)
            {
                combined = particle.Vel.sqrMagnitude > 0.0001f ? particle.Vel.normalized : UnityEngine.Random.insideUnitCircle;
            }

        // 登り中は花への向きをより強く・より速く追従させる(plantClimbSpeedMultiplier)
            var climbBoost = particle.IsClimbing ? Mathf.Max(1f, plantClimbSpeedMultiplier) : 1f;

            var desiredVelocity = combined.normalized * speedPxPerSec * particle.SpeedScale * climbBoost;
            var t = 1f - Mathf.Exp(-steerLerp * climbBoost * dt);
            particle.Vel = Vector2.Lerp(particle.Vel, desiredVelocity, t);

            if (particle.IsClimbing)
            {
                particle.Vel.y = Mathf.Max(particle.Vel.y, 0f);
            }

            particle.Pos += particle.Vel * dt;
            ClampParticlePosition(particle);

            if (particle.IsClimbing && plant != null && plant.IsBloomable && particle.ReattachCooldown <= 0f)
            {
                var bloomAttractRadius = Mathf.Max(4f, plant.HeightPx * bloomAttractRadiusRatio);
                if (Vector2.Distance(particle.Pos, plant.BloomPosition) <= bloomAttractRadius)
                {
                    particle.IsBloomAttached = true;
                    particle.IsClimbing = false;
                }
            }
        }

        void UpdateBloomAttached(Particle particle, PlantModel plant, Vector2 separationForce, float dt)
        {
            var toCenter = plant.BloomPosition - particle.Pos;
            var pull = toCenter.sqrMagnitude > 0.0001f
                ? toCenter.normalized * (bloomAttractForce * dt)
                : Vector2.zero;

            particle.Vel = pull + separationForce;
            particle.Pos += particle.Vel * dt;
            ClampParticlePosition(particle);

            var bloomSphereRadius = Mathf.Max(4f, plant.HeightPx * bloomSphereRadiusRatio);
            if (Vector2.Distance(particle.Pos, plant.BloomPosition) > bloomSphereRadius)
            {
                particle.IsBloomAttached = false;
                particle.AttachedPlant = null;
            }
        }

        void ClampParticlePosition(Particle particle)
        {
            particle.Pos.x = Mathf.Clamp(particle.Pos.x, 1f, MaskW - 2f);
            particle.Pos.y = Mathf.Clamp(particle.Pos.y, 1f, MaskH - 2f);
        }

        PlantModel FindNearestAlivePlantWithinInfluence(Vector2 position)
        {
            PlantModel best = null;
            var bestDistance = float.MaxValue;

            foreach (var plant in plants)
            {
                if (plant.CurrentStage == PlantStage.Dead)
                {
                    continue;
                }

                var influenceRadius = Mathf.Max(6f, plant.HeightPx * plantInfluenceRadiusRatio);
                var distance = Vector2.Distance(position, plant.Position);
                if (distance <= influenceRadius && distance < bestDistance)
                {
                    best = plant;
                    bestDistance = distance;
                }
            }

            return best;
        }

        (Vector2 Direction, bool IsClimbing) ComputePlantApproach(Particle particle, PlantModel plant)
        {
            // 根元(Position)の1点だけでなく、根元→花(BloomPosition)の「茎全体の線分」までの
            // 最短距離で判定する。こうすることで、判定範囲が根元を中心とした円ではなく、
            // 茎の先端まで届くカプセル状の領域になる。
            var distanceToStem = DistancePointToSegment(particle.Pos, plant.Position, plant.BloomPosition);
            var climbRadius = Mathf.Max(4f, plant.HeightPx * plantClimbAttractRadiusRatio);

            if (distanceToStem <= climbRadius)
            {
                var toTop = plant.BloomPosition - particle.Pos;
                return (toTop.sqrMagnitude > 0.0001f ? toTop.normalized : Vector2.up, true);
            }

            var toBase = plant.Position - particle.Pos;
            return (toBase.sqrMagnitude > 0.0001f ? toBase.normalized : Vector2.zero, false);
        }

        void ComputeSeparation(Vector2[] outForces, float dt)
        {
            var radiusSquared = separationRadiusPx * separationRadiusPx;

            for (var i = 0; i < particles.Count; i++)
            {
                var accum = Vector2.zero;
                var current = particles[i].Pos;

                for (var j = 0; j < particles.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    var diff = current - particles[j].Pos;
                    var distanceSquared = diff.sqrMagnitude;
                    if (distanceSquared > radiusSquared || distanceSquared < 0.0001f)
                    {
                        continue;
                    }

                    var distance = Mathf.Sqrt(distanceSquared);
                    accum += diff / distance * (1f - distance / separationRadiusPx);
                }

                outForces[i] = accum * (separationGain * dt);
            }
        }

        Vector2 ContourSteer(Particle particle, float fieldValue)
        {
            var gradient = FieldGradient(particle.Pos);
            if (gradient.sqrMagnitude < 0.00000001f)
            {
                return particle.Vel.sqrMagnitude > 0.0001f ? particle.Vel : UnityEngine.Random.insideUnitCircle;
            }

            var normal = gradient.normalized;
            var tangent = new Vector2(-normal.y, normal.x);
            var correction = normal * ((contourLevel - fieldValue) * correctionGain);
            return tangent + correction;
        }

        Vector2 EdgeSteer(Particle particle)
        {
            var position = particle.Pos;
            var minX = edgeMarginPx;
            var maxX = MaskW - edgeMarginPx;
            var minY = edgeMarginPx;
            var maxY = MaskH - edgeMarginPx;

            var cx = Mathf.Clamp(position.x, minX, maxX);
            var cy = Mathf.Clamp(position.y, minY, maxY);

            var dL = cx - minX;
            var dR = maxX - cx;
            var dB = cy - minY;
            var dT = maxY - cy;
            var nearestDistance = Mathf.Min(Mathf.Min(dL, dR), Mathf.Min(dB, dT));

            Vector2 nearest;
            Vector2 tangent;
            if (Mathf.Approximately(nearestDistance, dB))
            {
                nearest = new Vector2(cx, minY);
                tangent = Vector2.right;
            }
            else if (Mathf.Approximately(nearestDistance, dR))
            {
                nearest = new Vector2(maxX, cy);
                tangent = Vector2.up;
            }
            else if (Mathf.Approximately(nearestDistance, dT))
            {
                nearest = new Vector2(cx, maxY);
                tangent = Vector2.left;
            }
            else
            {
                nearest = new Vector2(minX, cy);
                tangent = Vector2.down;
            }

            nearest += particle.Jitter;
            return tangent + (nearest - position) * edgePullGain;
        }

        Vector2 SeekNearestCluster(Particle particle)
        {
            var position = particle.Pos;
            var cellWidth = (float)MaskW / CellsX;
            var cellHeight = (float)MaskH / CellsY;
            var best = float.MaxValue;
            var target = position;
            var bestCellIndex = -1;
            var found = false;

            for (var cy = 0; cy < CellsY; cy++)
            {
                for (var cx = 0; cx < CellsX; cx++)
                {
                    var cellIndex = cy * CellsX + cx;
                    if (cellCounts[cellIndex] == 0)
                    {
                        continue;
                    }

                    var center = new Vector2((cx + 0.5f) * cellWidth, (cy + 0.5f) * cellHeight);
                    var distance = (center - position).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        target = center;
                        bestCellIndex = cellIndex;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                return EdgeSteer(particle);
            }

            var componentSize = cellMaxComponentSize[bestCellIndex];
            var sizeBasedRadius = minPenAttractRadiusPx + Mathf.Sqrt(componentSize) * penAttractRadiusPerSqrtPixel;
            var attractRadius = Mathf.Min(sizeBasedRadius, penSenseRadiusPx);
            if (best > attractRadius * attractRadius)
            {
                return EdgeSteer(particle);
            }

            return target + particle.Jitter - position;
        }

        void TriggerCloudRainFromParticles(World world, MasterDatabase masters, float dt)
        {
            AdvanceParticleCloudRainCooldowns(dt);
            if (!enableParticleCloudRain || particles.Count == 0)
            {
                return;
            }

            var tuning = masters.TuningParameters.Get(1);
            var touchedCloudIds = new HashSet<int>();
            var touchRadiusPadding = Mathf.Max(0f, particleCloudTouchRadius);

            foreach (var ambientObject in world.AmbientObjects)
            {
                if (ambientObject.Kind != AmbientObjectKind.Cloud)
                {
                    continue;
                }

                touchedCloudIds.Add(ambientObject.Id);
                if (particleCloudRainCooldowns.TryGetValue(ambientObject.Id, out var cooldown) &&
                    cooldown > 0f)
                {
                    continue;
                }

                if (!IsCloudTouchedByAnyParticle(ambientObject, touchRadiusPadding))
                {
                    continue;
                }

                if (world.MarkCloudTouchedByExternalSource(ambientObject.Id, tuning.RainLingerSeconds))
                {
                    particleCloudRainCooldowns[ambientObject.Id] = Mathf.Max(0.01f, particleCloudRainCooldownSeconds);
                }
            }

            RemoveDeadParticleCloudCooldowns(touchedCloudIds);
        }

        void AdvanceParticleCloudRainCooldowns(float dt)
        {
            if (particleCloudRainCooldowns.Count == 0)
            {
                return;
            }

            var keys = new List<int>(particleCloudRainCooldowns.Keys);
            foreach (var key in keys)
            {
                particleCloudRainCooldowns[key] = Mathf.Max(0f, particleCloudRainCooldowns[key] - dt);
            }
        }

        void RemoveDeadParticleCloudCooldowns(HashSet<int> liveCloudIds)
        {
            var dead = new List<int>();
            foreach (var key in particleCloudRainCooldowns.Keys)
            {
                if (!liveCloudIds.Contains(key))
                {
                    dead.Add(key);
                }
            }

            foreach (var key in dead)
            {
                particleCloudRainCooldowns.Remove(key);
            }
        }

        bool IsCloudTouchedByAnyParticle(AmbientObject cloud, float touchRadiusPadding)
        {
            var touchRadius = cloud.ContactRadius + touchRadiusPadding;
            var touchRadiusSqr = touchRadius * touchRadius;
            foreach (var particle in particles)
            {
                var particlePosition = MaskPxToNormalized(particle.Pos);
                if ((particlePosition - cloud.Position).sqrMagnitude <= touchRadiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        void AdvanceRainPlants(World world, MasterDatabase masters, float dt)
        {
            if (enableRainPlants)
            {
                UpdateRainLanding(world, masters, dt);
            }

            UpdatePlants(dt);
        }

        void UpdateRainLanding(World world, MasterDatabase masters, float dt)
        {
            var liveKeys = new HashSet<int>();

            foreach (var effect in world.VisualEffects)
            {
                var effectMaster = masters.VisualEffects.Get(effect.VisualEffectMasterId);
                if (effectMaster.Kind != VisualEffectKind.RainColumn)
                {
                    continue;
                }

                var key = effect.SourceObjectId != 0 ? effect.SourceObjectId : effect.Id;
                liveKeys.Add(key);

                rainActiveSeconds.TryGetValue(key, out var activeSeconds);
                rainLandedCounts.TryGetValue(key, out var landedCount);
                activeSeconds += dt;

                var rainOrigin = NormalizedToMaskPx(effect.Position);
                var fallDistance = Mathf.Max(1f, rainOrigin.y - groundYPx);
                var landingInterval = fallDistance / Mathf.Max(1f, rainFallSpeedPxPerSec);
                var shouldHaveLanded = Mathf.FloorToInt(activeSeconds / Mathf.Max(0.05f, landingInterval));

                while (landedCount < shouldHaveLanded)
                {
                    var widthPx = Mathf.Max(2f, effect.Size.x * MaskW * 0.45f);
                    var jitterX = UnityEngine.Random.Range(-widthPx, widthPx);
                    OnRainLanded(new Vector2(Mathf.Clamp(rainOrigin.x + jitterX, 1f, MaskW - 2f), groundYPx));
                    landedCount++;
                }

                rainActiveSeconds[key] = activeSeconds;
                rainLandedCounts[key] = landedCount;
            }

            RemoveDeadRainSources(liveKeys);
        }

        void RemoveDeadRainSources(HashSet<int> liveKeys)
        {
            var dead = new List<int>();
            foreach (var key in rainActiveSeconds.Keys)
            {
                if (!liveKeys.Contains(key))
                {
                    dead.Add(key);
                }
            }

            foreach (var key in dead)
            {
                rainActiveSeconds.Remove(key);
                rainLandedCounts.Remove(key);
            }
        }

        void OnRainLanded(Vector2 landingPositionPx)
        {
            PlantModel target = null;
            var bestDistance = float.MaxValue;

            foreach (var plant in plants)
            {
                if (plant.CurrentStage == PlantStage.Dead)
                {
                    continue;
                }

                var mergeRadius = Mathf.Max(6f, plant.HeightPx * plantSpawnMergingRadiusRatio);
                var distance = Vector2.Distance(landingPositionPx, plant.Position);
                if (distance <= mergeRadius && distance < bestDistance)
                {
                    target = plant;
                    bestDistance = distance;
                }
            }

            if (target != null)
            {
                target.ReceiveRain();
                return;
            }

            if (plants.Count >= maxPlants)
            {
                return;
            }

            var newPlant = new PlantModel(
                nextPlantId++,
                landingPositionPx,
                plantSeedlingDuration,
                plantGrowingDuration,
                plantWiltingStartDelay,
                plantWiltingDuration,
                plantMaxHeightPx);
            newPlant.ReceiveRain();
            plants.Add(newPlant);
        }

        void UpdatePlants(float dt)
        {
            for (var i = plants.Count - 1; i >= 0; i--)
            {
                plants[i].Advance(dt);
                if (plants[i].CurrentStage == PlantStage.Dead)
                {
                    plants.RemoveAt(i);
                }
            }

            SyncPlantViews();
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
                    plantFlowerSizePx);
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

        void RecomputeEffectiveMask()
        {
            Array.Clear(effectiveMask, 0, effectiveMask.Length);
            Array.Clear(visited, 0, visited.Length);
            Array.Clear(componentSizeMap, 0, componentSizeMap.Length);
            effectiveWhiteCount = 0;

            for (var start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || visited[start])
                {
                    continue;
                }

                floodStack.Clear();
                componentBuffer.Clear();
                floodStack.Add(start);
                visited[start] = true;

                while (floodStack.Count > 0)
                {
                    var index = floodStack[floodStack.Count - 1];
                    floodStack.RemoveAt(floodStack.Count - 1);
                    componentBuffer.Add(index);

                    var x = index % MaskW;
                    var y = index / MaskW;
                    TryVisitNeighbor(x + 1, y);
                    TryVisitNeighbor(x - 1, y);
                    TryVisitNeighbor(x, y + 1);
                    TryVisitNeighbor(x, y - 1);
                }

                if (componentBuffer.Count < minBlobPixels)
                {
                    continue;
                }

                foreach (var index in componentBuffer)
                {
                    effectiveMask[index] = true;
                    componentSizeMap[index] = componentBuffer.Count;
                }

                effectiveWhiteCount += componentBuffer.Count;
            }
        }

        void TryVisitNeighbor(int x, int y)
        {
            if (x < 0 || x >= MaskW || y < 0 || y >= MaskH)
            {
                return;
            }

            var index = y * MaskW + x;
            if (!mask[index] || visited[index])
            {
                return;
            }

            visited[index] = true;
            floodStack.Add(index);
        }

        void RebuildCellCounts()
        {
            Array.Clear(cellCounts, 0, cellCounts.Length);
            Array.Clear(cellMaxComponentSize, 0, cellMaxComponentSize.Length);

            for (var y = 0; y < MaskH; y++)
            {
                var row = y * MaskW;
                var cellY = y * CellsY / MaskH;
                for (var x = 0; x < MaskW; x++)
                {
                    if (!effectiveMask[row + x])
                    {
                        continue;
                    }

                    var cellX = x * CellsX / MaskW;
                    var cellIndex = cellY * CellsX + cellX;
                    cellCounts[cellIndex]++;
                    if (componentSizeMap[row + x] > cellMaxComponentSize[cellIndex])
                    {
                        cellMaxComponentSize[cellIndex] = componentSizeMap[row + x];
                    }
                }
            }
        }

        void RebuildField()
        {
            var radius = Mathf.Clamp(blurRadius, 1, 32);
            var inv = 1f / (2 * radius + 1);

            for (var y = 0; y < MaskH; y++)
            {
                var row = y * MaskW;
                for (var x = 0; x < MaskW; x++)
                {
                    var sum = 0f;
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var xx = Mathf.Clamp(x + dx, 0, MaskW - 1);
                        if (effectiveMask[row + xx])
                        {
                            sum += 1f;
                        }
                    }

                    fieldTmp[row + x] = sum * inv;
                }
            }

            for (var x = 0; x < MaskW; x++)
            {
                for (var y = 0; y < MaskH; y++)
                {
                    var sum = 0f;
                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        var yy = Mathf.Clamp(y + dy, 0, MaskH - 1);
                        sum += fieldTmp[yy * MaskW + x];
                    }

                    field[y * MaskW + x] = sum * inv;
                }
            }
        }

        float SampleField(Vector2 point)
        {
            var x = Mathf.Clamp(point.x, 0f, MaskW - 1.001f);
            var y = Mathf.Clamp(point.y, 0f, MaskH - 1.001f);
            var x0 = (int)x;
            var y0 = (int)y;
            var fx = x - x0;
            var fy = y - y0;
            var index = y0 * MaskW + x0;

            var v00 = field[index];
            var v10 = field[index + 1];
            var v01 = field[index + MaskW];
            var v11 = field[index + MaskW + 1];
            return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
        }

        Vector2 FieldGradient(Vector2 point)
        {
            const float d = 1.5f;
            var gx = SampleField(point + new Vector2(d, 0f)) - SampleField(point - new Vector2(d, 0f));
            var gy = SampleField(point + new Vector2(0f, d)) - SampleField(point - new Vector2(0f, d));
            return new Vector2(gx, gy) / (2f * d);
        }

        Vector2 NormalizedToMaskPx(Vector2 normalized)
        {
            return new Vector2(
                Mathf.Clamp01(normalized.x) * (MaskW - 1),
                (1f - Mathf.Clamp01(normalized.y)) * (MaskH - 1));
        }

        Vector2 MaskPxToNormalized(Vector2 maskPx)
        {
            return new Vector2(
                Mathf.Clamp01(maskPx.x / Mathf.Max(1f, MaskW - 1)),
                1f - Mathf.Clamp01(maskPx.y / Mathf.Max(1f, MaskH - 1)));
        }

        Vector3 MaskToWorld(Vector2 maskPx)
        {
            return mapper.ToWorld(MaskPxToNormalized(maskPx));
        }

        static bool IsPointInsidePolygon(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                if ((pi.y > point.y) != (pj.y > point.y) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / Mathf.Max(0.000001f, pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
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

        sealed class Particle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float SpeedScale;
            public Vector2 Jitter;
            public float JitterTimer;
            public Color BodyColor;
            public Transform Root;
            public SpriteRenderer GlowRenderer;
            public SpriteRenderer BodyRenderer;
            public SpriteRenderer HeadRenderer;
            public bool IsClimbing;
            public bool IsBloomAttached;
            public PlantModel AttachedPlant;

            // 花のはじけ(A)で追加した状態。
            // BurstFreeTimer > 0 の間は「自由飛散状態」(操舵されず慣性+分離力だけで飛ぶ)。
            // ReattachCooldown > 0 の間は、はじけた本人がどの花にも再吸着できない(粒ごとのクールダウン)。
            public float BurstFreeTimer;
            public float ReattachCooldown;
        }

        enum PlantStage
        {
            Seedling,
            Growing,
            Blooming,
            Wilting,
            Dead
        }

        sealed class PlantModel
        {
            readonly float seedlingDuration;
            readonly float growingDuration;
            readonly float wiltingStartDelay;
            readonly float wiltingDuration;
            readonly float maxHeightPx;

            public PlantModel(
                int id,
                Vector2 position,
                float seedlingDuration,
                float growingDuration,
                float wiltingStartDelay,
                float wiltingDuration,
                float maxHeightPx)
            {
                Id = id;
                Position = position;
                this.seedlingDuration = Mathf.Max(0.01f, seedlingDuration);
                this.growingDuration = Mathf.Max(0.01f, growingDuration);
                this.wiltingStartDelay = Mathf.Max(0f, wiltingStartDelay);
                this.wiltingDuration = Mathf.Max(0.01f, wiltingDuration);
                this.maxHeightPx = Mathf.Max(1f, maxHeightPx);
            }

            public int Id { get; }
            public Vector2 Position { get; }
            public float AgeSeconds { get; private set; }
            public float SecondsSinceRain { get; private set; }
            public float HeightPx { get; private set; }
            public Vector2 BloomPosition => new(Position.x, Position.y + HeightPx);
            public bool IsBloomable => CurrentStage is PlantStage.Blooming or PlantStage.Wilting;

            // 花のはじけ(A)の発火判定(立ち上がりエッジ検出)用。
            // 前フレームの時点で「手の輪郭がbloomSphereRadius以内にあったか」を保持する。
            public bool HandTouchingBloom;

            public PlantStage CurrentStage
            {
                get
                {
                    if (AgeSeconds < seedlingDuration)
                    {
                        return PlantStage.Seedling;
                    }

                    if (AgeSeconds < seedlingDuration + growingDuration)
                    {
                        return PlantStage.Growing;
                    }

                    var drySeconds = SecondsSinceRain - wiltingStartDelay;
                    if (drySeconds <= 0f)
                    {
                        return PlantStage.Blooming;
                    }

                    return drySeconds < wiltingDuration ? PlantStage.Wilting : PlantStage.Dead;
                }
            }

            public float WiltProgress01 => CurrentStage == PlantStage.Wilting
                ? Mathf.Clamp01((SecondsSinceRain - wiltingStartDelay) / wiltingDuration)
                : CurrentStage == PlantStage.Dead ? 1f : 0f;

            public void ReceiveRain()
            {
                SecondsSinceRain = 0f;
            }

            public void Advance(float deltaTime)
            {
                AgeSeconds += Mathf.Max(0f, deltaTime);
                SecondsSinceRain += Mathf.Max(0f, deltaTime);

                var growEnd = seedlingDuration + growingDuration;
                var growProgress = Mathf.Clamp01(AgeSeconds / growEnd);
                var eased = 1f - (1f - growProgress) * (1f - growProgress);
                HeightPx = maxHeightPx * eased;
            }
        }

        sealed class PlantViewRuntime : MonoBehaviour
        {
            SpriteRenderer stemGlowRenderer;
            SpriteRenderer stemRenderer;
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
                float flowerSizePx)
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
