using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;
using LittlePeopleWorld.Unity;
using UnityEngine;

/// <summary>
/// 深度画像(ここでは2値マスク)の形に沿って粒が動くデモ。
///
/// - 左ドラッグ    : ペンで描く(白)
/// - 右ドラッグ    : その場で消しゴム(一時的)
/// - E            : ペン/消しゴムのモード切替(左クリックの動作が変わる)
/// - [ / ] または ホイール : ブラシサイズ変更
/// - C            : 全消去
///
/// 挙動:
/// - マスクが空(または有効な塊が無い)とき : 粒は画面の縁を周回し続ける
/// - 有効な白い形があるとき             : 各粒が「自分の近くで最も白が密集している場所」を
///                                        個別に見つけて向かい、近づいたら輪郭に沿って流れる
/// - 常に                              : 近くの粒同士は分離力で押し合い、同じ場所に固まらない
///
/// ノイズ対策(小さい点の無視):
/// マスクが変わるたびに「繋がった白の塊(連結成分)」を検出し、ピクセル数が
/// minBlobPixels 未満の小さな塊は無効として扱う(fieldにもcellCountsにも反映しない)。
/// これにより、単発クリックのような小さいノイズ点には一切反応しなくなる。
///
/// 近傍優先(グローバルな1点ではない):
/// 各粒は画面全体で最大の1点を目指すのではなく、粗いグリッド(CellsX×CellsY)の中から
/// 自分に最も近い「有効な(＝十分な大きさの)密集セル」を探して向かう。従って、離れた場所に
/// 複数のクラスターがあれば、粒はそれぞれ近い方へ別々に集まる。
///
/// 見た目:
/// LittlePeopleWorld の LittlePersonView と同じ「glow(発光ハロー) + body(本体) + head(ハイライト)」
/// の3層構造を採用。色は複数パレットからランダムに割り当てる。
///
/// 雲・星との接触エフェクト:
/// LittlePeopleWorld の AmbientObject / VisualEffectInstance / AmbientObjectView / VisualEffectView /
/// MasterDatabase をそのまま再利用する。判定は AmbientObject.IsTouchedBy(Vector2) という
/// 汎用オーバーロードを新設し(要 DomainModels.cs 側の修正)、粒の位置をそのまま渡すだけにしている。
/// これにより、雲・星まわりの見た目や雨/光エフェクトのロジックを一切複製せずに済む。
///
/// 雨→植物→粒の相互作用:
/// 雲が反応中(雨が降っている)の間、雲の高さから地面までの到達時間ごとに着地イベントが発生する
/// (UpdateRainLanding)。着地位置の近くに既存の植物があれば成長(タイマーリセット)、なければ
/// maxPlants の上限内で新規生成する(Plant / PlantView、別ファイル)。
/// 粒は植物に近づくと根元へ引き寄せられ→登り(垂直方向は落下しない)→花が咲いていれば
/// 中心に吸い付いてホバーする(分離力で押し出されると自然に解除される)、という3段階の
/// 操舵をマスク追従より優先して行う。
///
/// 空のGameObjectに付けるだけで動く(アセット不要 / 2Dシーン / Main Camera必須。
/// ただし LittlePeopleWorld.Domain / .Master / .Unity の各スクリプトと、
/// 同じプロジェクト内の Plant.cs / PlantView.cs が必要)。
/// </summary>
public class DepthMaskContourParticles : MonoBehaviour
{
    // ---- マスク解像度(深度画像に相当) ----
    const int MaskW = 256;
    const int MaskH = 144;
    const int CellsX = 16;   // 粗い占有グリッド(粒が近くの塊を探すために使う)
    const int CellsY = 9;

    [Header("粒")]
    [SerializeField] int particleCount = 160;
    [SerializeField] float particleSize = 0.10f;   // body の直径(ワールド単位)
    [SerializeField] float speedPxPerSec = 55f;    // マスクpx単位の速度
    [SerializeField] float steerLerp = 6f;         // 操舵の効きの強さ

    [Header("粒の見た目(LittlePersonViewと同じ3層構造)")]
    [SerializeField] Color[] particlePalette = new[]
    {
        new Color(1.00f, 0.85f, 0.25f), // 黄
        new Color(1.00f, 0.45f, 0.85f), // ピンク/マゼンタ
        new Color(0.45f, 0.95f, 0.85f), // 水色/ミント
        new Color(0.75f, 0.55f, 1.00f), // 紫
        new Color(1.00f, 1.00f, 1.00f), // 白
    };

    [Header("固まり防止(分離力)")]
    [SerializeField] float separationRadiusPx = 8f;
    [SerializeField] float separationGain = 380f;
    [SerializeField] float targetJitterPx = 9f;

    [Header("小さい点(ノイズ)の無視")]
    [Tooltip("繋がった白ピクセル数がこの値未満の塊は「存在しない」ものとして無視する。" +
             "小さいペン跡やノイズ点を無効化するためのフィルタ。")]
    [SerializeField] int minBlobPixels = 40;

    [Header("輪郭追従")]
    [SerializeField] int blurRadius = 4;
    [SerializeField] float contourLevel = 0.5f;
    [SerializeField] float correctionGain = 5f;
    [SerializeField] float senseThreshold = 0.03f;
    [Tooltip("粒がペンの塊に反応する距離の絶対上限(マスクpx)。塊がどれだけ大きくても、これより遠くには反応しない。")]
    [SerializeField] float penSenseRadiusPx = 70f;
    [Tooltip("ペンの塊の白ピクセル数に応じた反応距離の下限(マスクpx)。小さい塊は、これくらいの距離までしか反応しない。")]
    [SerializeField] float minPenAttractRadiusPx = 18f;
    [Tooltip("塊のピクセル数の平方根 × この値だけ、反応距離が下限に加算される。大きい塊ほど遠くからでも反応するようになる。")]
    [SerializeField] float penAttractRadiusPerSqrtPixel = 2.5f;

    [Header("縁の周回")]
    [SerializeField] float edgeMarginPx = 7f;
    [SerializeField] float edgePullGain = 0.12f;

    [Header("ペン")]
    [SerializeField] int brushRadius = 6;
    [SerializeField] int brushRadiusMin = 2;
    [SerializeField] int brushRadiusMax = 40;
    [SerializeField] int brushRadiusStep = 2;

    [Header("雲・星との接触エフェクト(既存のLittlePeopleWorld資産を再利用)")]
    [SerializeField] bool enableAmbientEffects = true;
    [Tooltip("雲が画面の端からどれだけ離れた場所でバウンドするか(normalized、0〜0.5程度)。" +
             "AmbientObject側のデフォルトは0(見た目の端が画面端ぎりぎりまで到達する)なので、ここで余白を追加する。")]
    [SerializeField] float cloudEdgePadding = 0.08f;
    [Tooltip("雲の中心Y座標がこれを超えて下に行かないようにする(normalized、0=画面上端、1=画面下端)。" +
             "0.5にすると、雲は画面の下半分には入らなくなる。")]
    [SerializeField] float cloudMaxCenterY = 0.5f;

    [Header("雨の落下(地面に着くまで消えない)")]
    [Tooltip("雨が降っている間、この速度(マスクpx/秒)で雲の位置から地面へ向けて降り続けていると仮定する。" +
             "雲の高さから地面までの到達時間ごとに、着地イベント(OnRainLanded)を発生させる。")]
    [SerializeField] float rainFallSpeedPxPerSec = 40f;
    [Tooltip("地面とみなすY座標(マスクpx、y上向きなので0に近いほど画面下)。")]
    [SerializeField] float groundYPx = 3f;
    [Tooltip("計算された雨の落下時間に、この倍数を掛ける。1.0=計算通り、2.0=2倍長く表示される。")]
    [SerializeField] float rainDurationMultiplier = 1.0f;
    [Tooltip("雨粒の横方向の太さ・広がりの倍率。1.0=既定、0.5=半分の太さ。")]
    [SerializeField] float rainDropWidthScale = 1.0f;
    [Tooltip("雨粒が降る(ループする)アニメーション速度。既定は7.5。小さくするほどゆっくり降る。")]
    [SerializeField] float rainVisualPulseSpeed = 7.5f;
    [Tooltip("雨粒1粒あたりの大きさの倍率。1.0=既定、2.0=2倍の大きさ。")]
    [SerializeField] float rainDropSizeScale = 1.0f;

