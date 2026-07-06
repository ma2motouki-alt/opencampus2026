using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public static class RuntimeSpriteFactory
    {
        static Sprite circle;
        static Sprite square;
        static Sprite star;
        static Sprite teardrop;

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    circle = CreateCircleSprite(64);
                }

                return circle;
            }
        }

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    for (var y = 0; y < texture.height; y++)
                    {
                        for (var x = 0; x < texture.width; x++)
                        {
                            texture.SetPixel(x, y, Color.white);
                        }
                    }

                    texture.Apply();
                    texture.wrapMode = TextureWrapMode.Clamp;
                    square = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
                }

                return square;
            }
        }

        public static Sprite Star
        {
            get
            {
                if (star == null)
                {
                    star = CreateStarSprite(96);
                }

                return star;
            }
        }

        // 雨のしずく用。上がとがり下が丸い、涙(teardrop)型のスプライト。
        // pivotは下端寄り(0.5, 0.28)にして、回転させたとき「丸い方が進行方向・とがった方が後ろ」に
        // なるよう扱いやすくしている。
        public static Sprite Teardrop
        {
            get
            {
                if (teardrop == null)
                {
                    teardrop = CreateTeardropSprite(64);
                }

                return teardrop;
            }
        }

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

        // 涙(teardrop)型スプライトを生成する。
        // 縦(y)方向に、上端が鋭くとがり・下端が丸く膨らむ形。横断面の半径を y の高さに応じて変化させ、
        // 上へ行くほど細く(0へ)、下側は半円状に丸める。アンチエイリアスのため輪郭を1pxぼかす。
        static Sprite CreateTeardropSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = (size - 1) * 0.5f;

            // y=0(下端)〜1(上端)の正規化高さに対する「横幅の最大半径(px)」を返す形状関数。
            // 下側(t<=0.32)は半円、上側(t>0.32)は先端に向かってなめらかに細くなる。
            float maxHalf = size * 0.30f;      // しずくの最も太い部分の半径
            float bulbCenter = 0.30f;          // 丸い膨らみの中心高さ(正規化)
            float bulbRadius = 0.30f;          // 丸い膨らみの半径(正規化)

            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);          // 0(下)〜1(上)
                float halfWidth;

                if (t <= bulbCenter)
                {
                    // 下端の丸み: 円の下半分(中心 bulbCenter、半径 bulbRadius)
                    float dy = (bulbCenter - t) / bulbRadius;   // 0〜1
                    dy = Mathf.Clamp01(dy);
                    halfWidth = maxHalf * Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
                }
                else
                {
                    // 上側: bulbCenter で最大幅、t=1 の先端で 0 になるようイーズ(2乗)で細くする
                    float u = (t - bulbCenter) / (1f - bulbCenter); // 0〜1
                    float taper = 1f - u;
                    halfWidth = maxHalf * taper * taper;
                }

                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    // 輪郭を1pxぼかしてアンチエイリアス
                    float alpha = Mathf.Clamp01(halfWidth + 0.5f - dx);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            // pivotは下寄り。回転の中心を膨らみ側に置くと、雨の向き合わせがしやすい。
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.28f), size);
        }

        static Sprite CreateStarSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var outer = size * 0.42f;
            var inner = size * 0.19f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var offset = new Vector2(x, y) - center;
                    var angle = Mathf.Atan2(offset.y, offset.x);
                    var radius = offset.magnitude;
                    var point = Mathf.Cos(angle * 5f);
                    var targetRadius = Mathf.Lerp(inner, outer, Mathf.Abs(point));
                    var alpha = Mathf.Clamp01(targetRadius + 1.5f - radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
