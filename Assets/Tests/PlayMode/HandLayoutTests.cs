using System.Collections;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// The hand rework: a card keeps its own <see cref="CardView"/> through
    /// everything that can happen to a hand, rather than the hand being torn
    /// down and rebuilt whenever it changes.
    ///
    /// <see cref="HandFanTests"/> already proves the fan's own geometry (centred,
    /// symmetric, compressing) and <see cref="OverlappingHandTests"/> already
    /// proves hover and sorting. What is missing, and what broke in the way the
    /// brief for this pass described, is identity across a change: whether the
    /// same card instance keeps the same view when a hand is drawn from, played
    /// from, or handed to the other seat.
    /// </summary>
    public sealed class HandLayoutTests : InteractionTestBase
    {
        private static Dictionary<EntityId, CardView> SnapshotHand(Player player, MatchPresenter presenter)
        {
            Dictionary<EntityId, CardView> snapshot = new Dictionary<EntityId, CardView>();

            foreach (CardInstance card in player.Hand)
            {
                if (presenter.TryGetCardView(card.Id, out CardView view))
                {
                    snapshot[card.Id] = view;
                }
            }

            return snapshot;
        }

        // ------------------------------------------------------------------
        //  Drawing
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Drawing_a_card_does_not_recreate_the_hand_it_was_drawn_into()
        {
            yield return LoadMatch();

            PlayerId acting = Session.State.CurrentPlayer;
            Dictionary<EntityId, CardView> before = SnapshotHand(Active, Presenter);

            // A full round trip: the acting player's own next turn, which is
            // the next time they draw.
            yield return RoundTrip();
            yield return HandAtRest();

            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(acting));
            Player active = Session.State.GetPlayer(acting);

            Assert.That(active.Hand.Count, Is.EqualTo(before.Count + 1),
                "A round trip should have drawn exactly one card.");

            foreach (KeyValuePair<EntityId, CardView> pair in before)
            {
                Assert.That(Presenter.TryGetCardView(pair.Key, out CardView still), Is.True,
                    "A card already in hand before the draw lost its view.");
                Assert.That(still, Is.SameAs(pair.Value),
                    "Drawing a card recreated a view for a card that was already in hand.");
            }
        }

        // ------------------------------------------------------------------
        //  Playing / removal
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Playing_a_card_does_not_recreate_the_views_left_behind()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            Dictionary<EntityId, CardView> before = SnapshotHand(Active, Presenter);
            CardView played = FirstPlayableCard();
            EntityId playedId = played.EntityId;

            List<Vector3> targetsBefore = new List<Vector3>();

            foreach (KeyValuePair<EntityId, CardView> pair in before)
            {
                if (pair.Key != playedId)
                {
                    targetsBefore.Add(pair.Value.RestingLocalPosition);
                }
            }

            yield return PlayOneMinionDirectly();
            yield return HandAtRest();

            Assert.That(Presenter.TryGetCardView(playedId, out CardView _), Is.False,
                "The played card is still tracked as a hand view.");

            List<Vector3> targetsAfter = new List<Vector3>();

            foreach (KeyValuePair<EntityId, CardView> pair in before)
            {
                if (pair.Key == playedId)
                {
                    continue;
                }

                Assert.That(Presenter.TryGetCardView(pair.Key, out CardView still), Is.True,
                    "A card that was not played lost its view when its neighbour was.");
                Assert.That(still, Is.SameAs(pair.Value),
                    "Playing one card recreated the view of a card left behind.");

                targetsAfter.Add(still.RestingLocalPosition);
            }

            // The gap the played card left has to actually close: the
            // remaining cards' own layout targets move, they do not stay
            // exactly where a wider hand put them.
            if (targetsBefore.Count > 1)
            {
                bool anyMoved = false;

                for (int index = 0; index < targetsBefore.Count; index++)
                {
                    if (Vector3.Distance(targetsBefore[index], targetsAfter[index]) > 0.01f)
                    {
                        anyMoved = true;
                        break;
                    }
                }

                Assert.That(anyMoved, Is.True,
                    "None of the remaining cards were given a new layout target after one was played.");
            }
        }

        // ------------------------------------------------------------------
        //  Player switch
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator A_player_switch_never_reuses_a_view_as_the_other_seats_card()
        {
            yield return LoadMatch();

            Dictionary<EntityId, CardView> playerOne = SnapshotHand(Active, Presenter);

            yield return EndTurn();
            yield return HandAtRest();

            Dictionary<EntityId, CardView> playerTwo = SnapshotHand(Active, Presenter);

            foreach (KeyValuePair<EntityId, CardView> pair in playerOne)
            {
                Assert.That(playerTwo.ContainsKey(pair.Key), Is.False,
                    "A player one card id turned up in player two's hand.");

                foreach (CardView other in playerTwo.Values)
                {
                    Assert.That(other, Is.Not.SameAs(pair.Value),
                        "The same CardView object is presenting a card for both seats at once.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Switching_back_restores_the_same_views_for_the_cards_still_in_hand()
        {
            yield return LoadMatch();

            Dictionary<EntityId, CardView> before = SnapshotHand(Active, Presenter);

            yield return RoundTrip();
            yield return HandAtRest();

            Dictionary<EntityId, CardView> after = SnapshotHand(Active, Presenter);

            foreach (KeyValuePair<EntityId, CardView> pair in before)
            {
                Assert.That(after.TryGetValue(pair.Key, out CardView still), Is.True,
                    "A card still in hand after the round trip lost its view.");
                Assert.That(still, Is.SameAs(pair.Value),
                    "Switching back to the original player rebuilt a view that did not need it.");
            }
        }

        // ------------------------------------------------------------------
        //  Turn entrance
        // ------------------------------------------------------------------

        /// <summary>
        /// The nudge <see cref="MatchFlowAnimations"/> gives the newly active
        /// hand, tested directly against <see cref="CardView"/> rather than
        /// against the timed coroutine that calls it: it starts a card below
        /// and smaller than the slot the layout already assigned it, and the
        /// card's own ordinary pose easing is what has to carry it back.
        /// </summary>
        [UnityTest]
        public IEnumerator Nudging_a_card_below_its_slot_settles_back_into_the_slot()
        {
            yield return LoadMatch();
            yield return HandAtRest();

            CardView card = FirstCardInHand();
            Vector3 slot = card.RestingLocalPosition;

            card.NudgeBelowRestingPose(0.4f, 0.85f);

            Assert.That(card.transform.localPosition.y, Is.LessThan(slot.y - 0.1f),
                "The nudge did not actually start the card below its slot.");

            yield return WaitUntil(() => Vector3.Distance(card.transform.localPosition, slot) < 0.01f);

            Assert.That(Vector3.Distance(card.transform.localPosition, slot), Is.LessThan(0.01f),
                "The card never settled back into the slot the layout gave it.");
        }

        // ------------------------------------------------------------------
        //  Drag cancellation
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Cancelling_a_drag_returns_the_card_to_its_own_slot()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            Vector3 slot = card.RestingLocalPosition;

            Press(card.transform.position);
            MoveTo(NearBoardRight);

            Assert.That(card.IsDragging, Is.True, "The card never started dragging.");

            Input.CancelForTests();
            yield return WaitUntil(() => !card.IsDragging);

            Assert.That(Presenter.TryGetCardView(card.EntityId, out CardView still), Is.True,
                "The cancelled card lost its view.");
            Assert.That(still, Is.SameAs(card), "The cancelled card came back as a different view.");

            yield return WaitUntil(() => Vector3.Distance(card.transform.localPosition, slot) < 0.02f);

            Assert.That(Vector3.Distance(card.transform.localPosition, slot), Is.LessThan(0.02f),
                "The card did not return to its own slot after the drag was cancelled.");
        }
    }
}
