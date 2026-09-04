using System.Collections;
using System.Collections.Generic;
using System.Text;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// What the presentation does with a batch of events, in order.
    ///
    /// A staged sequence is worth what its order is worth, and the order cannot
    /// be seen by looking at the board once everything has finished. So these
    /// watch the queue as it goes: what was staged, in what order, and what
    /// still existed at each step.
    ///
    /// That last part is the one that matters most. The engine finishes a whole
    /// exchange before the presentation shows any of it, so by the time a hit is
    /// being staged the minion taking it may have been off the board for several
    /// events. If the views were driven by reading the state, a trade that kills
    /// both minions would have nothing left to animate.
    /// </summary>
    public sealed class PresentationSequenceTests : InteractionTestBase
    {
        /// <summary>Watches a batch being staged, from outside the presentation.</summary>
        private sealed class Recorder
        {
            private readonly MatchPresenter _presenter;

            public Recorder(PresentationQueue queue, MatchPresenter presenter)
            {
                _presenter = presenter;
                queue.Staging += Record;
            }

            public List<GameEvent> Staged { get; } = new List<GameEvent>();

            /// <summary>Targets that still had a view when their hit was staged.</summary>
            public List<EntityId> LiveWhenHit { get; } = new List<EntityId>();

            /// <summary>Minions that still had a view when their death was staged.</summary>
            public List<EntityId> LiveWhenDying { get; } = new List<EntityId>();

            public List<EntityId> Deaths { get; } = new List<EntityId>();

            public void Clear()
            {
                Staged.Clear();
                LiveWhenHit.Clear();
                LiveWhenDying.Clear();
                Deaths.Clear();
            }

            public bool Contains<T>() where T : GameEvent => IndexOf<T>() >= 0;

            public int IndexOf<T>() where T : GameEvent
            {
                for (int index = 0; index < Staged.Count; index++)
                {
                    if (Staged[index] is T)
                    {
                        return index;
                    }
                }

                return -1;
            }

            public string Describe()
            {
                StringBuilder text = new StringBuilder("staged: ");

                for (int index = 0; index < Staged.Count; index++)
                {
                    text.Append(Staged[index].GetType().Name).Append(' ');
                }

                return text.ToString();
            }

            private void Record(GameEvent staged)
            {
                Staged.Add(staged);

                if (staged is DamageDealtEvent damage &&
                    _presenter.TryGetMinionView(damage.TargetId, out MinionView hit) && hit != null)
                {
                    LiveWhenHit.Add(damage.TargetId);
                }

                if (staged is MinionDiedEvent died)
                {
                    Deaths.Add(died.MinionId);

                    if (_presenter.TryGetMinionView(died.MinionId, out MinionView dying) && dying != null)
                    {
                        LiveWhenDying.Add(died.MinionId);
                    }
                }
            }
        }

        private Recorder Watch() => new Recorder(Session.Queue, Presenter);

        // ------------------------------------------------------------------

        /// <summary>
        /// A turn draws a card, the card reaches the hand, and the queue lets go
        /// of the input afterwards.
        /// </summary>
        [UnityTest]
        public IEnumerator A_drawn_card_is_staged_and_joins_the_hand()
        {
            yield return LoadMatch();

            Recorder recorder = Watch();

            yield return EndTurn();

            Assert.That(recorder.Contains<TurnStartedEvent>(), Is.True, recorder.Describe());
            Assert.That(recorder.Contains<CardDrawnEvent>(), Is.True,
                "A turn should have drawn a card. " + recorder.Describe());

            Assert.That(recorder.IndexOf<TurnStartedEvent>(), Is.LessThan(recorder.IndexOf<CardDrawnEvent>()),
                "The turn has to start before its draw, or the card flies to the wrong side of the table.");

            CardDrawnEvent drawn = null;

            foreach (GameEvent staged in recorder.Staged)
            {
                if (staged is CardDrawnEvent card)
                {
                    drawn = card;
                }
            }

            Assert.That(Presenter.TryGetCardView(drawn.CardInstanceId, out CardView view), Is.True,
                "The drawn card has no view.");
            Assert.That(view.transform.parent, Is.SameAs(Presenter.NearHandAnchor),
                "The drawn card did not end up in the acting player's hand.");

            Assert.That(Session.IsBusy, Is.False, "The queue never let go of the input.");
            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.Resolving));
        }

        [UnityTest]
        public IEnumerator Playing_a_card_stages_the_card_then_the_summon()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FirstPlayableCard();
            EntityId cardId = card.EntityId;

            Recorder recorder = Watch();

            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            Assert.That(recorder.Contains<CardPlayedEvent>(), Is.True, recorder.Describe());
            Assert.That(recorder.Contains<MinionSummonedEvent>(), Is.True, recorder.Describe());

            Assert.That(
                recorder.IndexOf<CardPlayedEvent>(),
                Is.LessThan(recorder.IndexOf<MinionSummonedEvent>()),
                "The card has to leave the hand before the minion arrives, or the two read as unrelated.");

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.EqualTo(1));
            Assert.That(Presenter.TryGetMinionView(player.Board[0].Id, out MinionView minion), Is.True,
                "The summoned minion has no view.");
            Assert.That(minion.transform.localScale.x, Is.EqualTo(1f).Within(0.01f),
                "The summon animation did not leave the minion at its full size.");

            Assert.That(Presenter.TryGetCardView(cardId, out CardView _), Is.False,
                "The played card is still shown in hand.");

            Assert.That(Session.IsBusy, Is.False, "The queue never let go of the input.");
        }

        [UnityTest]
        public IEnumerator An_attack_stages_the_declaration_before_its_damage()
        {
            yield return LoadMatch();
            yield return SetUpOneMinionEach();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            MinionView attacker = FirstMinionOf(acting);
            MinionView defender = FirstMinionOf(waiting);

            Recorder recorder = Watch();

            Drag(attacker.transform.position, defender.transform.position);
            yield return Settle();

            Assert.That(recorder.Contains<AttackDeclaredEvent>(), Is.True, recorder.Describe());
            Assert.That(recorder.Contains<DamageDealtEvent>(), Is.True, recorder.Describe());

            Assert.That(
                recorder.IndexOf<AttackDeclaredEvent>(),
                Is.LessThan(recorder.IndexOf<DamageDealtEvent>()),
                "The lunge has to be staged before the impact it causes.");

            // Both halves of the trade had a view to react on.
            Assert.That(recorder.LiveWhenHit.Count, Is.EqualTo(2),
                "Both minions should have had a view when their hit was staged. " + recorder.Describe());

            Assert.That(Session.IsBusy, Is.False, "The queue never let go of the input.");
            Assert.That(Presenter.NearHero, Is.Not.Null);
        }

        /// <summary>
        /// The case the whole design is built around: a trade that kills both
        /// minions. The engine has removed them both before the presentation
        /// stages the first hit, so the views have to be kept until the deaths
        /// are staged, and only then taken away.
        /// </summary>
        [UnityTest]
        public IEnumerator A_trade_that_kills_both_minions_stages_both_deaths()
        {
            yield return LoadMatch();
            yield return SetUpOneMinionEach();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            EntityId attackerId = FirstMinionOf(acting).EntityId;
            EntityId defenderId = FirstMinionOf(waiting).EntityId;

            // Two soldiers of two attack and three health need two exchanges to
            // finish each other off.
            yield return AttackWith(attackerId, defenderId);

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1),
                "The first trade should not have killed anything.");

            yield return RoundTrip();

            Recorder recorder = Watch();

            yield return AttackWith(attackerId, defenderId);

            Assert.That(recorder.Deaths, Has.Count.EqualTo(2),
                "Both minions should have died. " + recorder.Describe());
            Assert.That(recorder.Deaths, Contains.Item(attackerId));
            Assert.That(recorder.Deaths, Contains.Item(defenderId));

            // The point of the whole exercise: both were still there to be hit,
            // and both were still there to be seen dying.
            Assert.That(recorder.LiveWhenHit, Has.Count.EqualTo(2),
                "A minion was hit with no view left to show it. " + recorder.Describe());
            Assert.That(recorder.LiveWhenDying, Has.Count.EqualTo(2),
                "A minion died with no view left to play it out. " + recorder.Describe());

            // And afterwards, neither is left behind.
            Assert.That(Presenter.TryGetMinionView(attackerId, out MinionView _), Is.False,
                "The attacker's view outlived it.");
            Assert.That(Presenter.TryGetMinionView(defenderId, out MinionView _), Is.False,
                "The defender's view outlived it.");

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.Zero);
            Assert.That(Session.State.GetPlayer(waiting).Board.Count, Is.Zero);
            Assert.That(Session.IsBusy, Is.False);
        }

        /// <summary>
        /// The hero goes down, the match ends, and the input stays locked for
        /// good rather than coming back.
        /// </summary>
        [UnityTest]
        public IEnumerator Lethal_stages_the_damage_then_the_end_and_input_stays_locked()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();
            yield return FillActiveBoard();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            Recorder recorder = Watch();

            // Everything ready swings at the hero, turn after turn.
            for (int round = 0; round < 12 && !Session.State.HasEnded; round++)
            {
                yield return RoundTrip();
                yield return AttackHeroWithEverything(acting);
            }

            Assert.That(Session.State.HasEnded, Is.True, "The hero never went down.");

            Assert.That(recorder.Contains<HeroDiedEvent>(), Is.True, recorder.Describe());
            Assert.That(recorder.Contains<GameEndedEvent>(), Is.True, recorder.Describe());

            Assert.That(
                recorder.IndexOf<HeroDiedEvent>(),
                Is.LessThan(recorder.IndexOf<GameEndedEvent>()),
                "The hero has to be seen going down before the result is announced.");

            Assert.That(Session.State.GetPlayer(waiting).Hero.CurrentHealth, Is.LessThanOrEqualTo(0));

            // Input never comes back after the match is over.
            yield return null;

            Assert.That(Input.State, Is.EqualTo(InteractionState.GameEnded),
                "The input did not settle into the ended state.");

            Press(NearBoardRight);

            Assert.That(Input.HasSelection, Is.False, "Something was picked up after the match ended.");
        }

        /// <summary>
        /// The turn banner, the flip, and the input coming back. Run at real
        /// durations rather than instantly, because what is being checked is
        /// that the lock actually holds while the animation is running.
        /// </summary>
        [UnityTest]
        public IEnumerator A_turn_change_locks_input_through_its_animation_and_releases_it_after()
        {
            yield return LoadMatch();

            PresentationTiming timing = MatchTestScene.MakeFast(speed: 6f);
            Assert.That(timing, Is.Not.Null, "The scene has no presentation timing.");
            Assert.That(timing.IsInstant, Is.False);

            PlayerId before = Session.State.CurrentPlayer;

            Session.Submit(new EndTurnCommand(before));

            Assert.That(Session.IsBusy, Is.True, "The turn change staged nothing.");

            // While it is running, nothing may start.
            Press(FirstCardInHand().transform.position);
            Assert.That(Input.HasSelection, Is.False, "A card was picked up mid turn change.");

            yield return Settle();

            Assert.That(Session.IsBusy, Is.False);
            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(before.Opponent));
            Assert.That(Presenter.Viewpoint, Is.EqualTo(before.Opponent),
                "The board did not turn round to the new player.");

            // And the new player can act straight away.
            CardView card = FirstCardInHand();
            Assert.That(card.transform.parent, Is.SameAs(Presenter.NearHandAnchor),
                "The new player's hand is not the near one.");

            MoveTo(card.transform.position);
            Assert.That(card.IsHovered, Is.True,
                "The new player cannot even hover. The pointer landed on " + Input.LastHit + ".");
        }

        // --- setup -------------------------------------------------------

        private IEnumerator SetUpOneMinionEach()
        {
            yield return PlayTestSoldierEventually();
            yield return EndTurn();

            yield return PlayTestSoldierEventually();
            yield return EndTurn();
        }

        /// <summary>
        /// Ends turns until the active player is holding a playable Test
        /// Soldier, then plays it - specifically that card, not merely "a
        /// minion". This file's own arithmetic (two exchanges of 2 damage
        /// each to bring down a 3 health body) is tuned to Test Soldier's
        /// exact stats, and the hand may hold other minions - or Starcaller's
        /// Huntress Shot, playable only once a target exists - before it does.
        /// </summary>
        private IEnumerator PlayTestSoldierEventually(int maxTurns = 12)
        {
            for (int guard = 0; guard < maxTurns; guard++)
            {
                CardView soldier = FindCardInHand("test_soldier");

                if (soldier != null && soldier.IsPlayable)
                {
                    Session.Submit(new PlayCardCommand(Session.State.CurrentPlayer, soldier.EntityId));
                    yield return Settle();
                    yield break;
                }

                yield return EndTurn();
            }

            Assert.Fail("Test Soldier never became playable within " + maxTurns + " turns.");
        }

        private IEnumerator AttackWith(EntityId attacker, EntityId target)
        {
            Session.Submit(new AttackCommand(Session.State.CurrentPlayer, attacker, target));
            yield return Settle();
        }

        private IEnumerator AttackHeroWithEverything(PlayerId acting)
        {
            if (Session.State.CurrentPlayer != acting || Session.State.HasEnded)
            {
                yield break;
            }

            EntityId enemyHero = Session.State.GetPlayer(acting.Opponent).Hero.Id;

            for (int guard = 0; guard < 8 && !Session.State.HasEnded; guard++)
            {
                EntityId ready = EntityId.None;
                Player player = Session.State.GetPlayer(acting);

                for (int slot = 0; slot < player.Board.Count; slot++)
                {
                    if (Session.CanAttack(acting, player.Board[slot].Id) == RejectionReason.None)
                    {
                        ready = player.Board[slot].Id;
                        break;
                    }
                }

                if (ready.IsNone)
                {
                    yield break;
                }

                Session.Submit(new AttackCommand(acting, ready, enemyHero));
                yield return Settle();
            }
        }
    }
}