    [Header("植物システム")]
    [SerializeField] int maxPlants = 10;
    [Tooltip("植物が最大まで育ったときの茎の高さ(マスクpx)。他の半径は全てこの値に対する比率で決まる。")]
    [SerializeField] float plantMaxHeightPx = 46f;
    [Tooltip("雨の着地点がここに指定した比率×(植物の現在の高さ)以内なら、新規生成せず既存の植物を成長させる。" +
             "植物の『現在のサイズ』に応じて柔軟に変わる(育つほど、まとめて受け止める範囲も広がる)。")]
    [SerializeField] float plantSpawnMergingRadiusRatio = 0.5f;
    [Tooltip("粒が植物の根元へ向かい始める範囲(植物の現在の高さ × この値)。")]
    [SerializeField] float plantInfluenceRadiusRatio = 0.8f;
    [Tooltip("粒が『登り』状態に入る範囲(植物の現在の高さ × この値)。")]
    [SerializeField] float plantClimbAttractRadiusRatio = 0.3f;

    [Header("植物の見た目段階(秒)")]
    [SerializeField] float plantSeedlingDuration = 10f;
    [SerializeField] float plantGrowingDuration = 10f;
    [Tooltip("開花後、雨が止んでからこの秒数はBloomingを維持する。")]
    [SerializeField] float plantWiltingStartDelay = 5f;
    [Tooltip("Wilting開始からDead(消滅)までの秒数。")]
    [SerializeField] float plantWiltingDuration = 15f;

    [Header("粒と花の吸い付き")]
    [Tooltip("花に吸い付き始める範囲(植物の現在の高さ × この値)。")]
    [SerializeField] float bloomAttractRadiusRatio = 0.5f;
    [Tooltip("吸い付いた粒が留まれる範囲(植物の現在の高さ × この値)。これを超えて離れると吸い付き解除。")]
    [SerializeField] float bloomSphereRadiusRatio = 1.0f;
    [Tooltip("花の中心へ引き寄せる力の強さ。")]
    [SerializeField] float bloomAttractForce = 300f;

    [Header("花のはじけ(ペンが花に触れると吸い付いた粒が飛散)")]
    [Tooltip("花の吸い付き範囲内でペン/消しゴムにより変化(白化・黒化)したピクセルが、" +
             "この数以上あったときだけ「はじけ」が発火する(ノイズ対策)。")]
    [SerializeField] int burstMinChangedPixels = 8;
    [Tooltip("はじけたときの粒の初速(マスクpx/秒)。通常速度の数倍が目安。")]
    [SerializeField] float burstInitialSpeedPxPerSec = 220f;
    [Tooltip("はじけた粒が『自由飛散状態』を保つ時間(秒)。この間は操舵されず、初速のまま慣性で飛ぶ。")]
    [SerializeField] float burstFreeSeconds = 1.5f;
    [Tooltip("はじけた粒が再び花に吸い付けるようになるまでの時間(秒)。")]
    [SerializeField] float burstReattachCooldownSeconds = 2.0f;

    // ---- 雲・星・エフェクト(LittlePeopleWorldの既存クラスをそのまま利用) ----
    MasterDatabase ambientMasters;
    TuningParameterMaster tuning;
    NormalizedScreenMapper mapper;
    Transform ambientRoot;
    Transform effectsRoot;
    readonly List<AmbientObject> ambientObjects = new List<AmbientObject>();
    readonly List<AmbientObjectView> ambientObjectViews = new List<AmbientObjectView>();
    readonly List<VisualEffectInstance> visualEffects = new List<VisualEffectInstance>();
    readonly Dictionary<int, VisualEffectView> visualEffectViews = new Dictionary<int, VisualEffectView>();
    int nextVisualEffectId = 1;

    // 雨(RainColumn)専用に PulseSpeed(速度)だけを上書きしたコピー。
    // 元の VisualEffectMaster は他のオブジェクトとも共有されるデータなので、直接書き換えずコピーを使う。
    VisualEffectMaster customRainMaster;

    // ---- 植物(雨が地面に着いたら生成/成長) ----
    Transform plantsRoot;
    readonly List<Plant> plants = new List<Plant>();
    readonly Dictionary<int, PlantView> plantViews = new Dictionary<int, PlantView>();
    int nextPlantId = 1;

    // 雲ごとに「雨が降り続けている時間」と「これまでに着地させた雨粒の数」を追う。
    // 雲の高さ→地面までの到達時間ごとに1回、OnRainLandedを発生させるためのタイマー。
    readonly Dictionary<int, float> rainActiveSeconds = new Dictionary<int, float>();
    readonly Dictionary<int, int> rainLandedCounts = new Dictionary<int, int>();

    // ---- マスクと場 ----
    bool[] mask;            // 生の(フィルタ前の)マスク。描画/消しゴムで直接編集される
    bool[] effectiveMask;   // minBlobPixels 以上の塊だけを残したマスク。粒の判断は全てこちらを使う
    bool[] visited;         // 連結成分探索の作業バッファ
    int[] componentSizeMap; // 各有効ピクセルが属する連結成分の総ピクセル数(サイズに応じた反応距離の計算に使う)
    float[] field;
    float[] fieldTmp;
    Color32[] pixels;
    Texture2D maskTex;
    bool maskDirty;
    int effectiveWhiteCount; // 有効な(十分大きい)白ピクセルの総数
    readonly int[] cellCounts = new int[CellsX * CellsY]; // 有効マスクだけで集計した粗い密度グリッド
    readonly int[] cellMaxComponentSize = new int[CellsX * CellsY]; // 各セル内にある塊の最大サイズ(ピクセル数)

    // 連結成分探索用の使い回しバッファ(GC負荷を避けるため毎回確保しない)
    readonly List<int> floodStack = new List<int>();
    readonly List<int> componentBuffer = new List<int>();

    // このフレームでペン/消しゴムにより変化(白化 or 黒化)したピクセルのマスクpx座標。
    // 花のはじけ判定に使い、Updateの最後にクリアする。
    readonly List<Vector2> changedPixelsThisFrame = new List<Vector2>();

    // ---- 共有スプライト(RuntimeSpriteFactory.Circle と同じ生成方法。証明済みの丸スプライト) ----
    static Sprite circleSprite;
    static Sprite Circle => circleSprite != null ? circleSprite : (circleSprite = CreateCircleSprite(64));

    // ---- 粒 ----
    class Particle
    {
        public Vector2 pos;          // マスクpx座標 (原点左下, y上向き)
        public Vector2 vel;
        public float speedScale;
        public Vector2 jitter;
        public float jitterTimer;
        public Color bodyColor;

        // ---- 植物との相互作用状態 ----
        public bool isClimbing;       // 植物を登っている最中か
        public bool isBloomAttached;  // 花に吸い付いているか
        public Plant attachedPlant;   // 登行中/吸い付き中の対象の植物(どちらでもなければnull)

        // ---- はじけ(バースト)状態 ----
        public float burstFreeTimer;      // >0 の間は「自由飛散状態」(操舵されず慣性で飛ぶ)
        public float reattachCooldown;    // >0 の間は花に吸い付けない(再吸着禁止)

        public Transform root;
        public SpriteRenderer glowRenderer;
        public SpriteRenderer bodyRenderer;
        public SpriteRenderer headRenderer;
    }
    readonly List<Particle> particles = new List<Particle>();

