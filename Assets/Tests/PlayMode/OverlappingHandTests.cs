using System.Collections;
using System.Collections.Generic;
using CoH.Core.Identifiers;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// A hand whose cards cover each other, and the pointer that still has to
    /// pick the right one.
    ///
    /// Overlap is the point of a fan and the risk of one. Every card but the
    /// last has a neighbour standing in front of its right-hand side, so
    /// "click the card" stops being obvious and starts being a rule: the
    /// pointer takes whatever is nearest, and the fan puts later cards nearer.
    /// These check that rule holds where it now matters.
    /// </summary>
    public sealed class OverlappingHandTests : InteractionTestBase
    {
        /// <summary>
        /// A point on a card's own top left, where its cost is printed.
        ///
        /// Cards overlap from the right, so this strip belongs to the card
        /// itself whatever else is in the hand — which is exactly why the fan
        /// guarantees it stays uncovered.
        /// </summary>
        private static Vector3 WhereItsCostIs(CardView card) =>
            card.transform.TransformPoint(new Vector3(-0.3f, 0.34f, -0.02f));

        private List<CardView> Hand()
        {
            List<CardView> hand = new List<CardView>();

            foreach (CoH.Core.State.CardInstance card in Active.Hand)
            {
                if (Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    hand.Add(view);
                }
            }

            return hand;
        }

        // ------------------------------------------------------------------
        //  Picking one out
        // ------------------------------------------------------------------

        /// <summary>
        /// Every card in the hand can be hovered, at the one place the fan
        /// promises is its own. A card nobody can point at is a card nobody can
        /// play, however good the hand looks.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_card_in_an_overlapping_hand_can_be_pointed_at()
        {
            yield return LoadMatch();

            List<CardView> hand = Hand();

            Assert.That(hand.Count, Is.GreaterThan(2), "The match dealt too few cards to overlap.");

            for (int index = 0; index < hand.Count; index++)
            {
                MoveTo(WhereItsCostIs(hand[index]));
                yield return null;

                Assert.That(hand[index].IsHovered, Is.True,
                    "Card " + index + " of " + hand.Count + " (" + hand[index].EntityId +
                    ", order " + hand[index].DrawOrder + ") could not be pointed at. " +
                    "The ray met " + Input.LastHit + ".");
            }
        }

        /// <summary>
        /// And a press picks up the card that was pointed at, rather than the
        /// neighbour lying across it.
        /// </summary>
        [UnityTest]
        public IEnumerator Pressing_a_card_picks_up_that_card_and_not_its_neighbour()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            List<CardView> hand = Hand();

            for (int index = 0; index < hand.Count; index++)
            {
                CardView wanted = hand[index];

                if (!wanted.IsPlayable)
                {
                    continue;
                }

                EntityId id = wanted.EntityId;

                Press(WhereItsCostIs(wanted));

                Assert.That(Input.HeldEntity, Is.EqualTo(id),
                    "Pressing card " + index + " picked up a different one.");

                Input.CancelForTests();
                yield return null;
            }
        }

        // ------------------------------------------------------------------
        //  Hover
        // ------------------------------------------------------------------

        /// <summary>
        /// Hovering reveals a card rather than merely enlarging it: it rises out
        /// of the fan and comes forward of everything beside it. With the hand
        /// overlapping this is what makes a card readable at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Hovering_lifts_a_card_clear_of_the_hand_and_brings_it_forward()
        {
            yield return LoadMatch();

            List<CardView> hand = Hand();
            Assert.That(hand.Count, Is.GreaterThan(2));

            // One in the middle, which is the most buried.
            CardView card = hand[hand.Count / 2];

            Vector3 resting = card.transform.position;

            MoveTo(WhereItsCostIs(card));
            Assert.That(card.IsHovered, Is.True);

            yield return WaitUntil(() => card.transform.position.y > resting.y + 0.2f);

            Vector3 raised = card.transform.position;

            Assert.That(raised.y, Is.GreaterThan(resting.y + 0.2f),
                "A hovered card barely rose out of the hand.");

            // And toward the camera, which is what gives it a little perspective
            // over the rest of the hand.
            Assert.That(raised.z, Is.LessThan(resting.z),
                "A hovered card did not come forward at all.");

            // Whether it is actually in front is a question about draw order,
            // not about depth — which is the whole lesson of the bug this test
            // was written alongside. The hand lies on a tilted arc, so a card at
            // the end of it sits nearer the camera than one in the middle
            // without being drawn over it. Cards_draw_in_the_order... and
            // A_hovered_card_draws_in_front_of_every_other_card cover that.
            for (int index = 0; index < hand.Count; index++)
            {
                if (hand[index] != card)
                {
                    Assert.That(card.DrawOrder, Is.GreaterThan(hand[index].DrawOrder),
                        "Another card still draws over the one being read.");
                }
            }
        }

        [UnityTest]
        public IEnumerator A_card_returns_to_exactly_where_it_was_when_the_pointer_leaves()
        {
            yield return LoadMatch();

            List<CardView> hand = Hand();
            CardView card = hand[hand.Count / 2];

            Vector3 resting = card.RestingLocalPosition;

            MoveTo(WhereItsCostIs(card));
            yield return WaitUntil(() => card.IsHovered);

            MoveTo(EmptySpace);
            yield return WaitUntil(() => !card.IsHovered);
            yield return WaitUntil(() =>
                Vector3.Distance(card.transform.localPosition, resting) < 0.02f);

            Assert.That(Vector3.Distance(card.transform.localPosition, resting), Is.LessThan(0.02f),
                "A card drifted from where the fan put it after being hovered.");
        }

        // ------------------------------------------------------------------
        //  Draw order
        // ------------------------------------------------------------------

        /// <summary>
        /// A card draws as one card.
        ///
        /// Its layers carry sorting orders from the backdrop up to the last
        /// label, and those are global: without a sorting group, every card's
        /// frame is order twenty and every card's name is order a hundred and
        /// thirty, so a neighbour's name paints over this card's frame no
        /// matter which is nearer. That was the hover bug, and this is the
        /// property that fixed it.
        /// </summary>
        [UnityTest]
        public IEnumerator Cards_draw_in_the_order_they_sit_in_the_hand()
        {
            yield return LoadMatch();

            List<CardView> hand = Hand();
            Assert.That(hand.Count, Is.GreaterThan(2));

            for (int index = 1; index < hand.Count; index++)
            {
                Assert.That(hand[index].DrawOrder, Is.GreaterThan(hand[index - 1].DrawOrder),
                    "Card " + index + " does not draw in front of the one to its left.");
            }
        }

        [UnityTest]
        public IEnumerator A_hovered_card_draws_in_front_of_every_other_card()
        {
            yield return LoadMatch();

            List<CardView> hand = Hand();

            // The most buried one, which is the case that matters.
            CardView card = hand[hand.Count / 2];
            int resting = card.DrawOrder;

            MoveTo(WhereItsCostIs(card));
            yield return null;

            Assert.That(card.IsHovered, Is.True);
            Assert.That(card.DrawOrder, Is.GreaterThan(resting));

            for (int index = 0; index < hand.Count; index++)
            {
                if (hand[index] != card)
                {
                    Assert.That(card.DrawOrder, Is.GreaterThan(hand[index].DrawOrder),
                        "Card " + index + " still draws over the one being read.");
                }
            }
        }

        [UnityTest]
        public IEnumerator A_card_takes_its_place_back_in_the_order_when_the_pointer_leaves()
        {
            yield return LoadMatch();

            List<CardView> hand = Hand();
            CardView card = hand[hand.Count / 2];

            int resting = card.DrawOrder;

            MoveTo(WhereItsCostIs(card));
            yield return null;
            Assert.That(card.DrawOrder, Is.Not.EqualTo(resting));

            MoveTo(EmptySpace);
            yield return null;

            Assert.That(card.DrawOrder, Is.EqualTo(resting),
                "A card kept the order it was given while it was being read.");
        }

        /// <summary>
        /// A card being carried is in front of the whole hand, so it is never
        /// half behind the cards it is being dragged across.
        /// </summary>
        [UnityTest]
        public IEnumerator A_carried_card_draws_in_front_of_the_hand_it_came_from()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            List<CardView> hand = Hand();

            Press(WhereItsCostIs(card));
            MoveTo(NearBoardRight);

            Assert.That(card.IsDragging, Is.True);

            for (int index = 0; index < hand.Count; index++)
            {
                if (hand[index] != card)
                {
                    Assert.That(card.DrawOrder, Is.GreaterThan(hand[index].DrawOrder),
                        "A card in the hand draws over the one being carried.");
                }
            }

            Input.CancelForTests();
            yield return null;
        }

        // ------------------------------------------------------------------
        //  Both seats
        // ------------------------------------------------------------------

        /// <summary>
        /// The fan is geometry and knows nothing about whose turn it is, so the
        /// second seat gets the same hand. Worth a test anyway: this is where a
        /// screen-to-world mistake would surface.
        /// </summary>
        [UnityTest]
        public IEnumerator The_second_seat_gets_the_same_hand_as_the_first()
        {
            yield return LoadMatch();

            List<Vector3> first = Positions(Hand());

            yield return EndTurn();

            List<CardView> second = Hand();

            Assert.That(Presenter.NearHero.PlayerId, Is.EqualTo(Session.State.CurrentPlayer),
                "The near hero is not the player whose turn it is.");

            // Both hands are laid out by the same fan, so a hand of the same
            // size is in the same places.
            if (second.Count == first.Count)
            {
                List<Vector3> after = Positions(second);

                for (int index = 0; index < after.Count; index++)
                {
                    Assert.That(after[index].x, Is.EqualTo(first[index].x).Within(0.001f),
                        "The second seat's fan is a different shape.");
                }
            }

            // And it can still be pointed at, and still draws in hand order.
            for (int index = 0; index < second.Count; index++)
            {
                MoveTo(WhereItsCostIs(second[index]));
                yield return null;

                Assert.That(second[index].IsHovered, Is.True,
                    "Card " + index + " could not be pointed at from the second seat.");

                if (index > 0)
                {
                    Assert.That(second[index].DrawOrder, Is.GreaterThan(second[index - 1].DrawOrder),
                        "The second seat's hand draws in a different order.");
                }
            }
        }

        private static List<Vector3> Positions(List<CardView> hand)
        {
            List<Vector3> places = new List<Vector3>();

            for (int index = 0; index < hand.Count; index++)
            {
                places.Add(hand[index].RestingLocalPosition);
            }

            return places;
        }
    }
}
