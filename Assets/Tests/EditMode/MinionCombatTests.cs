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
    /// Minion against minion. The rule that matters: both attack values are
    /// read before either point of damage is applied, so a minion that dies in
    /// the exchange still strikes.
    /// </summary>
    public sealed class MinionCombatTests
    {
        private static List<string> TypeNames(IEnumerable<GameEvent> events) =>
            events.Select(gameEvent => gameEvent.GetType().Name).ToList();

        [Test]
        public void A_two_three_attacking_a_three_two_kills_both()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 3, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 3, health: 2);

            IReadOnlyList<GameEvent> events = TestFactory.Attack(engine, attacker.Id, defender.Id).Events;

            Assert.That(defender.IsInPlay, Is.False, "Took 2, had 2 health.");
            Assert.That(attacker.IsInPlay, Is.False, "Took 3, had 3 health, and struck all the same.");
            Assert.That(events.OfType<MinionDiedEvent>().Count(), Is.EqualTo(2));
            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(0));
            Assert.That(engine.State.GetPlayer(active.Opponent).Board.Count, Is.EqualTo(0));
        }

        [Test]
        public void A_bigger_minion_survives_and_keeps_its_damage()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 4, health: 5, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 2, health: 3);

            TestFactory.Attack(engine, attacker.Id, defender.Id);

            Assert.That(defender.IsInPlay, Is.False);
            Assert.That(attacker.IsInPlay, Is.True);
            Assert.That(attacker.CurrentHealth, Is.EqualTo(3), "Five health, took two.");
            Assert.That(attacker.Damage, Is.EqualTo(2));
            Assert.That(attacker.MaxHealth, Is.EqualTo(5), "Max health is untouched by damage.");
        }

        [Test]
        public void Both_can_survive_an_exchange()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 1, health: 5, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 2, health: 5);

            IReadOnlyList<GameEvent> events = TestFactory.Attack(engine, attacker.Id, defender.Id).Events;

            Assert.That(attacker.CurrentHealth, Is.EqualTo(3));
            Assert.That(defender.CurrentHealth, Is.EqualTo(4));
            Assert.That(attacker.IsInPlay, Is.True);
            Assert.That(defender.IsInPlay, Is.True);
            Assert.That(events.OfType<MinionDiedEvent>(), Is.Empty);
        }

        [Test]
        public void The_defenders_attack_is_read_before_it_takes_damage()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            // A 5/1 defender is wiped out by the first point of damage, yet it
            // must still hit back for its full 5.
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 1, health: 10, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 5, health: 1);

            TestFactory.Attack(engine, attacker.Id, defender.Id);

            Assert.That(defender.IsInPlay, Is.False);
            Assert.That(attacker.Damage, Is.EqualTo(5), "A dying defender still deals its full damage.");
        }

        [Test]
        public void A_defender_with_no_attack_deals_nothing_and_reports_nothing()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 3, health: 3, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 5);

            IReadOnlyList<GameEvent> events = TestFactory.Attack(engine, attacker.Id, defender.Id).Events;

            Assert.That(attacker.Damage, Is.EqualTo(0));
            Assert.That(defender.CurrentHealth, Is.EqualTo(2));
            Assert.That(events.OfType<DamageDealtEvent>().Count(), Is.EqualTo(1),
                "No damage event for a blow that deals nothing.");
        }

        [Test]
        public void The_event_order_of_a_trade_is_fixed()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 3, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 3, health: 2);

            IReadOnlyList<GameEvent> events = TestFactory.Attack(engine, attacker.Id, defender.Id).Events;

            Assert.That(TypeNames(events), Is.EqualTo(new List<string>
            {
                nameof(AttackDeclaredEvent),
                nameof(DamageDealtEvent),
                nameof(DamageDealtEvent),
                nameof(MinionDiedEvent),
                nameof(MinionDiedEvent)
            }));

            List<DamageDealtEvent> damage = events.OfType<DamageDealtEvent>().ToList();
            Assert.That(damage[0].TargetId, Is.EqualTo(defender.Id), "The attacker's blow is reported first.");
            Assert.That(damage[0].Amount, Is.EqualTo(2));
            Assert.That(damage[1].TargetId, Is.EqualTo(attacker.Id));
            Assert.That(damage[1].Amount, Is.EqualTo(3));
        }

        [Test]
        public void Both_deaths_happen_in_the_same_death_phase()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 2, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 2, health: 2);

            List<string> names = TypeNames(TestFactory.Attack(engine, attacker.Id, defender.Id).Events);

            // Both damage events come before either death: nothing was removed
            // while the attack was still resolving.
            Assert.That(names.IndexOf(nameof(MinionDiedEvent)),
                Is.GreaterThan(names.LastIndexOf(nameof(DamageDealtEvent))));
        }

        [Test]
        public void Simultaneous_deaths_follow_the_play_order_convention()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            // The defender entered play first, so it is processed first, even
            // though the attacker is the one that started the fight.
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 2, health: 2);
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 2, ready: true);

            List<EntityId> deaths = TestFactory.Attack(engine, attacker.Id, defender.Id)
                .Events.OfType<MinionDiedEvent>().Select(death => death.MinionId).ToList();

            Assert.That(defender.Timestamp, Is.LessThan(attacker.Timestamp));
            Assert.That(deaths, Is.EqualTo(new List<EntityId> { defender.Id, attacker.Id }));
        }

        [Test]
        public void The_board_closes_up_after_a_death()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 5, health: 5, ready: true);

            Minion left = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 5);
            Minion middle = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 1);
            Minion right = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 5);

            MinionDiedEvent death = TestFactory.Attack(engine, attacker.Id, middle.Id)
                .Events.OfType<MinionDiedEvent>().Single();

            Assert.That(death.BoardPosition, Is.EqualTo(1), "The slot it died on is preserved.");

            Zone<Minion> board = engine.State.GetPlayer(active.Opponent).Board;
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board[0], Is.SameAs(left));
            Assert.That(board[1], Is.SameAs(right));
        }

        [Test]
        public void Attacking_costs_the_attacker_its_attack_for_the_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 1, health: 9, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 9);

            TestFactory.Attack(engine, attacker.Id, defender.Id);

            Assert.That(attacker.AttacksThisTurn, Is.EqualTo(1));
            Assert.That(defender.AttacksThisTurn, Is.EqualTo(0), "Defending is not attacking.");
        }

        [Test]
        public void An_attack_creates_no_entity()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            int before = engine.State.EntityCount;
            TestFactory.Attack(engine, attacker.Id, defender.Id);

            Assert.That(engine.State.EntityCount, Is.EqualTo(before));
        }

        [Test]
        public void The_same_situation_always_resolves_the_same_way()
        {
            List<string> Run()
            {
                GameEngine engine = TestFactory.StartedMatch(seed: 4242UL);
                PlayerId active = engine.State.CurrentPlayer;
                Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 3, ready: true);
                Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 3, health: 2);

                List<GameEvent> all = new List<GameEvent>(
                    TestFactory.Attack(engine, attacker.Id, defender.Id).Events);
                all.AddRange(TestFactory.EndTurn(engine).Events);
                return all.Select(e => e.ToString()).ToList();
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
