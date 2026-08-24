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
        [Tooltip("Distance between the leftmost and rightmost card when the hand is full.")]
        public float MaxWidth = 5.5f;

        [Tooltip("Spacing between two neighbours before the hand starts overlapping.")]
        public float PreferredSpacing = 0.95f;

        [Tooltip("Total rotation across the fan, in degrees.")]
        public float SpreadAngle = 16f;

        [Tooltip("How far the outer cards drop below the middle one.")]
        public float ArcDrop = 0.35f;

        [Tooltip("Gap between cards along the view direction, so they overlap predictably.")]
        public float DepthStep = 0.02f;

        public float Scale = 1f;
    }

    /// <summary>
    /// Works out where each card of a hand belongs.
    ///
    /// Pure geometry, and pure on purpose: it takes an index and a count and
    /// returns a pose. It touches no scene object, so the same maths will drive
    /// a card sliding smoothly into place once easing arrives, without a line of
    /// it changing.
    ///
    /// The fan narrows as the hand grows rather than stretching past the board,
    /// which is what keeps a hand of ten readable.
    /// </summary>
    public static class HandFanLayout
    {
        public static CardPose GetPose(int index, int count, HandFanSettings settings)
        {
            if (count <= 0)
            {
                return new CardPose(Vector3.zero, Quaternion.identity, settings.Scale);
            }

            // -0.5 for the leftmost card, +0.5 for the rightmost, 0 for a single one.
            float centered = count == 1 ? 0f : (index / (float)(count - 1)) - 0.5f;

            float width = Mathf.Min(settings.MaxWidth, settings.PreferredSpacing * (count - 1));

            float x = centered * width;
            float y = -Mathf.Abs(centered) * settings.ArcDrop;
            float z = index * settings.DepthStep;

            float roll = -centered * settings.SpreadAngle;

            return new CardPose(
                new Vector3(x, y, z),
                Quaternion.Euler(0f, 0f, roll),
                settings.Scale);
        }
    }

    /// <summary>
    /// Works out where each minion of a board row belongs.
    ///
    /// The index handed in is the index in the engine's board zone, so what a
    /// player sees left to right is exactly the order the rules use. Unity holds
    /// no second opinion about board order.
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
            float width = spacing * (count - 1);

            return new Vector3(centered * width, 0f, 0f);
        }
    }
}
