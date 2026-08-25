using System.Collections;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// The pieces every animation is built from, on their own.
    ///
    /// Worth pinning down separately because a fault here would look like a
    /// fault in whichever animation happened to be running, and because a
    /// duration of zero has to genuinely cost nothing: that is what an instant
    /// presentation rests on, and what keeps a test suite from waiting on
    /// animations it does not care about.
    /// </summary>
    public sealed class AnimationPrimitiveTests
    {
        [Test]
        public void Every_easing_starts_at_zero_and_ends_at_one()
        {
            Assert.That(Easing.Linear(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Easing.Linear(1f), Is.EqualTo(1f).Within(0.0001f));

            Assert.That(Easing.OutQuad(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Easing.OutQuad(1f), Is.EqualTo(1f).Within(0.0001f));

            Assert.That(Easing.InOutQuad(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Easing.InOutQuad(1f), Is.EqualTo(1f).Within(0.0001f));

            Assert.That(Easing.OutBack(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Easing.OutBack(1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void OutQuad_covers_most_of_the_distance_early()
        {
            // What makes a move read as arriving rather than sliding.
            Assert.That(Easing.OutQuad(0.5f), Is.GreaterThan(0.7f));
        }

        [Test]
        public void OutBack_overshoots_before_it_settles()
        {
            bool overshot = false;

            for (float t = 0.5f; t < 1f; t += 0.01f)
            {
                if (Easing.OutBack(t) > 1f)
                {
                    overshot = true;
                }
            }

            Assert.That(overshot, Is.True, "OutBack never goes past its target, so nothing lands.");
        }

        [Test]
        public void Pulse_rises_and_falls()
        {
            Assert.That(Easing.Pulse(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Easing.Pulse(0.5f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Easing.Pulse(1f), Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>
        /// A zero duration must apply the end state and finish, without ever
        /// yielding. Anything else and an instant presentation would still cost
        /// one frame per event.
        /// </summary>
        [Test]
        public void A_zero_duration_tween_finishes_without_yielding()
        {
            float value = -1f;
            IEnumerator routine = Tweens.Over(0f, Easing.OutQuad, t => value = t);

            Assert.That(routine.MoveNext(), Is.False, "A zero duration tween yielded.");
            Assert.That(value, Is.EqualTo(1f), "A zero duration tween did not apply its end state.");
        }

        [Test]
        public void A_zero_wait_finishes_without_yielding()
        {
            Assert.That(Tweens.Wait(0f).MoveNext(), Is.False);
            Assert.That(Tweens.Wait(-1f).MoveNext(), Is.False);
        }

        [Test]
        public void A_real_duration_tween_does_yield()
        {
            IEnumerator routine = Tweens.Over(1f, Easing.Linear, _ => { });
            Assert.That(routine.MoveNext(), Is.True, "A real tween finished without waiting at all.");
        }

        [Test]
        public void Timings_scale_with_speed_and_collapse_when_instant()
        {
            GameObject host = new GameObject("timing");

            try
            {
                PresentationTiming timing = host.AddComponent<PresentationTiming>();

                float normal = timing.Death;
                Assert.That(normal, Is.GreaterThan(0f));

                timing.SetSpeed(2f);
                Assert.That(timing.Death, Is.EqualTo(normal / 2f).Within(0.0001f),
                    "Speed has to divide every duration, not just some.");

                timing.SetInstant(true);
                Assert.That(timing.IsInstant, Is.True);
                Assert.That(timing.Death, Is.Zero);
                Assert.That(timing.CardDraw, Is.Zero);
                Assert.That(timing.TurnBanner, Is.Zero);
                Assert.That(timing.AttackTravel, Is.Zero);
                Assert.That(timing.GameEndReveal, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// A shake has to put the transform back exactly, or a minion drifts a
        /// little further from its slot every time it is hit.
        /// </summary>
        [Test]
        public void A_shake_returns_the_transform_to_where_it_started()
        {
            GameObject host = new GameObject("shaken");

            try
            {
                host.transform.localPosition = new Vector3(1.5f, 0.25f, -3f);
                Vector3 before = host.transform.localPosition;

                IEnumerator routine = Tweens.Shake(host.transform, 0.2f, 0f);

                while (routine.MoveNext())
                {
                }

                Assert.That(Vector3.Distance(host.transform.localPosition, before), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
