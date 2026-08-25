using System;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The handful of curves this game moves on.
    ///
    /// Four, deliberately. Almost everything wants <see cref="OutQuad"/>, which
    /// starts fast and settles, because that is what reads as a thing arriving
    /// rather than a thing being slid. <see cref="OutBack"/> overshoots a little
    /// and is what makes a summon feel like it landed. Anything that needs a
    /// fifth curve probably needs a rethink instead.
    /// </summary>
    public static class Easing
    {
        public static float Linear(float t) => t;

        /// <summary>Fast, then settling. The default for anything travelling.</summary>
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Eases at both ends. For moves with no impact at the end.</summary>
        public static float InOutQuad(float t) =>
            t < 0.5f ? 2f * t * t : 1f - ((-2f * t + 2f) * (-2f * t + 2f) / 2f);

        /// <summary>Overshoots and comes back. A landing, not a slide.</summary>
        public static float OutBack(float t)
        {
            const float overshoot = 1.70158f;
            float shifted = t - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        /// <summary>Rises to one at the halfway point and falls back. For flashes.</summary>
        public static float Pulse(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

        public static Func<float, float> Default => OutQuad;
    }
}
