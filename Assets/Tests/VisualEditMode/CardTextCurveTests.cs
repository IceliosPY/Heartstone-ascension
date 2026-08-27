using System.Reflection;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The baseline described as an arch, and the one rule that keeps that
    /// description honest: there is still only one curve.
    ///
    /// Three friendly numbers over three control points is a pleasant way to
    /// adjust a title and a very easy way to end up with two records of the same
    /// shape, quietly disagreeing. So nothing stores an amount, a tilt or a
    /// centre: they are computed from the control points when they are shown and
    /// turned back into control points when they are moved. These check that the
    /// round trip holds, that each of the three does what its name says, and that
    /// a curve the three cannot describe is reported rather than silently
    /// flattened.
    /// </summary>
    public sealed class CardTextCurveTests
    {
        /// <summary>
        /// How far the baseline strays from its chord, and how far across.
        ///
        /// Measured against the curve's parameter rather than its x, because
        /// that is what the warp reads: it asks for the baseline at a fraction
        /// of the way across the title and hands that fraction straight in,
        /// never looking at the x it gets back.
        /// </summary>
        private static void Arch(
            Vector2 controlA, Vector2 controlB, Vector2 end,
            out float depth, out float across)
        {
            depth = 0f;
            across = 0.5f;

            for (int step = 1; step < 400; step++)
            {
                float t = step / 400f;
                float u = 1f - t;

                float y = 3f * u * u * t * controlA.y +
                          3f * u * t * t * controlB.y +
                          t * t * t * end.y;

                // Upward is negative, so a deeper arch is a more negative stray.
                float strayed = y - end.y * t;

                if (strayed < depth)
                {
                    depth = strayed;
                    across = t;
                }
            }

            depth = -depth;
        }

        // ------------------------------------------------------------------
        //  There and back
        // ------------------------------------------------------------------

        [Test]
        public void An_arch_reads_back_as_the_numbers_it_was_built_from()
        {
            float[] amounts = { 0f, 0.04f, 0.08f, 0.15f };
            float[] tilts = { -0.1f, 0f, 0.06f };
            float[] centres = { 0.4f, 0.5f, 0.6f };

            foreach (float amount in amounts)
            {
                foreach (float tilt in tilts)
                {
                    foreach (float centre in centres)
                    {
                        new CardTextCurve(amount, tilt, centre)
                            .ToControls(out Vector2 a, out Vector2 b, out Vector2 end);

                        CardTextCurve read = CardTextCurve.From(a, b, end);

                        string what = "amount " + amount + ", tilt " + tilt + ", centre " + centre;

                        Assert.That(read.Amount, Is.EqualTo(amount).Within(0.0005f), what);
                        Assert.That(read.Tilt, Is.EqualTo(tilt).Within(0.0005f), what);

                        // A flat baseline has no top, so its centre means
                        // nothing and is not worth asserting.
                        if (Mathf.Abs(amount) > 0.01f)
                        {
                            Assert.That(read.Centre, Is.EqualTo(centre).Within(0.01f), what);
                        }
                    }
                }
            }
        }

        [Test]
        public void An_arch_built_from_these_numbers_is_one_they_can_describe()
        {
            new CardTextCurve(0.09f, 0.03f, 0.45f)
                .ToControls(out Vector2 a, out Vector2 b, out Vector2 end);

            Assert.That(CardTextCurve.Fits(a, b, end), Is.True);
        }

        // ------------------------------------------------------------------
        //  What each of the three does
        // ------------------------------------------------------------------

        [Test]
        public void Curve_amount_is_how_deep_the_arch_is()
        {
            float[] wanted = { 0.03f, 0.07f, 0.14f };
            float previous = -1f;

            foreach (float amount in wanted)
            {
                new CardTextCurve(amount, 0f, 0.5f)
                    .ToControls(out Vector2 a, out Vector2 b, out Vector2 end);

                Arch(a, b, end, out float depth, out _);

                Assert.That(depth, Is.EqualTo(amount).Within(0.005f),
                    "An arch asked for " + amount + " came out " + depth + " deep.");

                Assert.That(depth, Is.GreaterThan(previous), "Deeper did not get deeper.");
                previous = depth;
            }
        }

        [Test]
        public void Curve_tilt_raises_one_end_and_leaves_the_arch_alone()
        {
            new CardTextCurve(0.08f, 0f, 0.5f)
                .ToControls(out Vector2 flatA, out Vector2 flatB, out Vector2 flatEnd);

            Arch(flatA, flatB, flatEnd, out float depth, out _);

            new CardTextCurve(0.08f, -0.06f, 0.5f)
                .ToControls(out Vector2 a, out Vector2 b, out Vector2 end);

            Assert.That(end.y, Is.EqualTo(-0.06f).Within(0.0001f),
                "Tilting did not move the end of the baseline.");

            Assert.That(flatEnd.y, Is.EqualTo(0f).Within(0.0001f));

            Arch(a, b, end, out float tiltedDepth, out _);

            Assert.That(tiltedDepth, Is.EqualTo(depth).Within(0.005f),
                "Tilting the baseline changed how deep its arch is.");
        }

        [Test]
        public void Curve_centre_moves_the_top_of_the_arch_across()
        {
            float[] wanted = { 0.4f, 0.5f, 0.6f };
            float previous = -1f;

            foreach (float centre in wanted)
            {
                new CardTextCurve(0.09f, 0f, centre)
                    .ToControls(out Vector2 a, out Vector2 b, out Vector2 end);

                Arch(a, b, end, out float depth, out float top);

                Assert.That(top, Is.EqualTo(centre).Within(0.01f),
                    "The top of an arch centred at " + centre + " came out at " + top + ".");

                Assert.That(top, Is.GreaterThan(previous), "Moving the centre right moved it left.");
                previous = top;

                // And moving it does not quietly change how deep the arch is.
                Assert.That(depth, Is.EqualTo(0.09f).Within(0.002f),
                    "Moving the top of the arch changed its depth.");
            }
        }

        /// <summary>
        /// And a cubic with one hump can only reach so far off centre. Past a
        /// third either way it would have to bend back on itself, which is a
        /// different shape rather than more of this one, so the ends of the
        /// range are held rather than exceeded.
        /// </summary>
        [Test]
        public void The_top_of_the_arch_stays_within_what_one_hump_can_do()
        {
            new CardTextCurve(0.09f, 0f, 0.05f)
                .ToControls(out Vector2 a, out Vector2 b, out Vector2 end);

            Arch(a, b, end, out _, out float top);

            Assert.That(top, Is.GreaterThanOrEqualTo(CardTextCurve.NearestCentre - 0.02f));

            new CardTextCurve(0.09f, 0f, 0.95f)
                .ToControls(out a, out b, out end);

            Arch(a, b, end, out _, out top);

            Assert.That(top, Is.LessThanOrEqualTo(CardTextCurve.FurthestCentre + 0.02f));
        }

        // ------------------------------------------------------------------
        //  What they cannot say
        // ------------------------------------------------------------------

        /// <summary>
        /// The minion banner rises at one end and falls at the other, which no
        /// single arch can do. That has to be reported: a tool that showed the
        /// nearest arch without saying so would flatten a measured shape the
        /// first time anybody nudged a slider.
        /// </summary>
        [Test]
        public void An_S_shaped_baseline_is_reported_as_one_the_numbers_cannot_describe()
        {
            Vector2 controlA = new Vector2(0.1179f, 0.0912f);
            Vector2 controlB = new Vector2(0.7208f, -0.1856f);
            Vector2 end = new Vector2(1f, 0.0016f);

            Assert.That(CardTextCurve.Fits(controlA, controlB, end), Is.False,
                "A baseline that rises and then falls was reported as a plain arch.");
        }

        /// <summary>
        /// And the arch the renderer's spell banner uses is one they can.
        /// </summary>
        [Test]
        public void The_spell_banner_is_an_arch_the_numbers_can_describe()
        {
            Vector2 controlA = new Vector2(0f, 0f);
            Vector2 controlB = new Vector2(0.4851f, -0.1987f);
            Vector2 end = new Vector2(1f, 0f);

            Assert.That(CardTextCurve.Fits(controlA, controlB, end), Is.True,
                "The spell banner is a plain arch and should be describable.");

            CardTextCurve curve = CardTextCurve.From(controlA, controlB, end);

            Assert.That(curve.Amount, Is.EqualTo(0.088f).Within(0.005f));
            Assert.That(curve.Tilt, Is.EqualTo(0f).Within(0.001f));

            // Its top sits at two thirds, which is as far off centre as one hump
            // reaches — the spell banner is right at the edge of the family.
            Assert.That(curve.Centre, Is.EqualTo(CardTextCurve.FurthestCentre).Within(0.02f));
        }

        // ------------------------------------------------------------------
        //  One curve, not two
        // ------------------------------------------------------------------

        /// <summary>
        /// Nothing stores an amount, a tilt or a centre.
        ///
        /// The whole risk of a friendlier set of numbers over a harder one is
        /// that both get written down and then disagree. The control points are
        /// the curve; these three are a way of talking about them.
        /// </summary>
        [Test]
        public void The_style_stores_control_points_and_nothing_else_about_the_curve()
        {
            FieldInfo[] fields = typeof(CardTextStyleDefinition)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                string name = field.Name.ToLowerInvariant();

                Assert.That(
                    name.Contains("amount") || name.Contains("tilt") || name.Contains("centre") ||
                    name.Contains("center"),
                    Is.False,
                    "The style stores '" + field.Name + "' as well as its control points, so the " +
                    "curve is written down twice and the two can disagree.");
            }
        }
    }
}
