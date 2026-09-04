using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Small procedural circular sprites, kept for two different reasons -
    /// neither of them "draw the medallion".
    ///
    /// <see cref="Ring"/> is <see cref="HeroPowerView"/>'s last-resort stand-in
    /// for its frame, reached for only on the rare path where no frame sprite
    /// is assigned at all - an unconfigured scene, a test that never wired
    /// one. The normal configured match draws the authored bronze-and-gold
    /// ring instead (<c>HeroPower_Frame.png</c>); this must never be
    /// preferred over it.
    ///
    /// <see cref="Disc"/> has a second, ongoing job that is not a fallback at
    /// all: it is the invisible stencil shape a uGUI <see cref="Mask"/> clips
    /// centre art to, and the shared circular background the four-choice
    /// stat gems are drawn on. Neither of those is "art" a real asset would
    /// ever replace - a mask shape has nothing to look like - so generating
    /// them in code rather than importing a texture is the permanent choice,
    /// not a placeholder for one.
    /// </summary>
    internal static class MedallionArt
    {
        private const int Size = 128;

        /// <summary>
        /// The name stamped on every sprite this class produces. Never the
        /// name of a resolved catalog or library sprite, which is why a test
        /// can tell apart "the fallback drew" from "the real frame drew" by
        /// name alone.
        /// </summary>
        public const string FallbackName = "MedallionArt_Fallback";

        private static Sprite _disc;
        private static Sprite _ring;
        private static Sprite _solid;

        /// <summary>A solid, softly anti-aliased circle. Used as a clip mask shape and a gem background.</summary>
        public static Sprite Disc()
        {
            return _disc ??= Build(innerRadius01: 0f);
        }

        /// <summary>
        /// A single opaque white pixel, one world unit wide at its default
        /// scale - tinted and stretched by whatever draws it. The choice
        /// menu's dimmed backdrop is the one thing here that is a flat
        /// rectangle rather than a circle, so it does not share <see cref="Disc"/>'s
        /// shape; it shares this class because it is exactly as much "not a
        /// real asset" as a mask shape is.
        /// </summary>
        public static Sprite Solid()
        {
            if (_solid != null)
            {
                return _solid;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);

            _solid = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _solid.name = FallbackName;
            return _solid;
        }

        /// <summary>
        /// A ring (donut), thick enough to actually read as a border rather
        /// than a thin outline - the frame's last-resort stand-in when no
        /// sprite is assigned.
        /// </summary>
        public static Sprite Ring()
        {
            return _ring ??= Build(innerRadius01: 0.70f);
        }

        private static Sprite Build(float innerRadius01)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2(Size * 0.5f, Size * 0.5f);
            float outer = Size * 0.5f - 1f;
            float inner = outer * innerRadius01;

            Color[] pixels = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

                    float outerEdge = Mathf.Clamp01(outer - distance);
                    float innerEdge = inner <= 0f ? 1f : Mathf.Clamp01(distance - inner);

                    float alpha = Mathf.Min(outerEdge, innerEdge);

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = FallbackName;
            return sprite;
        }
    }
}
