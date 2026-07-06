using UnityEngine;

/// <summary>
/// Plant の見た目を担当する View。DepthMaskContourParticles の粒と同じ、
/// 実績のある「丸スプライトを引き伸ばして着色する」方式で描画する(テクスチャ焼き込みはしない)。
///
/// 座標系について:
/// Plant.Position / Plant.Height はマスクpx単位(DepthMaskContourParticlesの粒と共通)なので、
/// Render() にはワールド座標に変換済みの根元位置(rootWorldPosition)と、
/// 「マスクpx 1つがワールド単位で何メートル相当か」を表す worldUnitsPerMaskPx を渡してもらう。
/// PlantView 自身はマスク⇔ワールドの変換方法を知らなくてよい(呼び出し側の責務)。
/// </summary>
public sealed class PlantView : MonoBehaviour
{
    SpriteRenderer stemGlowRenderer;
    SpriteRenderer stemRenderer;
    SpriteRenderer flowerGlowRenderer;
    SpriteRenderer flowerRenderer;
    SpriteRenderer flowerCenterRenderer;

    static Sprite circleSprite;
    static Sprite Circle => circleSprite != null ? circleSprite : (circleSprite = CreateCircleSprite(64));

    static readonly Color HealthyStemColor = new Color(0.35f, 0.82f, 0.42f, 1f);
    static readonly Color WiltedStemColor = new Color(0.62f, 0.5f, 0.28f, 1f);
    static readonly Color HealthyFlowerColor = new Color(1f, 0.55f, 0.75f, 1f);
    static readonly Color WiltedFlowerColor = new Color(0.55f, 0.42f, 0.32f, 1f);

    public void Initialize()
    {
        stemGlowRenderer = CreateRenderer("StemGlow", -3);
        stemRenderer = CreateRenderer("Stem", 6);
        flowerGlowRenderer = CreateRenderer("FlowerGlow", 7);
        flowerRenderer = CreateRenderer("Flower", 8);
        flowerCenterRenderer = CreateRenderer("FlowerCenter", 9);
    }

    public void Render(Plant plant, Vector3 rootWorldPosition, float worldUnitsPerMaskPx)
    {
        float wilt = WiltAmount(plant);
        float stemHeightWorld = Mathf.Max(0.0005f, plant.Height * worldUnitsPerMaskPx);
        float stemWidthWorld = Mathf.Max(0.004f, stemHeightWorld * 0.14f);

        // ---- 茎(根元から上に伸びる) ----
        var stemCenter = rootWorldPosition + new Vector3(0f, stemHeightWorld * 0.5f, 0f);
        stemRenderer.transform.position = stemCenter;
        stemRenderer.transform.localScale = new Vector3(stemWidthWorld, stemHeightWorld, 1f);
        stemRenderer.color = Color.Lerp(HealthyStemColor, WiltedStemColor, wilt);

        stemGlowRenderer.transform.position = stemCenter;
        stemGlowRenderer.transform.localScale = new Vector3(stemWidthWorld * 3.2f, stemHeightWorld * 1.05f, 1f);
        var stemGlowColor = Color.Lerp(stemRenderer.color, Color.white, 0.5f);
        stemGlowColor.a = Mathf.Lerp(0.16f, 0.04f, wilt);
        stemGlowRenderer.color = stemGlowColor;

        // ---- 花(Blooming/Wilting のときだけ表示) ----
        bool showFlower = plant.CurrentStage == Plant.Stage.Blooming || plant.CurrentStage == Plant.Stage.Wilting;
        flowerRenderer.enabled = showFlower;
        flowerGlowRenderer.enabled = showFlower;
        flowerCenterRenderer.enabled = showFlower;

        if (!showFlower)
        {
            return;
        }

        Vector3 bloomWorldPos = rootWorldPosition + new Vector3(0f, stemHeightWorld, 0f);
        // Wiltingが進むほど花がすぼむ(見た目だけの表現。判定半径はDepthMaskContourParticles側で別途扱う)
        float bloomOpen = Mathf.Lerp(1f, 0.45f, wilt);
        float flowerBaseScale = Mathf.Max(0.004f, plant.Height * worldUnitsPerMaskPx * 0.55f);

        var flowerColor = Color.Lerp(HealthyFlowerColor, WiltedFlowerColor, wilt);
        flowerColor.a = Mathf.Lerp(1f, 0.4f, wilt);

        flowerRenderer.transform.position = bloomWorldPos;
        flowerRenderer.transform.localScale = Vector3.one * flowerBaseScale * bloomOpen;
        flowerRenderer.color = flowerColor;

        flowerCenterRenderer.transform.position = bloomWorldPos;
        flowerCenterRenderer.transform.localScale = Vector3.one * flowerBaseScale * 0.42f * bloomOpen;
        flowerCenterRenderer.color = Color.Lerp(Color.white, WiltedFlowerColor, wilt * 0.8f);

        flowerGlowRenderer.transform.position = bloomWorldPos;
        flowerGlowRenderer.transform.localScale = Vector3.one * flowerBaseScale * 2.4f;
        var flowerGlowColor = Color.Lerp(flowerColor, Color.white, 0.55f);
        flowerGlowColor.a = Mathf.Lerp(0.32f, 0.05f, wilt);
        flowerGlowRenderer.color = flowerGlowColor;
    }

    static float WiltAmount(Plant plant)
    {
        if (plant.CurrentStage == Plant.Stage.Dead)
        {
            return 1f;
        }

        return plant.WiltProgress01;
    }

    SpriteRenderer CreateRenderer(string name, int sortingOrder)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    // RuntimeSpriteFactory.Circle / DepthMaskContourParticles と同じ、実績のある丸スプライト生成方法
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
}