    // ---- ペン状態 ----
    bool eraseMode = false;
    GameObject brushCursorGo;
    SpriteRenderer brushCursorSr;

    Camera cam;
    Vector2 lastPaintPos;
    static readonly Color32 ColWhite = new Color32(255, 255, 255, 255);
    static readonly Color32 ColBlack = new Color32(8, 8, 10, 255);
    GUIStyle hudStyle;

    void Start()
    {
        cam = Camera.main;
        cam.backgroundColor = Color.black;

        mask = new bool[MaskW * MaskH];
        effectiveMask = new bool[MaskW * MaskH];
        visited = new bool[MaskW * MaskH];
        componentSizeMap = new int[MaskW * MaskH];
        field = new float[MaskW * MaskH];
        fieldTmp = new float[MaskW * MaskH];
        pixels = new Color32[MaskW * MaskH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = ColBlack;

        maskTex = new Texture2D(MaskW, MaskH, TextureFormat.RGBA32, false);
        maskTex.filterMode = FilterMode.Point;
        maskTex.SetPixels32(pixels);
        maskTex.Apply();

        CreateBackgroundSprite();
        CreateParticles();
        CreateBrushCursor();
        SetupAmbientEffects();
    }

    /// <summary>
    /// 既存の LittlePeopleWorld 資産(MasterDatabase / AmbientObject / VisualEffectInstance /
    /// AmbientObjectView / VisualEffectView)を使って、雲と星をこのデモ内にも用意する。
    /// </summary>
    void SetupAmbientEffects()
    {
        if (!enableAmbientEffects)
        {
            return;
        }

        ambientMasters = MasterDatabase.CreateDefault();
        tuning = ambientMasters.TuningParameters.Get(1);

        // 雲の VisualEffectMasterId(=雨)を取得し、PulseSpeedだけ差し替えたコピーを作る
        var cloudType = ambientMasters.GetAmbientObjectType(AmbientObjectKind.Cloud);
        var originalRainMaster = ambientMasters.VisualEffects.Get(cloudType.VisualEffectMasterId);
        customRainMaster = new VisualEffectMaster(
            originalRainMaster.Id,
            originalRainMaster.Kind,
            originalRainMaster.RenderMode,
            originalRainMaster.Name,
            originalRainMaster.Color,
            rainVisualPulseSpeed, // ← ここだけ差し替え(既定は7.5、小さくすると降る速度がゆっくりになる)
            originalRainMaster.Alpha,
            originalRainMaster.DefaultSize,
            originalRainMaster.DurationSeconds,
            originalRainMaster.AssetKey,
            rainDropSizeScale); // ← 雨粒1粒あたりの大きさの倍率

        // AmbientObjectView / VisualEffectView は normalized(0..1) 座標をワールド座標へ変換する
        // NormalizedScreenMapper を必要とする。背景スプライトと同じ計算式で worldHeight を合わせる。
        float worldH = cam.orthographicSize * 2f;
        mapper = new NormalizedScreenMapper(cam, worldH);

        ambientRoot = new GameObject("ambient_objects").transform;
        ambientRoot.SetParent(transform);
        effectsRoot = new GameObject("visual_effects").transform;
        effectsRoot.SetParent(transform);
        plantsRoot = new GameObject("plants").transform;
        plantsRoot.SetParent(transform);

        SpawnAmbientObjects();
    }

    void SpawnAmbientObjects()
    {
        int nextAmbientId = 1;

        var cloudType = ambientMasters.GetAmbientObjectType(AmbientObjectKind.Cloud);
        for (int i = 0; i < tuning.AmbientCloudCount; i++)
        {
            SpawnAmbientObject(ref nextAmbientId, AmbientObjectKind.Cloud, cloudType);
        }

        var starType = ambientMasters.GetAmbientObjectType(AmbientObjectKind.Star);
        for (int i = 0; i < tuning.AmbientStarCount; i++)
        {
            SpawnAmbientObject(ref nextAmbientId, AmbientObjectKind.Star, starType);
        }
    }

    void SpawnAmbientObject(ref int nextAmbientId, AmbientObjectKind kind, AmbientObjectTypeMaster typeMaster)
    {
        int index = ambientObjects.Count;
        var position = AmbientSpawnPosition(kind, index);
        var velocity = AmbientVelocity(typeMaster.DriftVelocity, index);

        // 雲だけ、画面端からの余白(cloudEdgePadding)と、下半分に行かせない制限(cloudMaxCenterY)を持たせる。
        // 星は既存のまま(制限なし)。
        float edgePadding = kind == AmbientObjectKind.Cloud ? cloudEdgePadding : 0f;
        float maxCenterY = kind == AmbientObjectKind.Cloud ? cloudMaxCenterY : 1f;

        var ambient = new AmbientObject(
            nextAmbientId++,
            kind,
            position,
            typeMaster.DefaultSize,
            velocity,
            typeMaster.ContactRadius,
            edgePadding,
            maxCenterY);
        ambientObjects.Add(ambient);

        var viewGo = new GameObject($"{kind}_{ambient.Id}");
        viewGo.transform.SetParent(ambientRoot, false);
        var view = viewGo.AddComponent<AmbientObjectView>();
        view.Initialize();
        ambientObjectViews.Add(view);
    }

    void CreateBackgroundSprite()
    {
        var go = new GameObject("mask_view");
        go.transform.SetParent(transform);
        var sr = go.AddComponent<SpriteRenderer>();
        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;
        float ppu = MaskH / worldH;
        sr.sprite = Sprite.Create(maskTex, new Rect(0, 0, MaskW, MaskH),
                                  new Vector2(0.5f, 0.5f), ppu);
        sr.sortingOrder = -10;
        float spriteW = MaskW / ppu;
        go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
        go.transform.localScale = new Vector3(worldW / spriteW, 1f, 1f);
    }

    void CreateParticles()
    {
        for (int i = 0; i < particleCount; i++)
        {
            var root = new GameObject("particle_" + i).transform;
            root.SetParent(transform);

            var glow = CreateRenderer(root, "Glow", -4);
            var body = CreateRenderer(root, "Body", 10);
            var head = CreateRenderer(root, "Head", 11);
            head.transform.localPosition = new Vector3(particleSize * 0.12f, particleSize * 0.16f, 0f);

            var color = particlePalette[Random.Range(0, particlePalette.Length)];
            body.transform.localScale = Vector3.one * particleSize;
            head.transform.localScale = Vector3.one * particleSize * 0.58f;
            body.color = color;
            head.color = Color.Lerp(color, Color.white, 0.35f);

            var p = new Particle
            {
                pos = new Vector2(Random.Range(0f, MaskW), Random.Range(0f, MaskH)),
                vel = Random.insideUnitCircle.normalized * speedPxPerSec,
                speedScale = Random.Range(0.75f, 1.35f),
                jitter = Random.insideUnitCircle * targetJitterPx,
                jitterTimer = Random.Range(0f, 2f),
                bodyColor = color,
                root = root,
                glowRenderer = glow,
                bodyRenderer = body,
                headRenderer = head,
            };
            particles.Add(p);
        }
    }

    SpriteRenderer CreateRenderer(Transform parent, string name, int sortingOrder)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    void CreateBrushCursor()
    {
        brushCursorGo = new GameObject("brush_cursor");
        brushCursorGo.transform.SetParent(transform);
        brushCursorSr = brushCursorGo.AddComponent<SpriteRenderer>();
        brushCursorSr.sprite = CreateRingSprite();
        brushCursorSr.sortingOrder = 5;
        brushCursorSr.color = new Color(1f, 1f, 1f, 0.8f);
    }

    void Update()
    {
        HandleModeKeys();
        HandlePainting();
        UpdateBrushCursor();

        if (maskDirty)
        {
            maskTex.SetPixels32(pixels);
            maskTex.Apply();
            RecomputeEffectiveMask(); // 小さい塊を除外した「有効マスク」を作り直す
            RebuildField();           // 有効マスクだけをぼかして field を作る
            RebuildCellCounts();      // 有効マスクだけで粗いグリッドを再集計する
            maskDirty = false;
        }

        float dt = Time.deltaTime;

        // ペンが花に触れて変化したピクセルがあれば、そのフレームのうちに「はじけ」を判定する
        // (粒の操舵より前に処理して、はじけた粒がこのフレームから自由飛散状態になるようにする)
        HandleFlowerBurst();

        var separation = new Vector2[particles.Count];
        ComputeSeparation(separation);

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            UpdateJitter(p, dt);
            UpdateParticle(p, separation[i], dt);

            p.root.position = MaskToWorld(p.pos);

            if (p.vel.sqrMagnitude > 1e-4f)
            {
                float angle = Mathf.Atan2(p.vel.y, p.vel.x) * Mathf.Rad2Deg;
                p.root.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 2.2f + i * 0.7f);
            var glowColor = Color.Lerp(p.bodyColor, Color.white, 0.5f);
            glowColor.a = pulse * 0.4f;
            p.glowRenderer.color = glowColor;
            p.glowRenderer.transform.localScale = Vector3.one * particleSize * (2.0f + pulse * 0.6f);
        }

        UpdateAmbientEffects(dt);

        // このフレームの変化ピクセルは、はじけ判定に使い終わったのでクリアする
        changedPixelsThisFrame.Clear();
    }

