using System.Collections;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Looking at a card, picking it up, putting it down.
    ///
    /// The three gestures a player spends most of a match on, tested through the
    /// same pointer entry points the mouse drives.
    /// </summary>
    public sealed class CardInteractionTests : InteractionTestBase
    {
        [UnityTest]
        public IEnumerator Hovering_a_card_raises_it_and_leaving_puts_it_back()
        {
            yield return LoadMatch();

            CardView card = FirstCardInHand();
            Vector3 resting = card.transform.position;

            MoveTo(card.transform.position);

            Assert.That(card.IsHovered, Is.True,
                "Pointing at a card did not hover it. The pointer landed on " + Input.LastHit + ".");
            Assert.That(Input.State, Is.EqualTo(InteractionState.HoveringHandCard));

            yield return WaitUntil(() => card.transform.position.y > resting.y + 0.1f);

            Vector3 raised = card.transform.position;
            Assert.That(raised.y, Is.GreaterThan(resting.y + 0.1f), "The hovered card did not rise.");

            // In front of its neighbours means nearer the camera than it was.
            Assert.That(
                Vector3.Distance(raised, MatchCamera.transform.position),
                Is.LessThan(Vector3.Distance(resting, MatchCamera.transform.position)),
                "The hovered card did not come forward, so its neighbours can still cover it.");

            // And it must not have risen off the top of the screen.
            Vector3 viewport = MatchCamera.WorldToViewportPoint(raised);
            Assert.That(viewport.y, Is.InRange(0.02f, 0.98f), "The hovered card left the screen.");
            Assert.That(viewport.x, Is.InRange(0.02f, 0.98f), "The hovered card left the screen.");

            MoveTo(EmptySpace);
            Assert.That(card.IsHovered, Is.False, "Leaving the card did not un-hover it.");

            yield return WaitUntil(() => Vector3.Distance(card.transform.position, resting) < 0.02f);

            Assert.That(Vector3.Distance(card.transform.position, resting), Is.LessThan(0.02f),
                "The card did not return to the pose the fan computed for it.");
        }

        /// <summary>
        /// Hovering repeatedly must not walk the card out of the hand. The pose
        /// is a target, never an accumulated offset.
        /// </summary>
        [UnityTest]
        public IEnumerator Hovering_many_times_leaves_the_card_where_it_started()
        {
            yield return LoadMatch();

            CardView card = FirstCardInHand();
            Vector3 resting = card.transform.position;

            for (int pass = 0; pass < 5; pass++)
            {
                MoveTo(card.transform.position);
                yield return WaitUntil(() => card.transform.position.y > resting.y + 0.1f);

                MoveTo(EmptySpace);
                yield return WaitUntil(() => Vector3.Distance(card.transform.position, resting) < 0.02f);
            }

            Assert.That(Vector3.Distance(card.transform.position, resting), Is.LessThan(0.02f),
                "The card drifted after being hovered five times.");
        }

        /// <summary>
        /// Not affording a card stops it being played, not read. On turn one
        /// nothing is affordable, which makes it the right moment to check.
        /// </summary>
        [UnityTest]
        public IEnumerator An_unplayable_card_can_still_be_hovered_and_read()
        {
            yield return LoadMatch();

            CardView card = FirstCardInHand();
            Assert.That(card.IsPlayable, Is.False, "This test needs a card nobody can afford yet.");

            MoveTo(card.transform.position);

            Assert.That(card.IsHovered, Is.True, "An unplayable card refused to be inspected.");

            Vector3 resting = card.RestingLocalPosition;
            yield return WaitUntil(() => card.transform.localPosition.y > resting.y + 0.1f);

            Assert.That(card.transform.localPosition.y, Is.GreaterThan(resting.y + 0.1f),
                "An unplayable card would not rise to be read.");
        }

        [UnityTest]
        public IEnumerator An_unplayable_card_cannot_be_picked_up_and_sends_no_command()
        {
            yield return LoadMatch();

            CardView card = FirstCardInHand();
            Assert.That(card.IsPlayable, Is.False);

            int handBefore = Active.Hand.Count;

            Drag(card.transform.position, NearBoardRight);

            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.DraggingHandCard),
                "An unplayable card was picked up.");
            Assert.That(Active.Board.Count, Is.Zero, "An unplayable card reached the board.");
            Assert.That(Active.Hand.Count, Is.EqualTo(handBefore), "The card left the hand anyway.");
            Assert.That(Presenter.TryGetCardView(card.EntityId, out CardView _), Is.True,
                "The card lost its view.");
        }

        [UnityTest]
        public IEnumerator Picking_a_card_up_takes_it_out_of_the_hand_and_it_follows_the_pointer()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();

            Press(card.transform.position);

            Assert.That(Input.State, Is.EqualTo(InteractionState.DraggingHandCard),
                "Pressing a playable card did not pick it up. The pointer landed on " + Input.LastHit + ".");
            Assert.That(card.IsDragging, Is.True);
            Assert.That(card.transform.parent, Is.SameAs(Presenter.DragLayer),
                "A dragged card must leave the hand, or the fan drags it around underneath.");

            // It has to be nearer than the table, or the board would cover it.
            float cardDistance = Vector3.Distance(card.transform.position, MatchCamera.transform.position);
            float boardDistance = Vector3.Distance(NearBoardRight, MatchCamera.transform.position);
            Assert.That(cardDistance, Is.LessThan(boardDistance),
                "The dragged card is behind the board.");

            // And it has to actually follow the pointer.
            Vector3 first = card.transform.position;
            MoveTo(NearBoardAt(-3f));
            Vector3 second = card.transform.position;
            MoveTo(NearBoardAt(3f));
            Vector3 third = card.transform.position;

            Assert.That(Vector3.Distance(first, second), Is.GreaterThan(0.2f), "The card did not move.");
            Assert.That(third.x, Is.GreaterThan(second.x), "The card moved the wrong way.");

            Release(NearBoardAt(3f));
        }

        [UnityTest]
        public IEnumerator Dropping_a_card_away_from_the_board_returns_it_to_the_hand()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            EntityId id = card.EntityId;
            int handBefore = Active.Hand.Count;

            Drag(card.transform.position, EmptySpace);

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle), "The card is still held.");
            Assert.That(Active.Board.Count, Is.Zero, "A card dropped into space reached the board.");
            Assert.That(Active.Hand.Count, Is.EqualTo(handBefore), "The card left the hand.");
            Assert.That(card.IsDragging, Is.False);

            Assert.That(Presenter.TryGetCardView(id, out CardView same), Is.True, "The card lost its view.");
            Assert.That(same, Is.SameAs(card), "The card came back as a different view.");
            Assert.That(same.EntityId, Is.EqualTo(id), "The card came back with a different identity.");

            Assert.That(same.transform.parent, Is.SameAs(Presenter.NearHandAnchor),
                "The card did not go back under the hand.");

            yield return WaitUntil(() =>
                Vector3.Distance(same.transform.localPosition, same.RestingLocalPosition) < 0.05f);

            Assert.That(Vector3.Distance(same.transform.localPosition, same.RestingLocalPosition),
                Is.LessThan(0.05f),
                "The card did not settle back into the pose the fan computed.");
        }

        [UnityTest]
        public IEnumerator Dropping_a_card_on_the_board_plays_it_exactly_once()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FirstPlayableCard();
            EntityId id = card.EntityId;
            int handBefore = Active.Hand.Count;
            int manaBefore = Active.AvailableMana;

            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.EqualTo(1),
                "Dropping the card on the board summoned " + player.Board.Count + " minions." +
                " The release landed on " + Input.LastHit + ".");
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore - 1), "Exactly one card should have left the hand.");
            Assert.That(player.AvailableMana, Is.LessThan(manaBefore), "The engine did not charge for it.");
            Assert.That(Presenter.TryGetCardView(id, out CardView _), Is.False, "The card is still shown in hand.");
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
        }

        /// <summary>
        /// A release is one gesture. Nothing may turn it into two plays, whether
        /// through a second callback or through the release arriving after the
        /// press already resolved it.
        /// </summary>
        [UnityTest]
        public IEnumerator One_release_never_plays_two_cards()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FirstPlayableCard();

            Press(card.transform.position);
            MoveTo(NearBoardRight);
            Release(NearBoardRight);

            // Everything a stray event could look like, replayed on purpose.
            Release(NearBoardRight);
            Release(NearBoardRight);
            MoveTo(NearBoardRight);

            yield return Settle();

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1),
                "One release produced more than one minion.");
        }

        /// <summary>
        /// The slot follows the pointer across the row, and the row opens up to
        /// show it. Left of everything, between two, and past the end.
        /// </summary>
        [UnityTest]
        public IEnumerator The_drop_slot_follows_the_pointer_across_the_row()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            // Two minions to aim between.
            yield return PlayOneMinionDirectly();
            yield return RoundTrip();
            yield return PlayOneMinionDirectly();
            yield return RoundTrip();

            PlayerId acting = Session.State.CurrentPlayer;
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(2));

            CardView card = FirstPlayableCard();
            Press(card.transform.position);

            MoveTo(NearBoardAt(-4f));
            Assert.That(Presenter.InsertionSlot, Is.Zero, "Pointing left of the row should insert first.");

            MoveTo(NearBoardAt(0f));
            Assert.That(Presenter.InsertionSlot, Is.EqualTo(1), "Pointing between the two should insert between.");

            MoveTo(NearBoardAt(4f));
            Assert.That(Presenter.InsertionSlot, Is.EqualTo(2), "Pointing right of the row should append.");

            // The marker has to be showing the slot it claims.
            BoardInsertionMarker marker = Object.FindFirstObjectByType<BoardInsertionMarker>();
            Assert.That(marker, Is.Not.Null, "The board has no insertion marker.");
            Assert.That(marker.IsVisible, Is.True, "No slot is being shown while a card is over the board.");
            Assert.That(marker.Slot, Is.EqualTo(2));

            // Leaving the board closes it again.
            MoveTo(EmptySpace);
            Assert.That(Presenter.InsertionSlot, Is.EqualTo(-1), "The slot stayed open off the board.");
            Assert.That(marker.IsVisible, Is.False, "The marker stayed visible off the board.");

            Release(EmptySpace);
        }

        [UnityTest]
        public IEnumerator A_card_is_summoned_into_the_slot_that_was_shown()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            yield return PlayOneMinionDirectly();
            yield return RoundTrip();
            yield return PlayOneMinionDirectly();
            yield return RoundTrip();

            PlayerId acting = Session.State.CurrentPlayer;
            EntityId leftmost = Session.State.GetPlayer(acting).Board[0].Id;

            CardView card = FirstPlayableCard();

            Press(card.transform.position);
            MoveTo(NearBoardAt(-4f));

            int shown = Presenter.InsertionSlot;
            Assert.That(shown, Is.Zero);

            Release(NearBoardAt(-4f));
            yield return Settle();

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.EqualTo(3));
            Assert.That(player.Board[0].Id, Is.Not.EqualTo(leftmost),
                "The new minion did not land in the slot the marker promised.");
            Assert.That(player.Board[1].Id, Is.EqualTo(leftmost),
                "The minion that was leftmost should have moved along by one.");
        }

        /// <summary>
        /// With seven minions out, the engine refuses the card, so it is never
        /// picked up and never leaves the hand. The board limit stays entirely
        /// the engine's to enforce.
        /// </summary>
        [UnityTest]
        public IEnumerator A_full_board_offers_no_drop_and_the_card_stays_in_hand()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();
            yield return FillActiveBoard();

            PlayerId acting = Session.State.CurrentPlayer;
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(7));

            CardView card = FirstCardInHand();
            int handBefore = Session.State.GetPlayer(acting).Hand.Count;

            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.EqualTo(7), "An eighth minion reached a full board.");
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore), "The card left the hand anyway.");
            Assert.That(Presenter.InsertionSlot, Is.EqualTo(-1), "A full board offered a slot.");
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
        }
    }
}
