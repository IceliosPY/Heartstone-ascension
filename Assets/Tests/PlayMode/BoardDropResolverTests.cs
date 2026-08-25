using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Where a dragged minion would land, as pure geometry.
    ///
    /// No scene and no engine: this is the one piece of the drag that is only
    /// arithmetic, and it is worth pinning down on its own so a failure
    /// elsewhere is never mistaken for a bad index.
    /// </summary>
    public sealed class BoardDropResolverTests
    {
        private const float Spacing = 1.2f;

        [Test]
        public void An_empty_board_always_takes_the_first_slot()
        {
            Assert.That(BoardDropResolver.Resolve(-5f, 0, Spacing), Is.Zero);
            Assert.That(BoardDropResolver.Resolve(0f, 0, Spacing), Is.Zero);
            Assert.That(BoardDropResolver.Resolve(5f, 0, Spacing), Is.Zero);
        }

        [Test]
        public void Pointing_left_of_everything_inserts_at_the_far_left()
        {
            Assert.That(BoardDropResolver.Resolve(-9f, 3, Spacing), Is.Zero);
        }

        [Test]
        public void Pointing_right_of_everything_appends()
        {
            Assert.That(BoardDropResolver.Resolve(9f, 3, Spacing), Is.EqualTo(3));
        }

        /// <summary>
        /// Three minions sit at -1.2, 0 and 1.2. Pointing just past one of them
        /// means going in after it.
        /// </summary>
        [Test]
        public void Pointing_between_two_minions_inserts_between_them()
        {
            Assert.That(BoardDropResolver.Resolve(-0.6f, 3, Spacing), Is.EqualTo(1),
                "Between the first and the second.");

            Assert.That(BoardDropResolver.Resolve(0.6f, 3, Spacing), Is.EqualTo(2),
                "Between the second and the third.");
        }

        [Test]
        public void Every_slot_of_a_full_row_can_be_pointed_at()
        {
            // Sweeping across the row has to offer all eight insertion points of
            // a board of seven, or some of them could never be chosen.
            bool[] reached = new bool[8];

            for (float x = -6f; x <= 6f; x += 0.01f)
            {
                reached[BoardDropResolver.Resolve(x, 7, Spacing)] = true;
            }

            for (int slot = 0; slot < reached.Length; slot++)
            {
                Assert.That(reached[slot], Is.True, "Slot " + slot + " cannot be pointed at.");
            }
        }

        [Test]
        public void A_held_open_slot_pushes_its_neighbours_apart()
        {
            // Two minions, a gap opening between them: they take slots 0 and 2
            // of a row of three, which is exactly where they end up once the
            // third arrives.
            Vector3 left = BoardDropResolver.PositionWithGap(0, 2, 1, Spacing);
            Vector3 right = BoardDropResolver.PositionWithGap(1, 2, 1, Spacing);
            Vector3 gap = BoardDropResolver.GapPosition(2, 1, Spacing);

            Assert.That(left.x, Is.LessThan(gap.x));
            Assert.That(gap.x, Is.LessThan(right.x));

            Assert.That(left.x, Is.EqualTo(BoardRowLayout.GetPosition(0, 3, Spacing).x).Within(0.0001f));
            Assert.That(gap.x, Is.EqualTo(BoardRowLayout.GetPosition(1, 3, Spacing).x).Within(0.0001f));
            Assert.That(right.x, Is.EqualTo(BoardRowLayout.GetPosition(2, 3, Spacing).x).Within(0.0001f));
        }

        [Test]
        public void With_no_gap_the_row_is_laid_out_normally()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                Assert.That(
                    BoardDropResolver.PositionWithGap(slot, 4, -1, Spacing),
                    Is.EqualTo(BoardRowLayout.GetPosition(slot, 4, Spacing)));
            }
        }
    }
}
