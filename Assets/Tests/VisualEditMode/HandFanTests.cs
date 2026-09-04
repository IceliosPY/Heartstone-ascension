using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The shape of a hand, at the sizes that actually stress it.
    ///
    /// Pure geometry, so no scene and no cards: the fan takes an index and a
    /// count and gives back a pose, and everything worth checking about a hand
    /// of ten is decided there. What the scene tests cover is that a card can
    /// still be hovered and dragged; what these cover is that there is anything
    /// left to hover.
    /// </summary>
    public sealed class HandFanTests
    {
        /// <summary>The settings the match scene wires into the presenter.</summary>
        private static HandFanSettings Hand() => new HandFanSettings
        {
            Scale = 1.56f,
            Spacing = 0.765f,
            MaxWidth = 7.56f,
            PivotDistance = 15.0f,
            DepthStep = 0.035f,
            SmallHandGenerosity = 1.2f
        };

        /// <summary>
        /// The left quarter of a card is its mana gem — canvas x 33 to 212 of
        /// 800 — and a hand where that is covered is a hand a player cannot read
        /// at a glance. Cards overlap from the right, so this is the strip that
        /// has to survive.
        /// </summary>
        private const float GemFraction = 212f / 800f;

        /// <summary>The deck piles stand at x 5.7, so the hand has to stay inside them.</summary>
        private const float Elbow = 5.0f;

        private static readonly int[] Sizes = { 1, 2, 3, 5, 6, 7, 10 };

        // ------------------------------------------------------------------
        //  Shape
        // ------------------------------------------------------------------

        [Test]
        public void A_single_card_sits_upright_in_the_middle()
        {
            CardPose only = HandFanLayout.GetPose(0, 1, Hand());

            Assert.That(only.LocalPosition.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(only.LocalPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(only.LocalRotation.eulerAngles.z, Is.EqualTo(0f).Within(0.01f),
                "A hand of one has nothing to fan against and should not lean.");
            Assert.That(only.Scale, Is.EqualTo(Hand().Scale));
        }

        [Test]
        public void The_fan_is_symmetric_at_every_size()
        {
            HandFanSettings hand = Hand();

            foreach (int count in Sizes)
            {
                CardPose left = HandFanLayout.GetPose(0, count, hand);
                CardPose right = HandFanLayout.GetPose(count - 1, count, hand);

                Assert.That(left.LocalPosition.x, Is.EqualTo(-right.LocalPosition.x).Within(0.0001f),
                    "A hand of " + count + " leans to one side.");
                Assert.That(left.LocalPosition.y, Is.EqualTo(right.LocalPosition.y).Within(0.0001f));
            }
        }

        [Test]
        public void Cards_run_left_to_right_and_stack_forward()
        {
            HandFanSettings hand = Hand();

            foreach (int count in Sizes)
            {
                for (int index = 1; index < count; index++)
                {
                    CardPose previous = HandFanLayout.GetPose(index - 1, count, hand);
                    CardPose current = HandFanLayout.GetPose(index, count, hand);

                    Assert.That(current.LocalPosition.x, Is.GreaterThan(previous.LocalPosition.x),
                        "In a hand of " + count + ", card " + index + " is not to the right of the one before it.");

                    // Later cards sit nearer the camera, so an overlapping hand
                    // reads as a fan rather than as a shuffle — and so the
                    // pointer, which takes whatever is nearest, agrees with the
                    // sorting group about which card is on top. The hand
                    // anchor's local +z points away from the viewer, so nearer
                    // is less.
                    Assert.That(current.LocalPosition.z, Is.LessThan(previous.LocalPosition.z),
                        "In a hand of " + count + ", card " + index + " is not nearer the viewer than its neighbour.");
                }
            }
        }

        [Test]
        public void The_same_hand_always_lays_out_the_same_way()
        {
            HandFanSettings hand = Hand();

            for (int index = 0; index < 10; index++)
            {
                CardPose first = HandFanLayout.GetPose(index, 10, hand);
                CardPose again = HandFanLayout.GetPose(index, 10, hand);

                Assert.That(again.LocalPosition, Is.EqualTo(first.LocalPosition));
                Assert.That(again.Scale, Is.EqualTo(first.Scale));
            }
        }

        // ------------------------------------------------------------------
        //  Overlap
        // ------------------------------------------------------------------

        /// <summary>
        /// The cards really do overlap. A hand whose cards merely sit beside
        /// each other is a row, and the whole point of the rework was that it
        /// stopped looking like one.
        /// </summary>
        [Test]
        public void From_two_cards_upward_they_overlap()
        {
            HandFanSettings hand = Hand();
            float cardWidth = hand.Scale;

            foreach (int count in Sizes)
            {
                if (count < 2)
                {
                    continue;
                }

                float gap = HandFanLayout.SpacingFor(count, hand);

                Assert.That(gap, Is.LessThan(cardWidth),
                    "A hand of " + count + " does not overlap at all.");
            }
        }

        /// <summary>
        /// Every card in the hand still shows its cost. This is the number that
        /// decides whether a card is playable at all, and a hand that hides it
        /// makes the player pick cards up to find out.
        /// </summary>
        [Test]
        public void Every_card_shows_its_mana_gem_at_one_five_and_ten()
        {
            HandFanSettings hand = Hand();
            float needed = GemFraction * hand.Scale;

            foreach (int count in Sizes)
            {
                if (count < 2)
                {
                    continue;
                }

                float gap = HandFanLayout.SpacingFor(count, hand);

                Assert.That(gap, Is.GreaterThanOrEqualTo(needed),
                    "In a hand of " + count + " each card covers the cost of the one before it: " +
                    gap.ToString("0.000") + " apart, and the gem needs " + needed.ToString("0.000") + ".");
            }
        }

        /// <summary>
        /// A fuller hand overlaps harder rather than reaching further. One
        /// expression does it — the spacing is the smaller of what is wanted and
        /// what fits — and this is that behaviour stated as a property.
        /// </summary>
        [Test]
        public void A_fuller_hand_tightens_instead_of_widening()
        {
            HandFanSettings hand = Hand();

            for (int count = 3; count <= 10; count++)
            {
                float wider = HandFanLayout.SpacingFor(count, hand);
                float narrower = HandFanLayout.SpacingFor(count - 1, hand);

                Assert.That(wider, Is.LessThanOrEqualTo(narrower + 0.0001f),
                    "A hand of " + count + " spaced its cards further apart than a hand of " + (count - 1) + ".");

                Assert.That(HandFanLayout.WidthOf(count, hand),
                    Is.LessThanOrEqualTo(hand.MaxWidth + 0.0001f),
                    "A hand of " + count + " grew past the width it is allowed.");
            }
        }

        /// <summary>
        /// A hand small enough that <see cref="HandFanSettings.MaxWidth"/> was
        /// never going to bind gets the baseline spacing at minimum - and a
        /// hand of two or three actually gets more than that, so it does not
        /// read as thin and stingy next to a fuller one. The boost is largest
        /// at two cards and fades to nothing by six, where the fan is already
        /// wide enough on its own.
        /// </summary>
        [Test]
        public void A_small_hand_is_spaced_at_least_as_generously_as_it_is_allowed()
        {
            HandFanSettings hand = Hand();
            float baseline = hand.Scale * hand.Spacing;
            float previous = float.PositiveInfinity;

            foreach (int count in new[] { 2, 3, 4, 5, 6 })
            {
                float spacing = HandFanLayout.SpacingFor(count, hand);

                Assert.That(spacing, Is.GreaterThanOrEqualTo(baseline - 0.0001f),
                    "A hand of " + count + " is being squeezed below the baseline before it needs to be.");

                Assert.That(spacing, Is.LessThanOrEqualTo(previous + 0.0001f),
                    "A hand of " + count + " is not more tightly spaced than a smaller one.");

                previous = spacing;
            }

            Assert.That(HandFanLayout.SpacingFor(6, hand), Is.EqualTo(baseline).Within(0.0001f),
                "By six cards the small-hand boost should have faded out entirely.");
        }

        // ------------------------------------------------------------------
        //  Bounds
        // ------------------------------------------------------------------

        [Test]
        public void A_full_hand_stays_between_the_deck_piles()
        {
            HandFanSettings hand = Hand();
            float halfCard = 0.5f * hand.Scale;

            foreach (int count in Sizes)
            {
                for (int index = 0; index < count; index++)
                {
                    float edge = Mathf.Abs(HandFanLayout.GetPose(index, count, hand).LocalPosition.x) + halfCard;

                    Assert.That(edge, Is.LessThanOrEqualTo(Elbow),
                        "A hand of " + count + " reaches " + edge.ToString("0.00") +
                        " from the middle, past the deck at " + Elbow + ".");
                }
            }
        }

        /// <summary>
        /// The outer cards lean, but not so far that reading them is work. A
        /// wider fan is bought with radius rather than with rotation, and this
        /// is the number that says so.
        /// </summary>
        [Test]
        public void No_card_leans_further_than_is_comfortable()
        {
            HandFanSettings hand = Hand();

            foreach (int count in Sizes)
            {
                for (int index = 0; index < count; index++)
                {
                    float lean = HandFanLayout.GetPose(index, count, hand).LocalRotation.eulerAngles.z;

                    // Euler angles come back in 0..360, so a small negative
                    // rotation reads as just under a full turn.
                    if (lean > 180f)
                    {
                        lean -= 360f;
                    }

                    Assert.That(Mathf.Abs(lean), Is.LessThanOrEqualTo(20f),
                        "In a hand of " + count + ", card " + index + " leans " +
                        lean.ToString("0.0") + " degrees.");
                }
            }
        }

        [Test]
        public void The_arc_dips_but_does_not_sag()
        {
            HandFanSettings hand = Hand();

            for (int index = 0; index < 10; index++)
            {
                float drop = HandFanLayout.GetPose(index, 10, hand).LocalPosition.y;

                Assert.That(drop, Is.LessThanOrEqualTo(0.0001f), "A card rose above the middle of the fan.");
                Assert.That(drop, Is.GreaterThan(-0.8f),
                    "The outer cards drop " + drop.ToString("0.00") + ", which is a sag rather than an arc.");
            }
        }

        // ------------------------------------------------------------------
        //  Growing it
        // ------------------------------------------------------------------

        /// <summary>
        /// Scaling the cards, the width they are allowed and the radius by one
        /// factor gives the same hand, larger — every position scaled, every
        /// angle untouched.
        ///
        /// Worth pinning down, because the next person to adjust the hand will
        /// reach for whichever number is nearest, and only all three together
        /// leave the arrangement alone.
        /// </summary>
        [Test]
        public void Growing_the_cards_the_width_and_the_arc_together_is_the_same_fan_larger()
        {
            HandFanSettings small = Hand();

            HandFanSettings large = new HandFanSettings
            {
                Scale = small.Scale * 1.5f,
                Spacing = small.Spacing,
                MaxWidth = small.MaxWidth * 1.5f,
                PivotDistance = small.PivotDistance * 1.5f,
                DepthStep = small.DepthStep
            };

            foreach (int count in Sizes)
            {
                for (int index = 0; index < count; index++)
                {
                    CardPose before = HandFanLayout.GetPose(index, count, small);
                    CardPose after = HandFanLayout.GetPose(index, count, large);

                    Assert.That(after.LocalPosition.x, Is.EqualTo(before.LocalPosition.x * 1.5f).Within(0.0001f));
                    Assert.That(after.LocalPosition.y, Is.EqualTo(before.LocalPosition.y * 1.5f).Within(0.0001f));
                    Assert.That(after.Scale, Is.EqualTo(before.Scale * 1.5f).Within(0.0001f));

                    float leanBefore = before.LocalRotation.eulerAngles.z;
                    float leanAfter = after.LocalRotation.eulerAngles.z;

                    Assert.That(leanAfter, Is.EqualTo(leanBefore).Within(0.01f),
                        "The tilt changed, so this is a different fan rather than a bigger one.");
                }
            }
        }

        /// <summary>
        /// Spacing and lean are independent, which is the whole reason the fan
        /// was rewritten this way round. Flattening the arc must not move a card
        /// sideways, and spreading the cards must not change how far they lean
        /// per unit of distance.
        /// </summary>
        [Test]
        public void The_radius_changes_the_lean_and_nothing_else()
        {
            HandFanSettings flat = Hand();
            HandFanSettings curved = Hand();
            curved.PivotDistance = flat.PivotDistance * 0.5f;

            for (int index = 0; index < 10; index++)
            {
                CardPose a = HandFanLayout.GetPose(index, 10, flat);
                CardPose b = HandFanLayout.GetPose(index, 10, curved);

                Assert.That(b.LocalPosition.x, Is.EqualTo(a.LocalPosition.x).Within(0.0001f),
                    "Flattening the arc moved a card sideways.");
            }

            float flatLean = Mathf.Abs(Mathf.DeltaAngle(0f, HandFanLayout.GetPose(9, 10, flat).LocalRotation.eulerAngles.z));
            float curvedLean = Mathf.Abs(Mathf.DeltaAngle(0f, HandFanLayout.GetPose(9, 10, curved).LocalRotation.eulerAngles.z));

            Assert.That(curvedLean, Is.GreaterThan(flatLean),
                "A tighter radius should lean the outer cards further.");
        }

        // ------------------------------------------------------------------
        //  Adapting to hand size
        // ------------------------------------------------------------------

        private static float OuterLean(int count, HandFanSettings settings) =>
            Mathf.Abs(Mathf.DeltaAngle(
                0f, HandFanLayout.GetPose(count - 1, count, settings).LocalRotation.eulerAngles.z));

        /// <summary>
        /// A hand of two or three lies on exactly the plain baseline radius -
        /// per the reference, a small hand reads as close to upright, and
        /// what actually sets it apart from the baseline is the extra room
        /// <see cref="SpacingFor"/> gives it, never a curve of its own. Read
        /// straight off the angle the geometry itself implies (asin of x over
        /// the plain <see cref="HandFanSettings.PivotDistance"/>) rather than
        /// compared against a second settings object, since there is no
        /// longer a small-hand radius factor to switch off.
        /// </summary>
        [Test]
        public void A_small_hand_lies_on_the_plain_baseline_radius()
        {
            HandFanSettings hand = Hand();

            foreach (int count in new[] { 2, 3 })
            {
                CardPose outer = HandFanLayout.GetPose(count - 1, count, hand);
                float expected = Mathf.Asin(Mathf.Clamp(outer.LocalPosition.x / hand.PivotDistance, -1f, 1f)) * Mathf.Rad2Deg;

                Assert.That(OuterLean(count, hand), Is.EqualTo(expected).Within(0.01f),
                    "A hand of " + count + " does not lie on the plain baseline radius.");
            }
        }

        /// <summary>
        /// Hearthstone's own large-hand behaviour: more cards means more
        /// overlap, never a wider fan or a deeper curve. Each of these pairs
        /// is spaced strictly less generously than the one before it, which
        /// is the whole difference between seven, eight and ten cards - not
        /// how far any of them lean.
        /// </summary>
        [TestCase(2, 5)]
        [TestCase(5, 8)]
        [TestCase(8, 10)]
        public void A_fuller_hand_spaces_its_cards_less_generously(int fewer, int more)
        {
            HandFanSettings hand = Hand();

            Assert.That(HandFanLayout.SpacingFor(more, hand),
                Is.LessThan(HandFanLayout.SpacingFor(fewer, hand)),
                "A hand of " + more + " is not spaced any tighter than a hand of " + fewer + ".");
        }

        /// <summary>
        /// The hand's total footprint grows a lot while it still has room to
        /// - two to six cards - and then almost stops growing once it has
        /// reached <see cref="HandFanSettings.MaxWidth"/>. Seven to ten
        /// cards must occupy roughly the same width; the only thing an
        /// eighth, ninth or tenth card can still do is overlap harder.
        /// </summary>
        [Test]
        public void The_hand_s_width_saturates_instead_of_growing_with_every_card()
        {
            HandFanSettings hand = Hand();

            float growthWhileRoomy = HandFanLayout.WidthOf(6, hand) - HandFanLayout.WidthOf(2, hand);
            float growthOnceFull = HandFanLayout.WidthOf(10, hand) - HandFanLayout.WidthOf(7, hand);

            Assert.That(growthWhileRoomy, Is.GreaterThan(1f),
                "The hand barely widened at all from two cards to six.");
            Assert.That(growthOnceFull, Is.LessThan(growthWhileRoomy * 0.2f),
                "The hand kept widening well past six cards instead of saturating.");
        }

        /// <summary>
        /// Ten cards overlap far more than five - the visible sign of the
        /// compression above, stated the way a player would actually read it
        /// on screen: how much of the card behind is left showing.
        /// </summary>
        [Test]
        public void A_hand_of_ten_overlaps_far_more_than_a_hand_of_five()
        {
            HandFanSettings hand = Hand();

            float overlapAtFive = hand.Scale - HandFanLayout.SpacingFor(5, hand);
            float overlapAtTen = hand.Scale - HandFanLayout.SpacingFor(10, hand);

            Assert.That(overlapAtTen, Is.GreaterThan(overlapAtFive * 1.5f),
                "A hand of ten does not overlap substantially more than a hand of five.");
        }

        /// <summary>
        /// Seven cards and ten cards lean by nearly the same amount: once the
        /// width has saturated, the outer cards' angle is already set, and a
        /// few more cards squeezed into the same footprint must not also
        /// swing the fan open further. A denser hand of seven, not a wider
        /// semicircle.
        /// </summary>
        [Test]
        public void Seven_to_ten_cards_lean_by_about_the_same_amount()
        {
            HandFanSettings hand = Hand();

            float sevenLean = OuterLean(7, hand);
            float tenLean = OuterLean(10, hand);

            Assert.That(Mathf.Abs(tenLean - sevenLean), Is.LessThan(2f),
                "A hand of ten leans " + (tenLean - sevenLean).ToString("0.0") +
                " degrees further than a hand of seven, which is too close to a wider fan.");
        }

        /// <summary>
        /// A single card is never touched by any of this: it sits dead centre
        /// with no lean regardless of how generous or curved a small hand is
        /// configured to be, because it has nothing to fan against.
        /// </summary>
        [Test]
        public void A_single_card_ignores_the_small_hand_settings()
        {
            HandFanSettings hand = Hand();
            CardPose only = HandFanLayout.GetPose(0, 1, hand);

            Assert.That(only.LocalPosition.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(OuterLean(1, hand), Is.EqualTo(0f).Within(0.01f));
        }
    }
}
