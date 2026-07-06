using UnityEngine;

/// <summary>
/// 雨が地面に着いたときに生まれる植物。見た目(PlantView)を一切持たない、状態管理だけのクラス。
///
/// 成長段階は「生成からの経過時間(AgeSeconds)」と「最後に雨を受けてからの経過時間(SecondsSinceRain)」
/// の2つだけから毎回計算される(状態遷移をイベントで持たず、常に純粋関数的に導出する設計)。
///
///   Seedling(芽)  : Age &lt; seedlingDuration
///   Growing(成長中): seedlingDuration &lt;= Age &lt; seedlingDuration + growingDuration
///   Blooming(開花) : 上記以降、かつ「雨が止んでから wiltingStartDelay 秒未満」
///   Wilting(しおれ): 「雨が止んでから wiltingStartDelay 秒」〜「+ wiltingDuration 秒」
///   Dead(枯死)    : それ以降(→呼び出し側で削除する)
///
/// Seedling/Growing の間は、雨が降らなくても時間経過だけで開花まで育つ(「時間で花が咲く」仕様)。
/// Wilting は Blooming に達した後、水切れ(雨が降らない)によってのみ発生する。
/// Wilting 中に雨を受けると SecondsSinceRain が 0 にリセットされるため、自然に Blooming へ回復する。
/// </summary>
public sealed class Plant
{
    public enum Stage
    {
        Seedling,
        Growing,
        Blooming,
        Wilting,
        Dead,
    }

    public int Id { get; }

    /// 根元の位置(マスクpx座標、原点左下・y上向き。DepthMaskContourParticles の粒座標系と共通)
    public Vector2 Position { get; }

    public float AgeSeconds { get; private set; }
    public float SecondsSinceRain { get; private set; }

    /// 雨を受けた回数(見た目のバリエーション付け以外には使わない、純粋な演出用の値)
    public int RainCount { get; private set; }

    /// 現在の茎の高さ(マスクpx)。0(生成直後)〜maxHeightPx(Growing終了以降は一定)。
    public float Height { get; private set; }

    readonly float seedlingDuration;
    readonly float growingDuration;
    readonly float wiltingStartDelay;
    readonly float wiltingDuration;
    readonly float maxHeightPx;

    public Plant(
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

        AgeSeconds = 0f;
        SecondsSinceRain = 0f;
        RainCount = 0;
        Height = 0f;
    }

    /// 現在の成長段階。AgeSeconds / SecondsSinceRain から毎回導出する(状態を別途持たない)。
    public Stage CurrentStage
    {
        get
        {
            if (AgeSeconds < seedlingDuration)
            {
                return Stage.Seedling;
            }

            if (AgeSeconds < seedlingDuration + growingDuration)
            {
                return Stage.Growing;
            }

            float overDelay = SecondsSinceRain - wiltingStartDelay;
            if (overDelay <= 0f)
            {
                return Stage.Blooming;
            }

            if (overDelay < wiltingDuration)
            {
                return Stage.Wilting;
            }

            return Stage.Dead;
        }
    }

    /// Wilting段階の進行度(0=しおれ始め、1=枯死寸前)。Wilting以外では0を返す。
    public float WiltProgress01
    {
        get
        {
            if (CurrentStage != Stage.Wilting)
            {
                return 0f;
            }

            return Mathf.Clamp01((SecondsSinceRain - wiltingStartDelay) / wiltingDuration);
        }
    }

    /// 花(頂点)の位置。粒が登る/吸い付く際の目標座標として使う。
    public Vector2 BloomPosition => new Vector2(Position.x, Position.y + Height);

    /// 花が咲いていて、粒が吸い付ける状態かどうか。
    public bool IsBloomable
    {
        get
        {
            var stage = CurrentStage;
            return stage == Stage.Blooming || stage == Stage.Wilting;
        }
    }

    /// 雨を受けたときに呼ぶ。Wilting中の回復や、Blooming維持のためのタイマーリセットを行う。
    public void ReceiveRain()
    {
        SecondsSinceRain = 0f;
        RainCount++;
    }

    public void Advance(float dt)
    {
        AgeSeconds += dt;
        SecondsSinceRain += dt;

        float growEnd = seedlingDuration + growingDuration;
        float growProgress = Mathf.Clamp01(AgeSeconds / growEnd);
        // イーズアウトで、育ち始めはゆっくり・後半で伸びが落ち着く曲線にする
        float eased = 1f - (1f - growProgress) * (1f - growProgress);
        Height = maxHeightPx * eased;
    }
}
