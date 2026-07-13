using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed partial class WorldSpaceMaskAnimationController
    {
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
            var leftWing = CreateRenderer(root, "Left Wing", RuntimeSpriteFactory.Circle, 9);
            var rightWing = CreateRenderer(root, "Right Wing", RuntimeSpriteFactory.Circle, 9);
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
            var wingColor = Color.Lerp(color, Color.white, 0.7f);
            wingColor.a = particleWingAlpha;
            leftWing.color = wingColor;
            rightWing.color = wingColor;

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
                LeftWingRenderer = leftWing,
                RightWingRenderer = rightWing,
                BodyRenderer = body,
                HeadRenderer = head
            };
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
                var touchRadius = Mathf.Max(1f, plantFlowerSizePx * flowerBurstTouchRadiusRatio);
                var isTouchingNow = IsHandContourNearBloom(objects, bloomPos, touchRadius);

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

        // 指定した花に現在吸い付いている粒を、花の中心から放射状に弾き飛ばす。
        // はじけた粒は一定時間「自由飛散状態」(BurstFreeTimer > 0)となり、操舵されず慣性で飛ぶ。
        // また一定時間(ReattachCooldown)は、はじけた本人はどの花にも再吸着できない。
        void BurstPlant(PlantModel plant, Vector2 bloomPos)
        {
            var burstCount = 0;
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
                burstCount++;
            }

            if (burstCount > 0)
            {
                FlowerBurstSequence++;
            }
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
            public SpriteRenderer LeftWingRenderer;
            public SpriteRenderer RightWingRenderer;
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
    }
}
