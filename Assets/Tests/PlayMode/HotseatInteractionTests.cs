using System.Collections;
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
    /// Every Phase 8 gesture, for both players, across repeated flips of the
    /// board.
    ///
    /// The hotseat is the part of this project that has broken twice, both times
    /// in a way no rules test could see. So these check the gestures themselves,
    /// after the perspective has swung back and forth several times, rather than
    /// checking that commands are accepted.
    /// </summary>
    public sealed class HotseatInteractionTests : InteractionTestBase
    {
        [UnityTest]
        public IEnumerator Both_players_drag_and_drop_with_the_same_code()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            // Three plays alternating, so each player acts either side of a flip
            // and then again after the board has swung back.
            for (int round = 0; round < 4; round++)
            {
                PlayerId acting = Session.State.CurrentPlayer;
                int before = Session.State.GetPlayer(acting).Board.Count;

                CardView card = FirstPlayableCard();

                Assert.That(card.transform.parent, Is.SameAs(Presenter.NearHandAnchor),
                    "Round " + round + ": the acting player's card is not in the near hand.");

                Drag(card.transform.position, NearBoardRight);
                yield return Settle();

                Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(before + 1),
                    "Round " + round + " as " + acting + ": the drag did not play the card." +
                    " The release landed on " + Input.LastHit + ".");

                yield return EndTurn();

                if (PlayableCards().Count == 0)
                {
                    yield return AdvanceUntilSomethingIsPlayable();
                }
            }
        }

        [UnityTest]
        public IEnumerator Both_players_can_hover_a_card_after_a_flip()
        {
            yield return LoadMatch();

            for (int round = 0; round < 4; round++)
            {
                CardView card = FirstCardInHand();

                MoveTo(card.transform.position);

                Assert.That(card.IsHovered, Is.True,
                    "Round " + round + " as " + Session.State.CurrentPlayer +
                    ": the card would not hover. The pointer landed on " + Input.LastHit + ".");

                MoveTo(EmptySpace);
                Assert.That(card.IsHovered, Is.False);

                yield return EndTurn();
            }
        }

        [UnityTest]
        public IEnumerator Both_players_can_aim_and_attack()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            // A minion each, both old enough to swing.
            yield return PlayOneMinionDirectly();
            yield return EndTurn();
            yield return AdvanceUntilSomethingIsPlayable();
            yield return PlayOneMinionDirectly();
            yield return EndTurn();

            for (int round = 0; round < 2; round++)
            {
                PlayerId acting = Session.State.CurrentPlayer;
                PlayerId waiting = acting.Opponent;

                MinionView attacker = FirstMinionOf(acting);

                Assert.That(Session.CanAttack(acting, attacker.EntityId), Is.EqualTo(RejectionReason.None),
                    "Round " + round + ": " + acting + " has nothing ready to swing.");

                HeroView enemyHero = HeroViewOf(waiting);
                int before = Session.State.GetPlayer(waiting).Hero.CurrentHealth;

                Press(attacker.transform.position);

                Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingAttack),
                    "Round " + round + " as " + acting + ": aiming would not start after the flip." +
                    " The pointer landed on " + Input.LastHit + ".");

                MoveTo(enemyHero.transform.position);
                Release(enemyHero.transform.position);
                yield return Settle();

                Assert.That(Session.State.GetPlayer(waiting).Hero.CurrentHealth, Is.LessThan(before),
                    "Round " + round + " as " + acting + ": the attack did not land after the flip.");

                yield return EndTurn();
            }
        }

        /// <summary>
        /// The board swings round at every turn change, so screen to world has
        /// to keep agreeing with it. Aiming at a chosen offset in the row must
        /// resolve to the same slot whichever player is looking at it.
        /// </summary>
        [UnityTest]
        public IEnumerator Pointing_at_the_row_resolves_the_same_slot_after_every_flip()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            for (int round = 0; round < 4; round++)
            {
                PlayerId acting = Session.State.CurrentPlayer;
                int count = Session.State.GetPlayer(acting).Board.Count;

                CardView card = FirstPlayableCard();
                Press(card.transform.position);

                Assert.That(Input.State, Is.EqualTo(InteractionState.DraggingHandCard),
                    "Round " + round + " as " + acting + ": the card would not be picked up.");

                MoveTo(NearBoardAt(-4.5f));
                Assert.That(Presenter.InsertionSlot, Is.Zero,
                    "Round " + round + " as " + acting + ": the far left of the row did not resolve to slot 0.");

                MoveTo(NearBoardAt(4.5f));
                Assert.That(Presenter.InsertionSlot, Is.EqualTo(count),
                    "Round " + round + " as " + acting + ": the far right did not resolve to the end.");

                Release(NearBoardAt(4.5f));
                yield return Settle();

                Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(count + 1));

                yield return EndTurn();

                if (PlayableCards().Count == 0)
                {
                    yield return AdvanceUntilSomethingIsPlayable();
                }
            }
        }

        /// <summary>
        /// Nothing may start while the queue is replaying. A player acting on a
        /// board that has already moved on is the reason the lock exists.
        /// </summary>
        [UnityTest]
        public IEnumerator No_interaction_starts_while_the_queue_is_replaying()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            Vector3 cardPosition = card.transform.position;

            // End the turn and act immediately, before the events have played.
            Session.Submit(new EndTurnCommand(Session.State.CurrentPlayer));

            Assert.That(Session.IsBusy, Is.True, "The queue was expected to be replaying.");

            Press(cardPosition);

            Assert.That(Input.State, Is.EqualTo(InteractionState.Resolving),
                "An interaction started while events were still replaying.");
            Assert.That(Input.HasSelection, Is.False, "Something was picked up mid-resolution.");

            yield return Settle();

            Assert.That(Session.IsBusy, Is.False, "The queue never drained.");

            // And the moment it drains, the pointer works again.
            yield return AdvanceUntilSomethingIsPlayable();

            CardView now = FirstPlayableCard();
            Press(now.transform.position);

            Assert.That(Input.State, Is.EqualTo(InteractionState.DraggingHandCard),
                "Input never came back after the queue drained.");

            Release(EmptySpace);
        }

        /// <summary>
        /// An interaction caught by a resolution is dropped rather than left
        /// hanging over a board that is about to change.
        /// </summary>
        [UnityTest]
        public IEnumerator An_interaction_in_progress_is_dropped_when_the_queue_starts()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            EntityId id = card.EntityId;

            Press(card.transform.position);
            MoveTo(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.DraggingHandCard));

            // The turn ends from elsewhere, mid-drag.
            Session.Submit(new EndTurnCommand(Session.State.CurrentPlayer));
            yield return null;

            Assert.That(Input.HasSelection, Is.False, "The card was still held through a resolution.");
            Assert.That(Presenter.InsertionSlot, Is.EqualTo(-1), "A slot stayed open through a resolution.");

            yield return Settle();

            Assert.That(Presenter.TryGetCardView(id, out CardView returned), Is.True,
                "The dragged card was lost.");
            Assert.That(returned.IsDragging, Is.False, "The card is still in the air.");
        }
    }
}
