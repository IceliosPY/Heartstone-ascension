using System.Collections;
using System.Collections.Generic;
using System.Text;
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
    /// Where a minion stands once the fighting stops.
    ///
    /// A combat animation moves a minion off its slot on purpose, and the whole
    /// question is whether it gives that slot back. These run at real durations
    /// rather than instantly, because a displacement that outlives its animation
    /// can only happen if the animation actually ran: with every duration at
    /// zero there is no window for one system to still be writing a transform
    /// while another has finished with it.
    ///
    /// The rule being checked is the same one every time. When a sequence ends,
    /// a minion that is still alive stands exactly where the row layout says,
    /// and a minion that died is gone.
    /// </summary>
    public sealed class MinionPoseTests : InteractionTestBase
    {
        /// <summary>Fast enough not to be slow, slow enough to be real.</summary>
        private const float TestSpeed = 8f;

        private IEnumerator LoadAnimatedMatch()
        {
            yield return LoadMatch();
            MatchTestScene.MakeFast(TestSpeed);
        }

        // ------------------------------------------------------------------
        //  The invariant
        // ------------------------------------------------------------------

        /// <summary>
        /// Every minion on the board stands exactly on the pose the row layout
        /// computes for its slot, with nothing left over from an animation.
        /// </summary>
        private void AssertBoardIsAtRest(string when)
        {
            StringBuilder wrong = new StringBuilder();

            foreach (PlayerId seat in new[] { PlayerId.One, PlayerId.Two })
            {
                Player player = Session.State.GetPlayer(seat);

                for (int slot = 0; slot < player.Board.Count; slot++)
                {
                    EntityId id = player.Board[slot].Id;

                    if (!Presenter.TryGetMinionView(id, out MinionView view) || view == null)
                    {
                        wrong.AppendLine(seat + " slot " + slot + ": the minion has no view at all.");
                        continue;
                    }

                    Vector3 expected = BoardRowLayout.GetPosition(
                        slot, player.Board.Count, Presenter.BoardSpacing);

                    if (Vector3.Distance(view.RestingLocalPosition, expected) > 0.0005f)
                    {
                        wrong.AppendLine(
                            seat + " slot " + slot + ": the layout target is " +
                            view.RestingLocalPosition.ToString("F3") + " but the row wants " +
                            expected.ToString("F3") + ".");
                    }

                    if (view.VisualOffset.sqrMagnitude > 0.0000001f)
                    {
                        wrong.AppendLine(
                            seat + " slot " + slot + ": an animation offset of " +
                            view.VisualOffset.ToString("F3") + " outlived its animation.");
                    }

                    if (Vector3.Distance(view.transform.localPosition, expected) > 0.0005f)
                    {
                        wrong.AppendLine(
                            seat + " slot " + slot + ": it stands at " +
                            view.transform.localPosition.ToString("F3") + " instead of " +
                            expected.ToString("F3") + ".");
                    }
                }
            }

            Assert.That(wrong.Length, Is.Zero, when + ":\n" + wrong);
        }

        /// <summary>
        /// Waits for the board to settle, then checks it. Minions slide to their
        /// slots rather than snapping, so a check the same frame a sequence ends
        /// would be measuring the slide, not the result.
        /// </summary>
        private IEnumerator SettleBoardAndAssert(string when)
        {
            yield return WaitUntil(BoardIsAtRest, seconds: 4f);
            AssertBoardIsAtRest(when);
        }

        private bool BoardIsAtRest()
        {
            foreach (PlayerId seat in new[] { PlayerId.One, PlayerId.Two })
            {
                Player player = Session.State.GetPlayer(seat);

                for (int slot = 0; slot < player.Board.Count; slot++)
                {
                    if (!Presenter.TryGetMinionView(player.Board[slot].Id, out MinionView view) ||
                        view == null)
                    {
                        return false;
                    }

                    Vector3 expected = BoardRowLayout.GetPosition(
                        slot, player.Board.Count, Presenter.BoardSpacing);

                    if (Vector3.Distance(view.transform.localPosition, expected) > 0.0005f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // ------------------------------------------------------------------
        //  Setup
        // ------------------------------------------------------------------

        /// <summary>
        /// Gives both players minions, all old enough to attack, and leaves the
        /// starting player acting.
        /// </summary>
        private IEnumerator GiveBothPlayersMinions(int each)
        {
            yield return AdvanceUntilSomethingIsPlayable();

            PlayerId first = Session.State.CurrentPlayer;

            for (int guard = 0; guard < 40; guard++)
            {
                bool done =
                    Session.State.GetPlayer(first).Board.Count >= each &&
                    Session.State.GetPlayer(first.Opponent).Board.Count >= each;

                if (done && Session.State.CurrentPlayer == first)
                {
                    // One more round trip so nothing is summoning sick.
                    yield return RoundTrip();
                    yield break;
                }

                PlayerId acting = Session.State.CurrentPlayer;

                if (Session.State.GetPlayer(acting).Board.Count < each)
                {
                    foreach (CardInstance card in Session.State.GetPlayer(acting).Hand)
                    {
                        if (Session.CanSubmit(new PlayCardCommand(acting, card.Id)))
                        {
                            Session.Submit(new PlayCardCommand(acting, card.Id));
                            yield return Settle();
                            break;
                        }
                    }
                }

                yield return EndTurn();
            }

            Assert.Fail("Both players never reached " + each + " minions.");
        }

        private IEnumerator Attack(EntityId attacker, EntityId target)
        {
            PlayerId acting = Session.State.CurrentPlayer;

            Assert.That(Session.CanAttack(acting, attacker), Is.EqualTo(RejectionReason.None),
                "The attacker was not ready to swing.");

            Session.Submit(new AttackCommand(acting, attacker, target));
            yield return Settle();
        }

        private List<EntityId> BoardOf(PlayerId seat)
        {
            List<EntityId> ids = new List<EntityId>();
            Player player = Session.State.GetPlayer(seat);

            for (int slot = 0; slot < player.Board.Count; slot++)
            {
                ids.Add(player.Board[slot].Id);
            }

            return ids;
        }

        // ------------------------------------------------------------------
        //  The four outcomes of a single attack
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Both_survive_and_both_end_on_their_slots()
        {
            yield return LoadAnimatedMatch();
            yield return GiveBothPlayersMinions(1);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            yield return Attack(BoardOf(acting)[0], BoardOf(waiting)[0]);

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1));
            Assert.That(Session.State.GetPlayer(waiting).Board.Count, Is.EqualTo(1));

            yield return SettleBoardAndAssert("after a trade both survived");
        }

        [UnityTest]
        public IEnumerator The_defender_dies_and_the_attacker_returns_to_its_slot()
        {
            yield return LoadAnimatedMatch();
            yield return GiveBothPlayersMinions(2);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            EntityId defender = BoardOf(waiting)[0];

            // Soften it up, then finish it with a fresh attacker.
            yield return Attack(BoardOf(acting)[0], defender);
            yield return SettleBoardAndAssert("after the first exchange");

            yield return Attack(BoardOf(acting)[1], defender);

            Assert.That(Session.State.GetPlayer(waiting).Board, Has.Count.EqualTo(1),
                "The defender should have died.");
            Assert.That(Session.State.GetPlayer(acting).Board, Has.Count.EqualTo(2),
                "Both attackers should have survived.");

            yield return SettleBoardAndAssert("after killing the defender");
        }

        [UnityTest]
        public IEnumerator The_attacker_dies_and_the_defender_returns_to_its_slot()
        {
            yield return LoadAnimatedMatch();
            yield return GiveBothPlayersMinions(2);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            EntityId wounded = BoardOf(acting)[0];

            // One exchange leaves the attacker on its last point of health.
            yield return Attack(wounded, BoardOf(waiting)[0]);
            yield return RoundTrip();

            // Now it throws itself at a minion that has not been touched.
            yield return Attack(wounded, BoardOf(waiting)[1]);

            Assert.That(Session.State.GetPlayer(acting).Board, Has.Count.EqualTo(1),
                "The attacker should have died.");
            Assert.That(Session.State.GetPlayer(waiting).Board, Has.Count.EqualTo(2),
                "Both defenders should have survived.");

            yield return SettleBoardAndAssert("after the attacker died");
        }

        [UnityTest]
        public IEnumerator Both_die_and_every_other_minion_stays_on_its_slot()
        {
            yield return LoadAnimatedMatch();
            yield return GiveBothPlayersMinions(2);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            EntityId attacker = BoardOf(acting)[0];
            EntityId defender = BoardOf(waiting)[0];

            yield return Attack(attacker, defender);
            yield return RoundTrip();
            yield return Attack(attacker, defender);

            Assert.That(Session.State.GetPlayer(acting).Board, Has.Count.EqualTo(1),
                "The attacker should have died.");
            Assert.That(Session.State.GetPlayer(waiting).Board, Has.Count.EqualTo(1),
                "The defender should have died.");

            Assert.That(Presenter.TryGetMinionView(attacker, out MinionView _), Is.False,
                "The dead attacker kept its view.");
            Assert.That(Presenter.TryGetMinionView(defender, out MinionView _), Is.False,
                "The dead defender kept its view.");

            yield return SettleBoardAndAssert("after both died");
        }

        // ------------------------------------------------------------------
        //  And over and over
        // ------------------------------------------------------------------

        /// <summary>
        /// Attack after attack across several turns. A displacement that
        /// survives one sequence would accumulate here, and a row that drifts a
        /// little each time is exactly what this has to rule out.
        /// </summary>
        [UnityTest]
        public IEnumerator Repeated_attacks_never_leave_a_minion_off_its_slot()
        {
            yield return LoadAnimatedMatch();
            yield return GiveBothPlayersMinions(3);

            PlayerId acting = Session.State.CurrentPlayer;
            EntityId enemyHero = Session.State.GetPlayer(acting.Opponent).Hero.Id;

            for (int round = 0; round < 5; round++)
            {
                List<EntityId> board = BoardOf(acting);

                for (int index = 0; index < board.Count; index++)
                {
                    if (Session.CanAttack(acting, board[index]) != RejectionReason.None)
                    {
                        continue;
                    }

                    // The hero never hits back, so every minion survives and the
                    // row has to be intact after each swing.
                    yield return Attack(board[index], enemyHero);
                    yield return SettleBoardAndAssert("round " + round + ", attacker " + index);
                }

                yield return RoundTrip();
                yield return SettleBoardAndAssert("round " + round + ", after the turn came back");
            }
        }

        /// <summary>
        /// The same thing through the pointer rather than through the session,
        /// because the lunge is started by the interaction and it is the whole
        /// gesture that has to leave the board tidy.
        /// </summary>
        [UnityTest]
        public IEnumerator Attacking_by_dragging_leaves_the_row_at_rest()
        {
            yield return LoadAnimatedMatch();
            yield return GiveBothPlayersMinions(2);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            for (int round = 0; round < 3; round++)
            {
                List<EntityId> mine = BoardOf(acting);
                EntityId ready = EntityId.None;

                for (int index = 0; index < mine.Count; index++)
                {
                    if (Session.CanAttack(acting, mine[index]) == RejectionReason.None)
                    {
                        ready = mine[index];
                        break;
                    }
                }

                if (ready.IsNone || Session.State.GetPlayer(waiting).Board.Count == 0)
                {
                    break;
                }

                Assert.That(Presenter.TryGetMinionView(ready, out MinionView attacker), Is.True);
                Assert.That(
                    Presenter.TryGetMinionView(BoardOf(waiting)[0], out MinionView defender), Is.True);

                Drag(attacker.transform.position, defender.transform.position);
                yield return Settle();

                yield return SettleBoardAndAssert("after dragged attack " + round);

                yield return RoundTrip();
                yield return SettleBoardAndAssert("after the turn came back, round " + round);
            }
        }
    }
}
