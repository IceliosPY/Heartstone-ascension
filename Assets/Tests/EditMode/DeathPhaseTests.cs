using System.Collections.Generic;
using System.Linq;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Death phases: nothing is removed in the middle of an action, everything
    /// that died is removed together, and the order is decided by the game
    /// state rather than by memory layout.
    /// </summary>
    public sealed class DeathPhaseTests
    {
        private static List<MinionDiedEvent> Deaths(IEnumerable<GameEvent> events) =>
            events.OfType<MinionDiedEvent>().ToList();

        [Test]
        public void A_healthy_minion_is_left_alone()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, attack: 2, health: 3);

            IReadOnlyList<GameEvent> events = TestFactory.Damage(engine, minion.Id, 1);

            Assert.That(minion.IsInPlay, Is.True);
            Assert.That(minion.IsPendingDeath, Is.False);
            Assert.That(minion.CurrentHealth, Is.EqualTo(2));
            Assert.That(engine.State.GetPlayer(seat).Board.Count, Is.EqualTo(1));
            Assert.That(Deaths(events), Is.Empty);
        }

        [Test]
        public void A_minion_reduced_to_zero_is_removed_and_reported()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, attack: 2, health: 3);

            IReadOnlyList<GameEvent> events = TestFactory.Damage(engine, minion.Id, 3);

            Assert.That(minion.IsInPlay, Is.False);
            Assert.That(minion.Zone, Is.EqualTo(ZoneType.Graveyard));
            Assert.That(engine.State.GetPlayer(seat).Board.Count, Is.EqualTo(0));
            Assert.That(engine.State.GetPlayer(seat).Graveyard.Contains(minion), Is.True);

            List<MinionDiedEvent> deaths = Deaths(events);
            Assert.That(deaths, Has.Count.EqualTo(1));
            Assert.That(deaths[0].MinionId, Is.EqualTo(minion.Id));
            Assert.That(deaths[0].Controller, Is.EqualTo(seat));
        }

        [Test]
        public void Negative_health_kills_just_the_same()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, health: 3);

            TestFactory.Damage(engine, minion.Id, 10);

            Assert.That(minion.CurrentHealth, Is.EqualTo(-7));
            Assert.That(minion.IsInPlay, Is.False);
        }

        [Test]
        public void The_board_position_is_kept_in_the_event()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            TestFactory.PutMinionOnBoard(engine, seat);
            TestFactory.PutMinionOnBoard(engine, seat);
            Minion third = TestFactory.PutMinionOnBoard(engine, seat, health: 1);

            IReadOnlyList<GameEvent> events = TestFactory.Damage(engine, third.Id, 1);

            Assert.That(Deaths(events).Single().BoardPosition, Is.EqualTo(2));
            Assert.That(engine.State.GetPlayer(seat).Board.Count, Is.EqualTo(2));
        }

        [Test]
        public void Removing_a_minion_closes_the_gap_on_the_board()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion left = TestFactory.PutMinionOnBoard(engine, seat);
            Minion middle = TestFactory.PutMinionOnBoard(engine, seat, health: 1);
            Minion right = TestFactory.PutMinionOnBoard(engine, seat);

            TestFactory.Damage(engine, middle.Id, 1);

            Zone<Minion> board = engine.State.GetPlayer(seat).Board;
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board[0], Is.SameAs(left));
            Assert.That(board[1], Is.SameAs(right));
        }

        [Test]
        public void A_dead_minion_keeps_its_identity_for_the_event()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, health: 1);

            MinionDiedEvent death = Deaths(TestFactory.Damage(engine, minion.Id, 1)).Single();

            Assert.That(death.MinionId, Is.EqualTo(minion.Id));
            Assert.That(death.CardId, Is.EqualTo(minion.CardId));
            Assert.That(death.Owner, Is.EqualTo(minion.Owner));
            Assert.That(engine.State.GetEntity(minion.Id), Is.SameAs(minion));
        }

        [Test]
        public void A_stolen_minion_returns_to_its_owners_graveyard()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = PlayerId.One;
            PlayerId thief = PlayerId.Two;

            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, health: 1);
            engine.State.GetPlayer(owner).Board.Remove(minion);
            minion.Controller = thief;
            engine.State.GetPlayer(thief).Board.TryAdd(minion);

            MinionDiedEvent death = Deaths(TestFactory.Damage(engine, minion.Id, 1)).Single();

            Assert.That(death.Controller, Is.EqualTo(thief));
            Assert.That(death.Owner, Is.EqualTo(owner));
            Assert.That(engine.State.GetPlayer(owner).Graveyard.Contains(minion), Is.True);
            Assert.That(engine.State.GetPlayer(thief).Graveyard.Contains(minion), Is.False);
        }

        [Test]
        public void Destroying_ignores_health_entirely()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, health: 10);

            IReadOnlyList<GameEvent> events = TestFactory.Destroy(engine, minion.Id);

            Assert.That(minion.IsInPlay, Is.False);
            Assert.That(Deaths(events), Has.Count.EqualTo(1));
            Assert.That(events.OfType<DamageDealtEvent>(), Is.Empty, "Destruction is not damage.");
        }

        [Test]
        public void Healing_before_the_death_phase_saves_a_minion()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, health: 3);

            // Damage applied outside a resolution, then healed, then the
            // pipeline is asked to settle. Nothing should have died: whether a
            // character is doomed is read at the death phase, not latched when
            // the damage lands.
            minion.Damage = 3;
            Assert.That(minion.IsPendingDeath, Is.True);

            minion.Damage = 0;
            IReadOnlyList<GameEvent> events = engine.ResolvePending();

            Assert.That(minion.IsInPlay, Is.True);
            Assert.That(Deaths(events), Is.Empty);
        }

        [Test]
        public void A_minion_already_gone_cannot_be_damaged_again()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId seat = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, seat, health: 1);

            TestFactory.Damage(engine, minion.Id, 1);
            int damageAfterDeath = minion.Damage;

            IReadOnlyList<GameEvent> events = TestFactory.Damage(engine, minion.Id, 5);

            Assert.That(minion.Damage, Is.EqualTo(damageAfterDeath));
            Assert.That(events, Is.Empty);
        }
    }
}
