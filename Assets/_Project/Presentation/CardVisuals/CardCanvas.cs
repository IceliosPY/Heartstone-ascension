using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// The coordinate system a card is designed in, and how it reaches the
    /// table.
    ///
    /// Everything in a recipe is authored in pixels of an 800 by 1100 canvas —
    /// the proportions the project's card layout was measured on. Nothing in a
    /// recipe is in world units, and that is the separation that matters: the
    /// recipe says what the card *is*, and a layout says how big it is and where
    /// it is being shown.
    ///
    /// One card is one unit wide. A card in a hand, a card blown up for
    /// inspection and a card in a future collection are the same composition at
    /// three sizes, not three compositions.
    /// </summary>
    public static class CardCanvas
    {
        public const float Width = 800f;
        public const float Height = 1100f;

        /// <summary>A card is one unit across, so its height follows the ratio.</summary>
        public const float CardWidth = 1f;
        public const float CardHeight = CardWidth * (Height / Width);

        /// <summary>
        /// How far apart two consecutive sorting orders are pushed along the
        /// card's normal.
        ///
        /// The card is a flat object standing at an angle on a table rather than
        /// a sprite in a 2D scene, so depth has to be real distance. Small
        /// enough that the stack still reads as one card from any angle the
        /// camera can reach.
        /// </summary>
        public const float DepthStep = 0.0006f;

        /// <summary>Centre of a canvas rectangle, in the card's local space.</summary>
        public static Vector3 ToLocalPosition(Rect rect, int sortingOrder)
        {
            float centreX = (rect.x + rect.width * 0.5f) / Width;
            float centreY = (rect.y + rect.height * 0.5f) / Height;

            return new Vector3(
                (centreX - 0.5f) * CardWidth,
                (0.5f - centreY) * CardHeight,

                // Negative is toward the viewer: a higher sorting order draws in
                // front, which is the direction every sorting order in the
                // engine goes.
                -sortingOrder * DepthStep);
        }

        /// <summary>Size of a canvas rectangle, in the card's local space.</summary>
        public static Vector2 ToLocalSize(Rect rect) =>
            new Vector2(
                rect.width / Width * CardWidth,
                rect.height / Height * CardHeight);
    }
}
