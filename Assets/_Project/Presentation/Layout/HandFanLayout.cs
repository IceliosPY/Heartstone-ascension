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
        [Tooltip("Radius of the circle the cards sit on. Larger means a flatter fan.")]
        public float PivotDistance = 7f;

        [Tooltip("Degrees between two neighbours before the fan reaches its limit.")]
        public float AnglePerCard = 6.5f;

        [Tooltip("Total spread the fan never exceeds, so a full hand stays on screen.")]
        public float MaxSpreadAngle = 38f;

        [Tooltip("Gap along the view direction, so overlapping cards stack predictably.")]
        public float DepthStep = 0.035f;

        public float Scale = 0.9f;
    }

    /// <summary>
    /// Works out where each card of a hand belongs.
    ///
    /// The cards sit on an arc rather than a straight line: each one is rotated
    /// a little further round a pivot well below the hand, so the middle card
    /// stands upright and the outer ones lean away. That single construction
    /// gives the position, the tilt and the vertical drop at once, and it is
    /// what makes a row of rectangles read as a hand of cards.
    ///
    /// The fan stops widening once it reaches its maximum spread, so a hand of
    /// ten overlaps rather than sliding off the table.
    ///
    /// Pure geometry, on purpose: it takes an index and a count and returns a
    /// pose, touching no scene object. The same maths will drive a card easing
    /// into place later without a line of it changing.
    /// </summary>
    public static class HandFanLayout
    {
        public static CardPose GetPose(int index, int count, HandFanSettings settings)
        {
            if (count <= 0)
            {
                return new CardPose(Vector3.zero, Quaternion.identity, settings.Scale);
            }

            // -0.5 for the leftmost card, +0.5 for the rightmost, 0 for one card.
            float centered = count == 1 ? 0f : (index / (float)(count - 1)) - 0.5f;

            float spread = Mathf.Min(settings.MaxSpreadAngle, settings.AnglePerCard * (count - 1));
            float angle = centered * spread;
            float radians = angle * Mathf.Deg2Rad;

            float x = Mathf.Sin(radians) * settings.PivotDistance;

            // Measured from the middle card, so the centre of the hand stays put
            // whatever the hand size.
            float y = (Mathf.Cos(radians) - 1f) * settings.PivotDistance;
            float z = index * settings.DepthStep;

            return new CardPose(
                new Vector3(x, y, z),
                Quaternion.Euler(0f, 0f, -angle),
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