    // ================= 花のはじけ(ペンが花に触れると吸い付いた粒が飛散) =================

    /// <summary>
    /// このフレームでペン/消しゴムにより変化(白化・黒化)したピクセルを、各植物の花の
    /// 吸い付き範囲(bloomSphereRadius)と照合する。範囲内の変化ピクセルが burstMinChangedPixels
    /// 以上あった花については、その花に現在吸い付いている粒を放射状に弾き飛ばす。
    /// </summary>
    void HandleFlowerBurst()
    {
        if (changedPixelsThisFrame.Count == 0 || plants.Count == 0)
        {
            return;
        }

        foreach (var plant in plants)
        {
            if (!plant.IsBloomable)
            {
                continue;
            }

            Vector2 bloomPos = plant.BloomPosition;
            float bloomSphereRadius = Mathf.Max(4f, plant.Height * bloomSphereRadiusRatio);
            float radiusSq = bloomSphereRadius * bloomSphereRadius;

            // 花の範囲内で変化したピクセル数を数える
            int changedInRange = 0;
            for (int i = 0; i < changedPixelsThisFrame.Count; i++)
            {
                if ((changedPixelsThisFrame[i] - bloomPos).sqrMagnitude <= radiusSq)
                {
                    changedInRange++;
                    if (changedInRange >= burstMinChangedPixels)
                    {
                        break; // 発火に必要な数に達したら、それ以上数える必要はない
                    }
                }
            }

            if (changedInRange >= burstMinChangedPixels)
            {
                BurstPlant(plant, bloomPos);
            }
        }
    }

    /// <summary>
    /// 指定した花に現在吸い付いている粒を、花の中心から放射状に弾き飛ばす。
    /// はじけた粒は一定時間「自由飛散状態」となり、その間は操舵されず慣性で飛ぶ。
    /// また一定時間は花に再吸着できない(クールダウン)。
    /// </summary>
    void BurstPlant(Plant plant, Vector2 bloomPos)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (!p.isBloomAttached || p.attachedPlant != plant)
            {
                continue;
            }

