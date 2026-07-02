using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public static class RuntimeSpriteFactory
    {
        static Sprite circle;
        static Sprite square;
        static Sprite star;

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
