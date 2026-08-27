using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>Where a card should sit, as a target the view moves toward.</summary>
    public readonly struct CardPose
    {
        public CardPose(Vector3 localPosition, Quaternion localRotation, float scale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Scale = scale;
        }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public float Scale { get; }
    }

    [System.Serializable]
    public sealed class HandFanSettings
    {
        [Tooltip(
            "How large a card is drawn in the hand. Presentation only: the card's own " +
            "proportions and everything inside it belong to the recipe and do not change with this.")]
        public float Scale = 1.45f;

        [Tooltip(
            "How far apart two neighbours sit, as a fraction of a card's width. Below one they " +
            "overlap, which is what makes a hand read as a fan rather than as a row.")]
        [Range(0.2f, 1.2f)]
        public float Spacing = 0.765f;

        [Tooltip(
            "How wide the fan may grow, in world units, before it stops spreading and starts " +
            "overlapping harder. This is what keeps a full hand off the deck piles.")]
        public float MaxWidth = 7.56f;

        [Tooltip(
            "Radius of the arc the cards lie on. Larger is flatter: it sets how much the outer " +
            "cards lean, without changing how far apart they are.")]
        public float PivotDistance = 15.0f;

        [Tooltip("Gap along the view direction, so overlapping cards stack predictably.")]
        public float DepthStep = 0.035f;
    }

    /// <summary>
    /// Works out where each card of a hand belongs.
    ///
    /// The spacing is the input and the angle is the consequence, which is the
    /// opposite of the obvious way round and the only way to get a hand that
    /// looks like a card game's. Turning each card a fixed number of degrees
    /// further round a pivot gives an arc, but it ties how much cards overlap to
    /// how much they lean: to overlap more you have to lean more, and by the
    /// time the overlap looks right the outer cards are lying on their sides.
    ///
    /// So the cards are placed at a chosen distance apart, and the arc is then
    /// read off that distance. Overlap and lean become two numbers that can be
    /// set independently — the spacing decides one, the radius the other.
    ///
    /// The spacing tightens on its own as the hand fills up: a hand spreads
    /// until it reaches the width it is allowed, and after that more cards mean
    /// more overlap rather than a wider fan. One expression, no thresholds.
    ///
    /// Pure geometry, on purpose: it takes an index and a count and returns a
    /// pose, touching no scene object.
    /// </summary>
    public static class HandFanLayout
    {
        /// <summary>A card is one unit wide before the hand's scale is applied.</summary>
        private const float CardWidth = 1f;

        /// <summary>
        /// How far apart two neighbours actually end up, once the fan has run
        /// out of room.
        ///
        /// Public because it is the number every question about a hand comes
        /// down to — whether a mana gem is covered, whether a card can be
        /// clicked — and working it out a second time somewhere else is how two
        /// answers start disagreeing.
        /// </summary>
        public static float SpacingFor(int count, HandFanSettings settings)
        {
            if (count <= 1)
            {
                return 0f;
            }

            float wanted = CardWidth * settings.Scale * settings.Spacing;
            float allowed = settings.MaxWidth / (count - 1);

            return Mathf.Min(wanted, allowed);
        }

        /// <summary>How wide the whole hand is, centre of leftmost to centre of rightmost.</summary>
        public static float WidthOf(int count, HandFanSettings settings) =>
            count <= 1 ? 0f : SpacingFor(count, settings) * (count - 1);

        public static CardPose GetPose(int index, int count, HandFanSettings settings)
        {
            if (count <= 0)
            {
                return new CardPose(Vector3.zero, Quaternion.identity, settings.Scale);
            }

            // -0.5 for the leftmost card, +0.5 for the rightmost, 0 for one card.
            float centered = count == 1 ? 0f : (index / (float)(count - 1)) - 0.5f;

            float x = centered * WidthOf(count, settings);

            // The lean follows from the position rather than the other way
            // round. A card at the end of a wide fan leans more than one near
            // the middle, and a larger radius flattens all of them at once
            // without moving any of them sideways.
            float radius = Mathf.Max(0.01f, settings.PivotDistance);
            float angle = Mathf.Asin(Mathf.Clamp(x / radius, -1f, 1f));

            // Measured from the middle card, so the centre of the hand stays put
            // whatever the hand size.
            float y = (Mathf.Cos(angle) - 1f) * radius;

            // Toward the camera, not away from it. The hand anchor's local +z
            // points away, so stepping the depth positively used to put later
            // cards further off — while the sorting group drew them in front.
            // The pointer takes whatever is nearest, so it picked the card on
            // the left while the player was looking at the one on the right.
            // Depth and draw order now agree, and a click lands on the card
            // that is visibly on top.
            float z = -index * settings.DepthStep;

            return new CardPose(
                new Vector3(x, y, z),
                Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg),
                settings.Scale);
        }
    }

    /// <summary>
    /// Works out where each minion of a board row belongs.
    ///
    /// The index handed in is the index in the engine's board zone, so what a
    /// player sees left to right is exactly the order the rules use. Unity holds
    /// no second opinion about board order, and nothing about gameplay is
    /// decided here: this only turns an order into positions.
    ///
    /// The row stays centred at every size, from one minion to seven.
    /// </summary>
    public static class BoardRowLayout
    {
        public static Vector3 GetPosition(int index, int count, float spacing)
        {
            if (count <= 0)
            {
                return Vector3.zero;
            }

            float centered = count == 1 ? 0f : (index / (float)(count - 1)) - 0.5f;

            return new Vector3(centered * spacing * (count - 1), 0f, 0f);
        }
    }
}
