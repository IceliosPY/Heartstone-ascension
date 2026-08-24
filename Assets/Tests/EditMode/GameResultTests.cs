using System.Collections.Generic;
using System.Linq;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// How a match ends. One source of truth, one GameEnded, and a genuine draw
    /// when both heroes go down together rather than a win for whichever the
    /// loop happened to reach first.
    /// </summary>
    public sealed class GameResultTests
    {
        private static Hero HeroOf(GameEngine engine, PlayerId seat) =>
            engine.State.GetPlayer(seat).Hero;

        [Test]
        public void A_new_match_is_in_progress()
        {
            GameEngine engine = TestFactory.StartedMatch();

            Assert.That(engine.State.Result, Is.EqualTo(GameResult.InProgress));
            Assert.That(engine.State.HasEnded, Is.False);
            Assert.That(engine.State.Winner.IsNone, Is.True);
        }

        [Test]
        public void Killing_seat_one_gives_the_win_to_seat_two()
        {
            GameEngine engine = TestFactory.StartedMatch();

            IReadOnlyList<GameEvent> events = TestFactory.Damage(engine, HeroOf(engine, PlayerId.One).Id, 30);

            Assert.That(engine.State.Result, Is.EqualTo(GameResult.PlayerTwoWins));
            Assert.That(engine.State.Winner, Is.EqualTo(PlayerId.Two));
            Assert.That(events.OfType<HeroDiedEvent>().Single().PlayerId, Is.EqualTo(PlayerId.One));
        }

        [Test]
        public void Killing_seat_two_gives_the_win_to_seat_one()
        {
            GameEngine engine = TestFactory.StartedMatch();

            TestFactory.Damage(engine, HeroOf(engine, PlayerId.Two).Id, 30);

            Assert.That(engine.State.Result, Is.EqualTo(GameResult.PlayerOneWins));
            Assert.That(engine.State.Winner, Is.EqualTo(PlayerId.One));
        }

        [Test]
        public void Both_heroes_dying_in_the_same_phase_is_a_draw()
        {
            GameEngine engine = TestFactory.StartedMatch();

            IReadOnlyList<GameEvent> events = TestFactory.DamageTogether(
                engine,
                (HeroOf(engine, PlayerId.One).Id, 30),
                (HeroOf(engine, PlayerId.Two).Id, 30));

            Assert.That(engine.State.Result, Is.EqualTo(GameResult.Draw));
            Assert.That(engine.State.Winner.IsNone, Is.True);
            Assert.That(events.OfType<HeroDiedEvent>().Count(), Is.EqualTo(2));

            GameEndedEvent ended = events.OfType<GameEndedEvent>().Single();
            Assert.That(ended.IsDraw, Is.True);
            Assert.That(ended.Result, Is.EqualTo(GameResult.Draw));
        }

        [Test]
        public void A_draw_does_not_depend_on_which_hero_is_listed_first()
        {
            GameEngine left = TestFactory.StartedMatch(seed: 12UL);
            GameEngine right = TestFactory.StartedMatch(seed: 12UL);

            TestFactory.DamageTogether(
                left,
                (HeroOf(left, PlayerId.One).Id, 30),
                (HeroOf(left, PlayerId.Two).Id, 30));

            TestFactory.DamageTogether(
                right,
                (HeroOf(right, PlayerId.Two).Id, 30),
                (HeroOf(right, PlayerId.One).Id, 30));

            Assert.That(left.State.Result, Is.EqualTo(GameResult.Draw));
            Assert.That(right.State.Result, Is.EqualTo(GameResult.Draw));
        }

        [Test]
        public void A_hero_and_a_minion_can_die_in_the_same_phase()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion minion = TestFactory.PutMinionOnBoard(engine, PlayerId.Two, health: 1);

            IReadOnlyList<GameEvent> events = TestFactory.DamageTogether(
                engine,
                (minion.Id, 1),
                (HeroOf(engine, PlayerId.One).Id, 30));

            Assert.That(events.OfType<MinionDiedEvent>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<HeroDiedEvent>().Count(), Is.EqualTo(1));
            Assert.That(engine.State.Result, Is.EqualTo(GameResult.PlayerTwoWins));

            // Heroes were stamped before any minion, so they are processed first.
            List<string> order = events
                .Where(e => e is MinionDiedEvent || e is HeroDiedEvent)
                .Select(e => e.GetType().Name)
                .ToList();
            Assert.That(order, Is.EqualTo(new List<string> { nameof(HeroDiedEvent), nameof(MinionDiedEvent) }));
        }

        [Test]
        public void Game_ended_is_reported_exactly_once()
        {
            GameEngine engine = TestFactory.StartedMatch();

            IReadOnlyList<GameEvent> lethal = TestFactory.Damage(engine, HeroOf(engine, PlayerId.One).Id, 30);
            Assert.That(lethal.OfType<GameEndedEvent>().Count(), Is.EqualTo(1));

            // Anything that runs afterwards must not announce it again.
            IReadOnlyList<GameEvent> afterwards = engine.ResolvePending();
            Assert.That(afterwards.OfType<GameEndedEvent>(), Is.Empty);
            Assert.That(afterwards, Is.Empty);
        }

        [Test]
        public void A_dead_hero_is_not_reported_dead_twice()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Hero hero = HeroOf(engine, PlayerId.One);

            TestFactory.Damage(engine, hero.Id, 30);
            IReadOnlyList<GameEvent> again = TestFactory.Damage(engine, hero.Id, 30);

            Assert.That(hero.HasDied, Is.True);
            Assert.That(again.OfType<HeroDiedEvent>(), Is.Empty);
            Assert.That(again.OfType<DamageDealtEvent>(), Is.Empty, "A dead hero takes no more damage.");
        }

        [Test]
        public void Commands_are_refused_once_the_match_is_over()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.Damage(engine, HeroOf(engine, active).Id, 30);

            int turnBefore = engine.State.TurnNumber;
            GameResult resultBefore = engine.State.Result;

            CommandResult endTurn = engine.Execute(new EndTurnCommand(active));
            CommandResult mulligan = engine.Execute(new MulliganCommand(active));

            Assert.That(endTurn.Reason, Is.EqualTo(RejectionReason.GameAlreadyEnded));
            Assert.That(mulligan.Reason, Is.EqualTo(RejectionReason.GameAlreadyEnded));
            Assert.That(endTurn.Events, Is.Empty);
            Assert.That(mulligan.Events, Is.Empty);

            Assert.That(engine.State.TurnNumber, Is.EqualTo(turnBefore), "The state must not move.");
            Assert.That(engine.State.Result, Is.EqualTo(resultBefore));
            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Ended));
            Assert.That(engine.State.CurrentPlayer.IsNone, Is.True);
        }

        [Test]
        public void Destroying_a_hero_ends_the_match_too()
        {
            GameEngine engine = TestFactory.StartedMatch();

            TestFactory.Destroy(engine, HeroOf(engine, PlayerId.Two).Id);

            Assert.That(HeroOf(engine, PlayerId.Two).HasDied, Is.True);
            Assert.That(engine.State.Result, Is.EqualTo(GameResult.PlayerOneWins));
        }
    }
}
