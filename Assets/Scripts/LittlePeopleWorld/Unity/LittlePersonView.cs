using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class LittlePersonView : MonoBehaviour
    {
        // ===== Step A-1 チューニング定数 =====
        // 速度のx成分がこれを超えたときだけ向きを更新する(小刻みな左右反転=パタつきを防ぐ)。
        // 単位は正規化座標系での速度(Velocity)。Wander時のジッター量より少し大きめに。
        const float FlipThreshold = 0.0025f;

        // 落下中の回転(ふらふら回る演出)の角速度と振幅。
        const float FallingTiltSpeed = 9f;
        const float FallingTiltDegrees = 24f;

        // 頭・体のオフセットは bodySize に対する比率で持つ(Step A-1: 固定値(0.12,0.16)を廃止)。
        const float HeadOffsetYRatio = 0.75f;
        const float HeadScaleRatio = 0.58f;
        const float BodyWidthRatio = 0.82f;   // 体をわずかに縦長にして「胴体」らしさを出す
        const float BodyHeightRatio = 1.05f;

        // ===== Step A-2 チューニング定数(手足) =====
        // 腕・脚の付け根位置(bodySize比率、Figureローカル)。
        // ArmBack/ArmFrontは同じ肩位置に重ねて配置し、sortingOrderの前後だけで奥/手前を表現する
        // (横向きシルエットの2本腕なので、左右ではなく前後の1組として扱う)。脚も同様。
        const float ArmOffsetXRatio = 0.30f;
        const float ArmOffsetYRatio = 0.28f;
        const float ArmWidthRatio = 0.22f;
        const float ArmLengthRatio = 0.55f;

        const float LegOffsetXRatio = 0.14f;
        const float LegOffsetYRatio = -0.38f;
        const float LegWidthRatio = 0.34f;
        const float LegLengthRatio = 0.72f;

        // 歩行位相はTime.time直結ではなく速度で積分する(止まった瞬間に振幅ごと0へ収束させ、
        // 歩く速さと脚の回転速度を自然に連動させるため)。
        const float StrideFrequency = 260f;
        const float WalkSwingDegrees = 32f;
        const float ArmSwingRatio = 0.7f;   // 腕は脚より小さい振幅・逆位相
        const float BounceHeightRatio = 0.06f; // 1歩ごとに2回弾む上下バウンス

        // ===== Step A-3 チューニング定数(状態ポーズ) =====
        // ClimbBar系: 両腕を上げてバーを掴む。脚は小振幅で交互に踏ん張る。
        const float ClimbArmDegrees = 155f;
        const float ClimbStompSpeed = 6f;
        const float ClimbStompDegrees = 10f;

        // Falling: 歩行スイングを上書きし、手足を高周波でバタつかせる。
        const float FallingFlailSpeed = 18f;
        const float FallingFlailDegrees = 55f;

        // Startled(Emotion): 縦に伸びる squash & stretch + 遷移した瞬間だけの一瞬ジャンプ。
        const float StartledBodyStretch = 1.18f;
        const float StartledHopDurationSeconds = 0.25f;
        const float StartledHopHeightRatio = 0.5f;

        // Curious(Emotion): 頭を進行方向(常に+x側。Figureの反転で左右とも自動的に正しい向きになる)へ傾ける。
        const float CuriousHeadTiltDegrees = 12f;
        const float CuriousHeadOffsetXRatio = 0.06f;

        // ===== 階層(Step A-0) =====
        // root (このコンポーネント) … 位置のみ。rotationは常にidentityに固定する。
        //   ├ Glow    … 反転・回転の影響を受けない別系統
        //   └ Figure  … 向き反転(scale.x = ±1)と、落下時の傾きだけを担当する中間ノード。
        //               Step A-2以降、腕・脚はすべてこの子として追加する。
        SpriteRenderer glowRenderer;
        Transform figureRoot;
        SpriteRenderer bodyRenderer;
        SpriteRenderer headRenderer;
        SpriteRenderer armBackRenderer;
        SpriteRenderer armFrontRenderer;
        SpriteRenderer legBackRenderer;
        SpriteRenderer legFrontRenderer;

        // View側だけで完結する状態(Domainは汚さない)
        float facing = 1f;     // +1: 右向き, -1: 左向き
        bool facingInitialized;
        float walkPhase;       // Step A-2: 歩行位相。Time.time直結ではなく速度で積分する。

        // Step A-3: Startledへの遷移検出用。Domainにジャンプ用の状態を足さずに済ませるための
        // View側だけのテクニック。
        LittlePersonEmotion previousEmotion = LittlePersonEmotion.Calm;
        float hopTimer;

        public void Initialize()
        {
            // Glowはroot直下(反転・回転の影響を受けない)
            glowRenderer = CreateRenderer("Glow", RuntimeSpriteFactory.Circle, -4, transform);

            // Figureは向き反転と落下時の傾きだけを担当する空の中間ノード
            var figureObject = new GameObject("Figure");
            figureObject.transform.SetParent(transform, false);
            figureRoot = figureObject.transform;

            bodyRenderer = CreateRenderer("Body", RuntimeSpriteFactory.Circle, 10, figureRoot);
            headRenderer = CreateRenderer("Head", RuntimeSpriteFactory.Circle, 11, figureRoot);
            // 頭の位置・スケールはbodySizeに依存するため、固定値はここでは設定せずRender()側で毎フレーム設定する。

            // Step A-2: 手足。Capsule(pivot上端)を使い、付け根(肩/股関節)を支点に
            // localRotationだけで振り子のように振らせる。奥/手前はsortingOrderで表現。
            armBackRenderer = CreateRenderer("Arm Back", RuntimeSpriteFactory.Capsule, 9, figureRoot);
            legBackRenderer = CreateRenderer("Leg Back", RuntimeSpriteFactory.Capsule, 9, figureRoot);
            legFrontRenderer = CreateRenderer("Leg Front", RuntimeSpriteFactory.Capsule, 10, figureRoot);
            armFrontRenderer = CreateRenderer("Arm Front", RuntimeSpriteFactory.Capsule, 12, figureRoot);
        }

        public void Render(LittlePerson person, LittlePersonArchetypeMaster archetype, NormalizedScreenMapper mapper)
        {
            // root: 位置のみ。回転は常に無回転(Step A-1: 進行方向への全身回転を廃止)。
            transform.position = mapper.ToWorld(person.Position);
            transform.rotation = Quaternion.identity;

            var bodySize = mapper.ToWorldRadius(archetype.Size);

            UpdateFacing(person);
            UpdateFigureTransform(person);
            UpdateWalkPhase(person, archetype);
            UpdateHopTimer(person);

            // 歩行スイング(0〜1に正規化した速度比 swingAmount で振幅を絞る。止まると自然に0へ)。
            var speed = person.Velocity.magnitude;
            var swingAmount = Mathf.Clamp01(speed / archetype.MoveSpeed);
            var walkLegSwing = Mathf.Sin(walkPhase) * WalkSwingDegrees * swingAmount;
            var bounce = Mathf.Abs(Mathf.Sin(walkPhase)) * bodySize * BounceHeightRatio * swingAmount;

            var pose = SelectPose(person, walkLegSwing);

            // ホップ(Startled遷移時の一瞬ジャンプ)はFigure全体の上下位置に加算する。
            var hopProgress = 1f - hopTimer / StartledHopDurationSeconds;
            var hopOffset = hopTimer > 0f ? Mathf.Sin(hopProgress * Mathf.PI) * bodySize * StartledHopHeightRatio : 0f;
            figureRoot.localPosition = new Vector3(0f, hopOffset, 0f);

            // 体・頭のスケールと位置(bodySize比率で統一)。バウンス+squash&stretchを反映。
            bodyRenderer.transform.localPosition = new Vector3(0f, bounce, 0f);
            bodyRenderer.transform.localScale = new Vector3(
                bodySize * BodyWidthRatio / pose.BodyStretch,
                bodySize * BodyHeightRatio * pose.BodyStretch,
                1f);

            headRenderer.transform.localPosition = new Vector3(
                bodySize * pose.HeadOffsetXRatio,
                bodySize * HeadOffsetYRatio * pose.BodyStretch + bounce,
                0f);
            headRenderer.transform.localScale = Vector3.one * bodySize * HeadScaleRatio;
            headRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, pose.HeadTiltDeg);

            // 手足の付け根位置とスケール(Step A-2)。ArmBack/ArmFrontは同じ肩位置、
            // LegBack/LegFrontも同じ股関節位置に重ね、前後の重なりはsortingOrderで表現する。
            var armAnchor = new Vector3(bodySize * ArmOffsetXRatio, bodySize * ArmOffsetYRatio, 0f);
            var legAnchor = new Vector3(bodySize * LegOffsetXRatio, bodySize * LegOffsetYRatio, 0f);
            var armScale = new Vector3(bodySize * ArmWidthRatio, bodySize * ArmLengthRatio, 1f);
            var legScale = new Vector3(bodySize * LegWidthRatio, bodySize * LegLengthRatio, 1f);

            armBackRenderer.transform.localPosition = armAnchor;
            armFrontRenderer.transform.localPosition = armAnchor;
            legBackRenderer.transform.localPosition = legAnchor;
            legFrontRenderer.transform.localPosition = legAnchor;

            armBackRenderer.transform.localScale = armScale;
            armFrontRenderer.transform.localScale = armScale;
            legBackRenderer.transform.localScale = legScale;
            legFrontRenderer.transform.localScale = legScale;

            // Pose.OverrideWalkSwingがtrueなら通常の歩行スイングをポーズの角度で完全に置き換える
            // (ClimbBar系の踏ん張り、Fallingのバタつき)。falseなら通常どおり歩行スイングを使う。
            legFrontRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, pose.LegFrontDeg);
            legBackRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, pose.LegBackDeg);
            armFrontRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, pose.ArmFrontDeg);
            armBackRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, pose.ArmBackDeg);

            // Glowはroot直下のまま、位置はrootに追従(localPositionはゼロで良い)
            glowRenderer.transform.localPosition = Vector3.zero;
            glowRenderer.transform.localScale = Vector3.one * bodySize * GlowScale(person);

            var bodyColor = archetype.BodyColor;
            bodyRenderer.color = bodyColor;
            headRenderer.color = Color.Lerp(bodyColor, Color.white, 0.35f);

            var limbColor = Color.Lerp(bodyColor, Color.black, 0.12f);
            armBackRenderer.color = limbColor;
            armFrontRenderer.color = limbColor;
            legBackRenderer.color = limbColor;
            legFrontRenderer.color = limbColor;

            var glowColor = GlowColor(person, bodyColor);
            glowColor.a = GlowAlpha(person);
            glowRenderer.color = glowColor;

            previousEmotion = person.Emotion;
        }

        // 状態(CurrentBehavior/Emotion)から、その1フレームの手足・頭・体の見た目パラメータを
        // まとめて決定する。角度を個別に上書きすると分岐が散らばるため、構造体で一括して返す。
        readonly struct Pose
        {
            public readonly float ArmFrontDeg;
            public readonly float ArmBackDeg;
            public readonly float LegFrontDeg;
            public readonly float LegBackDeg;
            public readonly float BodyStretch;
            public readonly float HeadTiltDeg;
            public readonly float HeadOffsetXRatio;

            public Pose(
                float armFrontDeg,
                float armBackDeg,
                float legFrontDeg,
                float legBackDeg,
                float bodyStretch,
                float headTiltDeg,
                float headOffsetXRatio)
            {
                ArmFrontDeg = armFrontDeg;
                ArmBackDeg = armBackDeg;
                LegFrontDeg = legFrontDeg;
                LegBackDeg = legBackDeg;
                BodyStretch = bodyStretch;
                HeadTiltDeg = headTiltDeg;
                HeadOffsetXRatio = headOffsetXRatio;
            }
        }

        Pose SelectPose(LittlePerson person, float walkLegSwing)
        {
            switch (person.CurrentBehavior)
            {
                case LittlePersonBehaviorKind.ClimbBar:
                case LittlePersonBehaviorKind.TransferToSurface:
                case LittlePersonBehaviorKind.SurfaceWalk:
                case LittlePersonBehaviorKind.RideSurface:
                {
                    // 両腕を上げてバーを掴む。脚は小振幅で交互に踏ん張る(歩行スイングは無視)。
                    var stomp = Mathf.Sin(Time.time * ClimbStompSpeed + person.PreferenceSeed) * ClimbStompDegrees;
                    return new Pose(
                        armFrontDeg: ClimbArmDegrees,
                        armBackDeg: -ClimbArmDegrees,
                        legFrontDeg: stomp,
                        legBackDeg: -stomp,
                        bodyStretch: 1f,
                        headTiltDeg: 0f,
                        headOffsetXRatio: 0f);
                }

                case LittlePersonBehaviorKind.Falling:
                {
                    // 手足バタバタ。歩行と同じsinだが高周波・大振幅にして歩行スイングを完全に上書き。
                    var flail = Mathf.Sin(Time.time * FallingFlailSpeed + person.PreferenceSeed) * FallingFlailDegrees;
                    return new Pose(
                        armFrontDeg: -flail,
                        armBackDeg: flail,
                        legFrontDeg: flail,
                        legBackDeg: -flail,
                        bodyStretch: 1f,
                        headTiltDeg: 0f,
                        headOffsetXRatio: 0f);
                }
            }

            // 通常飛行/歩行時は歩行スイングをそのまま使い、Emotionに応じた頭・体の演出だけ加える。
            var bodyStretchByEmotion = person.Emotion == LittlePersonEmotion.Startled ? StartledBodyStretch : 1f;
            var headTiltByEmotion = person.Emotion == LittlePersonEmotion.Curious ? CuriousHeadTiltDegrees : 0f;
            var headOffsetByEmotion = person.Emotion == LittlePersonEmotion.Curious ? CuriousHeadOffsetXRatio : 0f;

            return new Pose(
                armFrontDeg: -walkLegSwing * ArmSwingRatio,
                armBackDeg: walkLegSwing * ArmSwingRatio,
                legFrontDeg: walkLegSwing,
                legBackDeg: -walkLegSwing,
                bodyStretch: bodyStretchByEmotion,
                headTiltDeg: headTiltByEmotion,
                headOffsetXRatio: headOffsetByEmotion);
        }

        // Emotionが Calm/Curious → Startled に変わった瞬間だけhopTimerをセットし、
        // タイマー中は放物線状のジャンプオフセットをRender()側で加算する。
        void UpdateHopTimer(LittlePerson person)
        {
            if (previousEmotion != LittlePersonEmotion.Startled && person.Emotion == LittlePersonEmotion.Startled)
            {
                hopTimer = StartledHopDurationSeconds;
            }
            else if (hopTimer > 0f)
            {
                hopTimer = Mathf.Max(0f, hopTimer - Time.deltaTime);
            }
        }

        // 歩行位相をTime.time直結ではなく速度で積分する。
        // 理由: 止まった瞬間に脚が中途半端な角度で凍らず、振幅(swingAmount)ごと0に収束する。
        // また歩く速さと脚の回転速度が自然に連動する。
        void UpdateWalkPhase(LittlePerson person, LittlePersonArchetypeMaster archetype)
        {
            var speed = person.Velocity.magnitude;
            walkPhase += speed * StrideFrequency * Time.deltaTime;
        }

        // 速度のx成分にヒステリシスを設けて向きを更新する。
        // 小さい閾値なしだと、Wander中の微小な速度反転で毎フレーム左右がパタつく。
        void UpdateFacing(LittlePerson person)
        {
            var vx = person.Velocity.x;
            if (!facingInitialized)
            {
                // 初回だけは閾値なしで確定させる(0のままだと反転しない可能性があるため)
                facing = vx >= 0f ? 1f : -1f;
                facingInitialized = true;
                return;
            }

            if (Mathf.Abs(vx) > FlipThreshold)
            {
                facing = vx >= 0f ? 1f : -1f;
            }
            // 閾値以下のときは前フレームの向きを維持する
        }

        // Figureの反転(scale.x)と、落下時のみの傾き(rotation)を設定する。
        // rootではなくFigureだけを回転させるのは、Glowや将来のUIなどroot直下の
        // 他要素に回転を伝播させないため。
        void UpdateFigureTransform(LittlePerson person)
        {
            figureRoot.localScale = new Vector3(facing, 1f, 1f);

            var tilt = 0f;
            if (person.CurrentBehavior == LittlePersonBehaviorKind.Falling)
            {
                tilt = Mathf.Sin(Time.time * FallingTiltSpeed) * FallingTiltDegrees;
            }

            figureRoot.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        SpriteRenderer CreateRenderer(string name, Sprite sprite, int sortingOrder, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
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