            // 花の中心から粒への向き(放射状に外向き)。中心とほぼ重なっている場合はランダム方向。
            Vector2 dir = p.pos - bloomPos;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Random.insideUnitCircle.normalized;
            }
            else
            {
                dir = dir.normalized;
            }

            p.vel = dir * burstInitialSpeedPxPerSec;
            p.isBloomAttached = false;
            p.isClimbing = false;
            p.attachedPlant = null;
            p.burstFreeTimer = burstFreeSeconds;
            p.reattachCooldown = burstReattachCooldownSeconds;
        }
    }

    void UpdateAmbientEffects(float dt)
    {
        if (!enableAmbientEffects)
        {
            return;
        }

        foreach (var ambient in ambientObjects)
        {
            ambient.Advance(dt);
        }

        UpdateAmbientReactions();
        UpdateRainLanding(dt);   // 雨が地面に着くまでの時間をトラッキングし、着地したら植物を生成/成長させる
        UpdatePlants(dt);        // 植物の成長段階を時間経過で進める(開花・しおれ・消滅)
        AdvanceVisualEffects(dt);
        SyncAmbientViews();
        SyncVisualEffectViews();
    }

    void UpdateAmbientReactions()
    {
        foreach (var ambient in ambientObjects)
        {
            bool touched = IsTouchedByAnyParticle(ambient);
            var typeMaster = ambientMasters.GetAmbientObjectType(ambient.Kind);
            var effectMaster = ambientMasters.VisualEffects.Get(typeMaster.VisualEffectMasterId);

            if (ambient.Kind == AmbientObjectKind.Cloud)
            {
                if (touched)
                {
                    ambient.MarkCloudTouched(tuning.RainLingerSeconds);
                }

                if (ambient.IsReacting)
                {
                    // 雲から地面までの距離を計算する(表示時間・描画サイズの両方に使う)。
                    // 注意: groundYPx はマスクpx空間(y上向き)、ambient.Position は normalized空間(y下向き)
                    // なので、正しく変換してから引き算する必要がある(以前はここが誤っていた)。
                    float cloudHeightNormalized = ambient.Position.y;               // 0(画面上)〜1(画面下)
                    float groundHeightNormalized = 1f - (groundYPx / MaskH);        // マスクpx→normalizedへ正しく変換
                    float fallDistanceNormalized = Mathf.Max(0.01f, groundHeightNormalized - cloudHeightNormalized);

                    var rainPosition = ClampNormalized(ambient.Position + new Vector2(0f, ambient.Size.y * 0.45f));

                    // rainSize.y は「雨粒がループする縦方向の範囲」そのもの。
                    // 従来は effectMaster.DefaultSize.y(固定・小さい値)だったため、
                    // 雲のすぐ近くの狭い範囲でしか雨粒が描画されず、地面に届く前に見えなくなっていた。
                    // ここを実際の落下距離に合わせることで、雨粒が本当に地面まで届いて見えるようにする。
                    var rainSize = new Vector2(
                        Mathf.Max(effectMaster.DefaultSize.x, ambient.Size.x * 0.75f) * rainDropWidthScale,
                        fallDistanceNormalized);

                    float rainDurationSeconds = (fallDistanceNormalized * MaskH) / Mathf.Max(1f, rainFallSpeedPxPerSec);
                    rainDurationSeconds *= Mathf.Max(0.1f, rainDurationMultiplier);

                    RefreshOrCreateVisualEffect(
                        effectMaster.Id,
                        ambient.Id,
                        rainPosition,
                        rainSize,
                        0f,
                        rainDurationSeconds);
                }
            }
            else if (ambient.Kind == AmbientObjectKind.Star &&
                     touched &&
                     ambient.TryTriggerStar(effectMaster.DurationSeconds, tuning.StarCooldownSeconds))
            {
                RefreshOrCreateVisualEffect(
                    effectMaster.Id,
                    ambient.Id,
                    ambient.Position,
                    effectMaster.DefaultSize,
                    0f,
                    effectMaster.DurationSeconds);
            }
        }
    }

    /// <summary>
    /// このデモの粒(マスクpx座標)のいずれかが、雲/星に触れているかを調べる。
    /// AmbientObject.IsTouchedBy(Vector2) の汎用オーバーロードを使う(要 DomainModels.cs 側の追加)。
    /// </summary>
    bool IsTouchedByAnyParticle(AmbientObject ambient)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var normalizedPos = MaskPxToNormalized(particles[i].pos);
            if (ambient.IsTouchedBy(normalizedPos))
            {
                return true;
            }
        }
        return false;
    }

    void RefreshOrCreateVisualEffect(
        int visualEffectMasterId,
        int sourceObjectId,
        Vector2 position,
        Vector2 size,
        float angleDegrees,
        float durationSeconds)
    {
        foreach (var effect in visualEffects)
        {
            if (effect.VisualEffectMasterId == visualEffectMasterId && effect.SourceObjectId == sourceObjectId)
            {
                effect.Refresh(position, size, angleDegrees, durationSeconds);
                return;
            }
        }

        visualEffects.Add(new VisualEffectInstance(
            nextVisualEffectId++,
            visualEffectMasterId,
            sourceObjectId,
            position,
            size,
            angleDegrees,
            durationSeconds));
    }

    void AdvanceVisualEffects(float dt)
    {
        for (int i = visualEffects.Count - 1; i >= 0; i--)
        {
            visualEffects[i].Advance(dt);
            if (visualEffects[i].IsExpired)
            {
                visualEffects.RemoveAt(i);
            }
        }
    }

    void SyncAmbientViews()
    {
        for (int i = 0; i < ambientObjects.Count; i++)
        {
            var typeMaster = ambientMasters.GetAmbientObjectType(ambientObjects[i].Kind);
            // debugEnabled=false: 接触半径の可視化はこのデモでは常に非表示にしておく
            ambientObjectViews[i].Render(ambientObjects[i], typeMaster, mapper, false);
        }
    }

    void SyncVisualEffectViews()
    {
        var liveIds = new HashSet<int>();

        foreach (var effect in visualEffects)
        {
            liveIds.Add(effect.Id);
            if (!visualEffectViews.TryGetValue(effect.Id, out var view))
            {
                var viewGo = new GameObject($"effect_{effect.Id}");
                viewGo.transform.SetParent(effectsRoot, false);
                view = viewGo.AddComponent<VisualEffectView>();
                view.Initialize();
                visualEffectViews[effect.Id] = view;
            }

            // 雨(RainColumn)だけは PulseSpeed を差し替えたコピーを使う。それ以外(星など)は元のマスターのまま。
            var effectMaster = effect.VisualEffectMasterId == customRainMaster.Id
                ? customRainMaster
                : ambientMasters.VisualEffects.Get(effect.VisualEffectMasterId);
            view.Render(effect, effectMaster, mapper);
        }

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

    // ================= 雨の着地 → 植物の生成/成長 =================

    /// <summary>
    /// 雲が「反応中(雨が降っている)」の間、雲の高さから地面までの到達時間ごとに
    /// 1回ずつ OnRainLanded を発生させる。VisualEffectInstance 自体の内部タイマーには依存せず、
    /// 「雲が反応し続けている時間(=雨が降り続けている時間)」を自前でトラッキングすることで実現する。
    /// これにより「雨は地面に着くまで消えない(=着地は雲の高さ分の時間がかかる)」を表現しつつ、
    /// 雨が降り続く限り一定間隔で着地イベントが繰り返される。
    /// </summary>
    void UpdateRainLanding(float dt)
    {
        foreach (var ambient in ambientObjects)
        {
            if (ambient.Kind != AmbientObjectKind.Cloud)
            {
                continue;
            }

            rainActiveSeconds.TryGetValue(ambient.Id, out var activeSeconds);
            rainLandedCounts.TryGetValue(ambient.Id, out var landedCount);

            if (ambient.IsReacting)
            {
                activeSeconds += dt;

                var cloudMaskPos = NormalizedToMaskPx(ambient.Position);
                float fallDistance = Mathf.Max(1f, cloudMaskPos.y - groundYPx);
                float fallDuration = fallDistance / Mathf.Max(1f, rainFallSpeedPxPerSec);

                int dropsSoFar = Mathf.FloorToInt(activeSeconds / fallDuration);
                while (landedCount < dropsSoFar)
                {
                    landedCount++;
                    float cloudWidthPx = ambient.Size.x * MaskW;
                    float jitterX = Random.Range(-0.5f, 0.5f) * cloudWidthPx;
                    OnRainLanded(new Vector2(cloudMaskPos.x + jitterX, groundYPx));
                }
            }
            else
            {
                // 雨が止んだら、次に降り出したときにまた最初の一滴からやり直す
                activeSeconds = 0f;
                landedCount = 0;
            }

            rainActiveSeconds[ambient.Id] = activeSeconds;
            rainLandedCounts[ambient.Id] = landedCount;
        }
    }

    /// <summary>
    /// 雨が地面に着いたときの処理。近くに既存の植物があれば成長させ(ReceiveRain)、
    /// なければ maxPlants の上限内で新しい植物を生成する。上限に達している場合は何もしない。
    /// </summary>
    void OnRainLanded(Vector2 landingPositionPx)
    {
        var existing = FindMergeablePlant(landingPositionPx);
        if (existing != null)
        {
            existing.ReceiveRain();
            return;
        }

        if (plants.Count >= maxPlants)
        {
            return;
        }

        var plant = new Plant(
            nextPlantId++,
            new Vector2(landingPositionPx.x, groundYPx),
            plantSeedlingDuration,
            plantGrowingDuration,
            plantWiltingStartDelay,
            plantWiltingDuration,
            plantMaxHeightPx);
        plants.Add(plant);
    }

    /// <summary>
    /// 着地位置の近くに、成長を受け止められる既存の植物があれば返す。
    /// 判定半径は「その植物の現在の高さ × plantSpawnMergingRadiusRatio」で、育つほど広くなる。
    /// </summary>
    Plant FindMergeablePlant(Vector2 positionPx)
    {
        Plant best = null;
        float bestDist = float.MaxValue;

        foreach (var plant in plants)
        {
            if (plant.CurrentStage == Plant.Stage.Dead)
            {
                continue;
            }

            float mergeRadius = Mathf.Max(6f, plant.Height * plantSpawnMergingRadiusRatio);
            float d = Vector2.Distance(plant.Position, positionPx);
            if (d <= mergeRadius && d < bestDist)
            {
                best = plant;
                bestDist = d;
            }
        }

        return best;
    }

    void UpdatePlants(float dt)
    {
        for (int i = plants.Count - 1; i >= 0; i--)
        {
            plants[i].Advance(dt);
            if (plants[i].CurrentStage == Plant.Stage.Dead)
            {
                plants.RemoveAt(i);
            }
        }

        SyncPlantViews();
    }

    void SyncPlantViews()
    {
        var liveIds = new HashSet<int>();
        float worldUnitsPerMaskPx = (cam.orthographicSize * 2f) / MaskH;

        foreach (var plant in plants)
        {
            liveIds.Add(plant.Id);
            if (!plantViews.TryGetValue(plant.Id, out var view))
            {
                var viewGo = new GameObject($"plant_{plant.Id}");
                viewGo.transform.SetParent(plantsRoot, false);
                view = viewGo.AddComponent<PlantView>();
                view.Initialize();
                plantViews[plant.Id] = view;
            }

            var rootWorld = MaskToWorld(plant.Position);
            view.Render(plant, rootWorld, worldUnitsPerMaskPx);
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

    /// <summary>
    /// マスクpx座標(原点左下, y上向き) を、LittlePeopleWorld側の normalized 座標
    /// (0..1, 原点左上, y下向き)に変換する MaskPxToNormalized の逆変換。
    /// 雲の位置(normalized)をマスクpx空間へ持ってきて、着地判定・着地座標の計算に使う。
    /// </summary>
    Vector2 NormalizedToMaskPx(Vector2 normalized)
    {
        return new Vector2(normalized.x * MaskW, (1f - normalized.y) * MaskH);
    }

    // 雲・星の初期配置と漂う速度(World.cs の同名ロジックを踏襲した簡易版)
    static Vector2 AmbientSpawnPosition(AmbientObjectKind kind, int index)
    {
        if (kind == AmbientObjectKind.Cloud)
        {
            switch (index % 3)
            {
                case 0: return new Vector2(0.24f, 0.09f);
                case 1: return new Vector2(0.72f, 0.16f);
                default: return new Vector2(0.44f, 0.30f); // 下半分に行かせない設定と矛盾しないよう上半分に変更
            }
        }

        switch (index % 3)
        {
            case 0: return new Vector2(0.54f, 0.08f);
            case 1: return new Vector2(0.90f, 0.45f);
            default: return new Vector2(0.18f, 0.72f);
        }
    }

    static Vector2 AmbientVelocity(Vector2 baseVelocity, int index)
    {
        float xSign = index % 2 == 0 ? 1f : -1f;
        float ySign = index % 3 == 0 ? 1f : -1f;
        return new Vector2(baseVelocity.x * xSign, baseVelocity.y * ySign);
    }

    static Vector2 ClampNormalized(Vector2 value)
    {
        return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    }

    /// <summary>
    /// マスクpx座標(原点左下, y上向き) を、LittlePeopleWorld側の normalized 座標
    /// (0..1, 原点左上, y下向き)に変換する。AmbientObject.Position はこの規約(Clamp01)なので、
    /// 粒の座標系との橋渡しにこの変換が必要。
    /// </summary>
    Vector2 MaskPxToNormalized(Vector2 maskPx)
    {
        return new Vector2(maskPx.x / MaskW, 1f - maskPx.y / MaskH);
    }

    void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.fontSize = 16;
            hudStyle.normal.textColor = Color.white;
        }

        string mode = eraseMode ? "消しゴム" : "ペン";
        GUI.Label(new Rect(12, 10, 460, 24),
            $"モード: {mode} (Eで切替)   サイズ: {brushRadius} ([ ] または ホイール)   最小反応サイズ: {minBlobPixels}px   Cで全消去",
            hudStyle);
    }

    // ================= 粒の操舵 =================

    void UpdateJitter(Particle p, float dt)
    {
        p.jitterTimer -= dt;
        if (p.jitterTimer <= 0f)
        {
            p.jitter = Random.insideUnitCircle * targetJitterPx;
            p.jitterTimer = Random.Range(1.2f, 2.6f);
        }
    }

    void UpdateParticle(Particle p, Vector2 separationForce, float dt)
    {
        // タイマー類を減衰させる
        if (p.reattachCooldown > 0f)
        {
            p.reattachCooldown = Mathf.Max(0f, p.reattachCooldown - dt);
        }

        // 0. 自由飛散状態(はじけた直後)。操舵せず、初速のまま慣性で飛ぶ(分離力だけは受ける)。
        if (p.burstFreeTimer > 0f)
        {
            p.burstFreeTimer = Mathf.Max(0f, p.burstFreeTimer - dt);

            p.vel += separationForce;
            p.pos += p.vel * dt;

            // 画面の縁で緩やかに跳ね返して、外へ突き抜けないようにする
            if (p.pos.x < 1f || p.pos.x > MaskW - 2f)
            {
                p.vel.x *= -0.6f;
            }
            if (p.pos.y < 1f || p.pos.y > MaskH - 2f)
            {
                p.vel.y *= -0.6f;
            }
            p.pos.x = Mathf.Clamp(p.pos.x, 1f, MaskW - 2f);
            p.pos.y = Mathf.Clamp(p.pos.y, 1f, MaskH - 2f);
            return;
        }

        // 1. 既に花に吸い付いている場合は、通常の操舵とは別の「引力+分離力」だけで動く(ホバー挙動)
        if (p.isBloomAttached && p.attachedPlant != null && p.attachedPlant.IsBloomable)
        {
            UpdateBloomAttached(p, p.attachedPlant, separationForce, dt);
            return;
        }

        if (p.isBloomAttached)
        {
            // 対象がしおれ切って吸い付けなくなった場合は解除して通常状態に戻す
            p.isBloomAttached = false;
            p.attachedPlant = null;
        }

        // 2. 既に登行中の植物がまだ有効なら、それを最優先で継続してターゲットにする
        //    (登っている途中で「近傍探索」が別の判定を返して振り出しに戻るのを防ぐため)
        Plant plant;
        if (p.isClimbing && p.attachedPlant != null && p.attachedPlant.CurrentStage != Plant.Stage.Dead)
        {
            plant = p.attachedPlant;
        }
        else
        {
            p.isClimbing = false;
            plant = FindNearestAlivePlantWithinInfluence(p.pos);
        }

        Vector2 desiredDir;

        if (plant != null)
        {
            bool climbingNow;
            (desiredDir, climbingNow) = ComputePlantApproach(p, plant);
            p.isClimbing = climbingNow;
            p.attachedPlant = plant;
        }
        else
        {
            p.attachedPlant = null;

            if (effectiveWhiteCount == 0)
            {
                desiredDir = EdgeSteer(p);
            }
            else
            {
                float f = SampleField(p.pos);
                if (f >= senseThreshold)
                {
                    desiredDir = ContourSteer(p, f);
                }
                else
                {
                    desiredDir = SeekNearestCluster(p); // 自分に一番近い有効な塊へ向かう
                }
            }
        }

        Vector2 combined = desiredDir.normalized + separationForce;

        Vector2 desiredVel = combined.normalized * speedPxPerSec * p.speedScale;
        float t = 1f - Mathf.Exp(-steerLerp * dt);
        p.vel = Vector2.Lerp(p.vel, desiredVel, t);

        if (p.isClimbing)
        {
            // 登り中は分離力で押されても「落下(下方向への移動)」だけはしない。横方向の押し合いはそのまま働く。
            p.vel.y = Mathf.Max(p.vel.y, 0f);
        }

        p.pos += p.vel * dt;

        p.pos.x = Mathf.Clamp(p.pos.x, 1f, MaskW - 2f);
        p.pos.y = Mathf.Clamp(p.pos.y, 1f, MaskH - 2f);

        // 登行中に花の吸い付き範囲へ入ったら、次フレームからホバー(吸い付き)状態へ移行する
        // (ただし、はじけた直後のクールダウン中は吸い付けない)
        if (p.isClimbing && plant != null && plant.IsBloomable && p.reattachCooldown <= 0f)
        {
            float bloomAttractRadius = Mathf.Max(4f, plant.Height * bloomAttractRadiusRatio);
            if (Vector2.Distance(p.pos, plant.BloomPosition) <= bloomAttractRadius)
            {
                p.isBloomAttached = true;
                p.isClimbing = false;
            }
        }
    }

    /// <summary>
    /// 花に吸い付いている粒の動き。花の中心への引力(bloomAttractForce)と、
    /// 粒同士の分離力だけで動く「ホバー」挙動。分離力で押し出されて中心から
    /// bloomSphereRadius を超えて離れたら、呼び出し元(UpdateParticle)側で吸い付きを解除する。
    /// </summary>
    void UpdateBloomAttached(Particle p, Plant plant, Vector2 separationForce, float dt)
    {
        Vector2 toCenter = plant.BloomPosition - p.pos;
        Vector2 pull = toCenter.sqrMagnitude > 0.0001f
            ? toCenter.normalized * (bloomAttractForce * dt)
            : Vector2.zero;

        p.vel = pull + separationForce;
        p.pos += p.vel * dt;

        p.pos.x = Mathf.Clamp(p.pos.x, 1f, MaskW - 2f);
        p.pos.y = Mathf.Clamp(p.pos.y, 1f, MaskH - 2f);

        float bloomSphereRadius = Mathf.Max(4f, plant.Height * bloomSphereRadiusRatio);
        if (Vector2.Distance(p.pos, plant.BloomPosition) > bloomSphereRadius)
        {
            p.isBloomAttached = false;
            p.attachedPlant = null;
        }
    }

    /// <summary>
    /// 粒に一番近い、まだ生きている植物のうち「影響範囲(plantInfluenceRadius)」内にあるものを返す。
    /// 範囲外なら null(植物とは無関係の、通常のマスク追従に任せる)。
    /// </summary>
    Plant FindNearestAlivePlantWithinInfluence(Vector2 pos)
    {
        Plant best = null;
        float bestDist = float.MaxValue;

        foreach (var plant in plants)
        {
            if (plant.CurrentStage == Plant.Stage.Dead)
            {
                continue;
            }

            float influenceRadius = Mathf.Max(6f, plant.Height * plantInfluenceRadiusRatio);
            float d = Vector2.Distance(pos, plant.Position);
            if (d <= influenceRadius && d < bestDist)
            {
                best = plant;
                bestDist = d;
            }
        }

        return best;
    }

    /// <summary>
    /// 影響範囲内にいる植物へのアプローチ方向を計算する。
    /// 根元からの距離が plantClimbAttractRadius 以内なら「登り」、それより遠ければ「根元へ接近」。
    /// </summary>
    (Vector2 direction, bool isClimbing) ComputePlantApproach(Particle p, Plant plant)
    {
        float distToBase = Vector2.Distance(p.pos, plant.Position);
        float climbRadius = Mathf.Max(4f, plant.Height * plantClimbAttractRadiusRatio);

        if (distToBase <= climbRadius)
        {
            Vector2 toTop = plant.BloomPosition - p.pos;
            return (toTop.sqrMagnitude > 0.0001f ? toTop.normalized : Vector2.up, true);
        }

        Vector2 toBase = plant.Position - p.pos;
        return (toBase.sqrMagnitude > 0.0001f ? toBase.normalized : Vector2.zero, false);
    }

    void ComputeSeparation(Vector2[] outForces)
    {
        float r2 = separationRadiusPx * separationRadiusPx;

        for (int i = 0; i < particles.Count; i++)
        {
            Vector2 accum = Vector2.zero;
            Vector2 pi = particles[i].pos;

            for (int j = 0; j < particles.Count; j++)
            {
                if (i == j) continue;
                Vector2 diff = pi - particles[j].pos;
                float d2 = diff.sqrMagnitude;
                if (d2 > r2 || d2 < 0.0001f) continue;

                float d = Mathf.Sqrt(d2);
                accum += diff / d * (1f - d / separationRadiusPx);
            }

            outForces[i] = accum * (separationGain * Time.deltaTime);
        }
    }

    Vector2 ContourSteer(Particle p, float f)
    {
        Vector2 g = FieldGradient(p.pos);
        if (g.sqrMagnitude < 1e-8f)
        {
            return p.vel.sqrMagnitude > 1e-4f ? p.vel : Random.insideUnitCircle;
        }

        Vector2 gn = g.normalized;
        Vector2 tangent = new Vector2(-gn.y, gn.x);
        Vector2 correction = gn * (contourLevel - f) * correctionGain;
        return tangent + correction;
    }

    Vector2 EdgeSteer(Particle p)
    {
        Vector2 pos = p.pos;
        float minX = edgeMarginPx, maxX = MaskW - edgeMarginPx;
        float minY = edgeMarginPx, maxY = MaskH - edgeMarginPx;

        float cx = Mathf.Clamp(pos.x, minX, maxX);
        float cy = Mathf.Clamp(pos.y, minY, maxY);

        float dL = cx - minX, dR = maxX - cx, dB = cy - minY, dT = maxY - cy;
        float m = Mathf.Min(Mathf.Min(dL, dR), Mathf.Min(dB, dT));

        Vector2 nearest;
        Vector2 tangent;
        if (m == dB)      { nearest = new Vector2(cx, minY); tangent = new Vector2( 1f, 0f); }
        else if (m == dR) { nearest = new Vector2(maxX, cy); tangent = new Vector2(0f,  1f); }
        else if (m == dT) { nearest = new Vector2(cx, maxY); tangent = new Vector2(-1f, 0f); }
        else              { nearest = new Vector2(minX, cy); tangent = new Vector2(0f, -1f); }

        nearest += p.jitter;
        return tangent + (nearest - pos) * edgePullGain;
    }

    /// 自分(粒)に最も近い、有効な(十分大きい)密集セルへ向かう。
    /// グローバルな1点ではなく粒ごとに最寄りを選ぶため、離れた場所に複数の塊があれば
    /// 粒はそれぞれ近い方へ分かれて集まる。
    /// 反応距離は花の吸い付き(bloomSphereRadius)と同じ考え方で、塊のピクセル数に応じて動的に決まる
    /// (小さい塊ほど近距離でしか反応せず、離れると自動的に「反応なし」に戻る)。
    Vector2 SeekNearestCluster(Particle p)
    {
        Vector2 pos = p.pos;
        float cellW = (float)MaskW / CellsX;
        float cellH = (float)MaskH / CellsY;
        float best = float.MaxValue;
        Vector2 target = pos;
        int bestCellIndex = -1;
        bool found = false;

        for (int cy = 0; cy < CellsY; cy++)
        {
            for (int cx = 0; cx < CellsX; cx++)
            {
                int cellIndex = cy * CellsX + cx;
                if (cellCounts[cellIndex] == 0) continue;
                Vector2 center = new Vector2((cx + 0.5f) * cellW, (cy + 0.5f) * cellH);
                float d = (center - pos).sqrMagnitude;
                if (d < best) { best = d; target = center; bestCellIndex = cellIndex; found = true; }
            }
        }

        if (!found)
        {
            return EdgeSteer(p);
        }

        // 最寄りの塊のピクセル数から、反応距離を計算する(下限 + サイズに応じた加算、絶対上限つき)
        int componentSize = cellMaxComponentSize[bestCellIndex];
        float sizeBasedRadius = minPenAttractRadiusPx + Mathf.Sqrt(componentSize) * penAttractRadiusPerSqrtPixel;
        float attractRadius = Mathf.Min(sizeBasedRadius, penSenseRadiusPx);

        // その反応距離より遠ければ、「本当に近傍にペンが無い」とみなして反応しない(縁の周回に戻る)
        if (best > attractRadius * attractRadius)
        {
            return EdgeSteer(p);
        }

        return (target + p.jitter) - pos;
    }

    // ================= 小さい塊の除外(連結成分フィルタ) =================

    /// mask から「繋がった白ピクセルの塊(連結成分)」を検出し、ピクセル数が
    /// minBlobPixels 未満の小さな塊を除いた effectiveMask を作る。
    /// これにより、単発クリックのような小さいノイズ点は以降の判断に一切影響しなくなる。
    void RecomputeEffectiveMask()
    {
        System.Array.Clear(effectiveMask, 0, effectiveMask.Length);
        System.Array.Clear(visited, 0, visited.Length);
        System.Array.Clear(componentSizeMap, 0, componentSizeMap.Length);
        effectiveWhiteCount = 0;

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start])
            {
                continue;
            }

            // 4近傍の連結成分をスタックで探索(再帰を使わずGC負荷も抑える)
            floodStack.Clear();
            componentBuffer.Clear();
            floodStack.Add(start);
            visited[start] = true;

            while (floodStack.Count > 0)
            {
                int idx = floodStack[floodStack.Count - 1];
                floodStack.RemoveAt(floodStack.Count - 1);
                componentBuffer.Add(idx);

                int x = idx % MaskW;
                int y = idx / MaskW;

                TryVisitNeighbor(x + 1, y);
                TryVisitNeighbor(x - 1, y);
                TryVisitNeighbor(x, y + 1);
                TryVisitNeighbor(x, y - 1);
            }

            if (componentBuffer.Count >= minBlobPixels)
            {
                for (int i = 0; i < componentBuffer.Count; i++)
                {
                    effectiveMask[componentBuffer[i]] = true;
                    componentSizeMap[componentBuffer[i]] = componentBuffer.Count; // この塊の総ピクセル数を記録
                }
                effectiveWhiteCount += componentBuffer.Count;
            }
            // minBlobPixels 未満の塊は effectiveMask に反映しない(=無視される)
        }
    }

    void TryVisitNeighbor(int x, int y)
    {
        if (x < 0 || x >= MaskW || y < 0 || y >= MaskH)
        {
            return;
        }

        int idx = y * MaskW + x;
        if (!mask[idx] || visited[idx])
        {
            return;
        }

        visited[idx] = true;
        floodStack.Add(idx);
    }

    /// 有効マスク(effectiveMask)だけを使って粗いグリッドの占有数を再集計する
    void RebuildCellCounts()
    {
        System.Array.Clear(cellCounts, 0, cellCounts.Length);
        System.Array.Clear(cellMaxComponentSize, 0, cellMaxComponentSize.Length);
        for (int y = 0; y < MaskH; y++)
        {
            int row = y * MaskW;
            int cellY = y * CellsY / MaskH;
            for (int x = 0; x < MaskW; x++)
            {
                if (!effectiveMask[row + x])
                {
                    continue;
                }
                int cellX = x * CellsX / MaskW;
                int cellIndex = cellY * CellsX + cellX;
                cellCounts[cellIndex]++;
                // このセルに顔を出している塊のうち、最大のものを記録しておく
                if (componentSizeMap[row + x] > cellMaxComponentSize[cellIndex])
                {
                    cellMaxComponentSize[cellIndex] = componentSizeMap[row + x];
                }
            }
        }
    }

    // ================= field(ぼかしたマスク) =================

    /// effectiveMask(小さい塊を除外済み)をぼかして field を作る
    void RebuildField()
    {
        int R = blurRadius;
        float inv = 1f / (2 * R + 1);

        for (int y = 0; y < MaskH; y++)
        {
            int row = y * MaskW;
            for (int x = 0; x < MaskW; x++)
            {
                float s = 0f;
                for (int dx = -R; dx <= R; dx++)
                {
                    int xx = Mathf.Clamp(x + dx, 0, MaskW - 1);
                    if (effectiveMask[row + xx]) s += 1f;
                }
                fieldTmp[row + x] = s * inv;
            }
        }
        for (int x = 0; x < MaskW; x++)
        {
            for (int y = 0; y < MaskH; y++)
            {
                float s = 0f;
                for (int dy = -R; dy <= R; dy++)
                {
                    int yy = Mathf.Clamp(y + dy, 0, MaskH - 1);
                    s += fieldTmp[yy * MaskW + x];
                }
                field[y * MaskW + x] = s * inv;
            }
        }
    }

    float SampleField(Vector2 p)
    {
        float x = Mathf.Clamp(p.x, 0f, MaskW - 1.001f);
        float y = Mathf.Clamp(p.y, 0f, MaskH - 1.001f);
        int x0 = (int)x, y0 = (int)y;
        float fx = x - x0, fy = y - y0;
        int i = y0 * MaskW + x0;

        float v00 = field[i];
        float v10 = field[i + 1];
        float v01 = field[i + MaskW];
        float v11 = field[i + MaskW + 1];
        return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
    }

    Vector2 FieldGradient(Vector2 p)
    {
        const float d = 1.5f;
        float gx = SampleField(p + new Vector2(d, 0)) - SampleField(p - new Vector2(d, 0));
        float gy = SampleField(p + new Vector2(0, d)) - SampleField(p - new Vector2(0, d));
        return new Vector2(gx, gy) / (2f * d);
    }

    // ================= ペン入力 =================

    void HandleModeKeys()
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.E))
        {
            eraseMode = !eraseMode;
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket))
        {
            brushRadius = Mathf.Clamp(brushRadius - brushRadiusStep, brushRadiusMin, brushRadiusMax);
        }
        else if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket))
        {
            brushRadius = Mathf.Clamp(brushRadius + brushRadiusStep, brushRadiusMin, brushRadiusMax);
        }

        float scroll = UnityEngine.Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            brushRadius = Mathf.Clamp(brushRadius + (scroll > 0 ? brushRadiusStep : -brushRadiusStep),
                                       brushRadiusMin, brushRadiusMax);
        }

        if (UnityEngine.Input.GetKeyDown(KeyCode.C)) ClearMask();
    }

    void HandlePainting()
    {
        bool leftHeld = UnityEngine.Input.GetMouseButton(0);
        bool rightHeld = UnityEngine.Input.GetMouseButton(1);
        if (!leftHeld && !rightHeld) return;

        bool white = leftHeld && !eraseMode && !rightHeld;

        Vector2 mp = MouseToMask();

        if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonDown(1))
        {
            lastPaintPos = mp;
            PaintStamp(mp, white);
        }
        else
        {
            float dist = Vector2.Distance(lastPaintPos, mp);
            float step = Mathf.Max(1f, brushRadius * 0.5f);
            int n = Mathf.CeilToInt(dist / step);
            for (int i = 1; i <= n; i++)
                PaintStamp(Vector2.Lerp(lastPaintPos, mp, (float)i / n), white);
            lastPaintPos = mp;
        }
    }

    void UpdateBrushCursor()
    {
        Vector2 mp = MouseToMask();
        brushCursorGo.transform.position = MaskToWorld(mp);

        float worldH = cam.orthographicSize * 2f;
        float ppu = MaskH / worldH;
        float worldRadius = brushRadius / ppu;
        brushCursorGo.transform.localScale = Vector3.one * (worldRadius * 2f);

        brushCursorSr.color = eraseMode
            ? new Color(1f, 0.35f, 0.35f, 0.9f)
            : new Color(1f, 1f, 1f, 0.85f);
    }

    void PaintStamp(Vector2 center, bool white)
    {
        int r = brushRadius;
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int r2 = r * r;

        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(MaskH - 1, cy + r); y++)
        {
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(MaskW - 1, cx + r); x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r2) continue;

                int idx = y * MaskW + x;
                if (mask[idx] == white) continue;

                mask[idx] = white;
                pixels[idx] = white ? ColWhite : ColBlack;
                maskDirty = true;

                // 白化・黒化どちらの変化も、花のはじけ判定のために座標を記録しておく
                changedPixelsThisFrame.Add(new Vector2(x, y));
            }
        }
    }

    void ClearMask()
    {
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = false;
            pixels[i] = ColBlack;
        }
        effectiveWhiteCount = 0;
        maskDirty = true;
    }

    // ================= 座標変換 =================

    Vector2 MouseToMask()
    {
        Vector3 vp = cam.ScreenToViewportPoint(UnityEngine.Input.mousePosition);
        return new Vector2(Mathf.Clamp01(vp.x) * MaskW, Mathf.Clamp01(vp.y) * MaskH);
    }

    Vector3 MaskToWorld(Vector2 mp)
    {
        Vector3 vp = new Vector3(mp.x / MaskW, mp.y / MaskH, -cam.transform.position.z);
        Vector3 w = cam.ViewportToWorldPoint(vp);
        w.z = 0f;
        return w;
    }

    // ================= 見た目生成(RuntimeSpriteFactory.Circle と同じ、実績のある生成方法) =================

    static Sprite CreateCircleSprite(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = (size - 1) * 0.5f;
        var radius = center - 1f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Clamp01(radius + 0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite CreateRingSprite()
    {
        const int res = 64;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float c = (res - 1) * 0.5f;
        const float ringWidth = 0.06f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float ring = 1f - Mathf.Abs(d - 0.9f) / ringWidth;
                float a = Mathf.Clamp01(ring);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
