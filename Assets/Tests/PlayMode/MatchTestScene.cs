using CoH.Presentation;
using UnityEngine;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Shared setup for anything that loads the match scene.
    ///
    /// Almost every test cares what a sequence produced and not how long it
    /// spent producing it, so they collapse every duration to zero. That is not
    /// a second code path: the same animations run, in the same order, and each
    /// tween applies its end state without waiting. The tests that are actually
    /// about timing turn it back on for themselves.
    /// </summary>
    public static class MatchTestScene
    {
        /// <summary>Collapses every presentation duration to zero.</summary>
        public static void MakeInstant()
        {
            PresentationTiming timing = Object.FindFirstObjectByType<PresentationTiming>();

            if (timing != null)
            {
                timing.SetInstant(true);
            }
        }

        /// <summary>Restores real durations, sped up so a test is not slow.</summary>
        public static PresentationTiming MakeFast(float speed = 8f)
        {
            PresentationTiming timing = Object.FindFirstObjectByType<PresentationTiming>();

            if (timing != null)
            {
                timing.SetInstant(false);
                timing.SetSpeed(speed);
            }

            return timing;
        }
    }
}
