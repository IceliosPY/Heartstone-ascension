using System;
using System.Collections;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Just enough tweening, and no more.
    ///
    /// Coroutines rather than a scheduler, because the presentation queue
    /// already sequences work by waiting on coroutines: a tween that is a
    /// coroutine composes with everything else for free, and "wait for this to
    /// finish" needs no machinery at all.
    ///
    /// Every one of them is driven by elapsed time, never by a per-frame
    /// increment, so nothing here runs at a different speed on a different
    /// machine. A duration of zero applies the end state and returns without
    /// yielding, which is what makes an instant presentation genuinely instant
    /// rather than merely fast: no frame is spent on it at all.
    /// </summary>
    public static class Tweens
    {
        /// <summary>Runs a normalised 0 to 1 value through an easing curve over time.</summary>
        public static IEnumerator Over(float duration, Func<float, float> ease, Action<float> apply)
        {
            ease ??= Easing.Linear;

            if (duration <= 0f)
            {
                apply(1f);
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                apply(ease(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            apply(1f);
        }

        public static IEnumerator MoveTo(
            Transform target, Vector3 destination, float duration, Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 from = target.position;

            yield return Over(duration, ease ?? Easing.Default, t =>
            {
                if (target != null)
                {
                    target.position = Vector3.LerpUnclamped(from, destination, t);
                }
            });
        }

        public static IEnumerator LocalMoveTo(
            Transform target, Vector3 destination, float duration, Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 from = target.localPosition;

            yield return Over(duration, ease ?? Easing.Default, t =>
            {
                if (target != null)
                {
                    target.localPosition = Vector3.LerpUnclamped(from, destination, t);
                }
            });
        }

        public static IEnumerator ScaleTo(
            Transform target, Vector3 destination, float duration, Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 from = target.localScale;

            yield return Over(duration, ease ?? Easing.Default, t =>
            {
                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(from, destination, t);
                }
            });
        }

        /// <summary>Position, rotation and scale together, which is how a card moves.</summary>
        public static IEnumerator PoseTo(
            Transform target, Vector3 position, Quaternion rotation, Vector3 scale,
            float duration, Func<float, float> ease = null)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 fromPosition = target.position;
            Quaternion fromRotation = target.rotation;
            Vector3 fromScale = target.localScale;

            yield return Over(duration, ease ?? Easing.Default, t =>
            {
                if (target == null)
                {
                    return;
                }

                target.SetPositionAndRotation(
                    Vector3.LerpUnclamped(fromPosition, position, t),
                    Quaternion.SlerpUnclamped(fromRotation, rotation, t));

                target.localScale = Vector3.LerpUnclamped(fromScale, scale, t);
            });
        }

        /// <summary>
        /// A quick recoil around wherever the target is standing, and back to
        /// exactly there. It reads the resting position once and restores it, so
        /// a shake never leaves a view a few millimetres from where it started.
        /// </summary>
        public static IEnumerator Shake(Transform target, float amount, float duration)
        {
            if (target == null || duration <= 0f || amount <= 0f)
            {
                yield break;
            }

            Vector3 resting = target.localPosition;

            // A fixed pattern rather than randomness: the presentation must not
            // touch the match's random source, and a recoil does not need to
            // differ from one hit to the next.
            yield return Over(duration, Easing.Linear, t =>
            {
                if (target == null)
                {
                    return;
                }

                float decay = 1f - t;
                float wobble = Mathf.Sin(t * Mathf.PI * 6f) * amount * decay;
                target.localPosition = resting + new Vector3(wobble, 0f, wobble * 0.4f);
            });

            if (target != null)
            {
                target.localPosition = resting;
            }
        }

        /// <summary>Waits, honouring a zero duration by not costing a frame.</summary>
        public static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
