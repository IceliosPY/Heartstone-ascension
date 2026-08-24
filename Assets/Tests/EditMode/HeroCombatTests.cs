using System.Collections.Generic;
using System.Linq;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Attacking the enemy hero, and the armour that stands in front of it.
    ///
    /// A hero being attacked never strikes back. That is a Hearthstone rule,
    /// not a shortcut: only a defending minion retaliates, whatever attack the
    /// hero may have.
    /// </summary>
    public sealed class HeroCombatTests
    {
        [Test]
        public void A_minion_can_attack_the_enemy_hero()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 4, health: 5, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            IReadOnlyList<GameEvent> events = TestFactory.Attack(engine, attacker.Id, enemyHero.Id).Events;

            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(26), "Thirty health, took four.");
            Assert.That(attacker.Damage, Is.EqualTo(0));
            Assert.That(attacker.AttacksThisTurn, Is.EqualTo(1));
            Assert.That(events.OfType<DamageDealtEvent>().Count(), Is.EqualTo(1), "One blow, one way.");
        }

        [Test]
        public void The_hero_does_not_strike_back_even_with_an_attack_value()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 3, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            // As if the hero were holding a weapon.
            enemyHero.AttackModifier = 7;
            Assert.That(enemyHero.Attack, Is.EqualTo(7));

            TestFactory.Attack(engine, attacker.Id, enemyHero.Id);

            Assert.That(attacker.Damage, Is.EqualTo(0), "Only defending minions retaliate.");
            Assert.That(attacker.IsInPlay, Is.True);
            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(28));
        }

        [Test]
        public void The_event_order_of_a_hero_attack_is_fixed()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 3, health: 3, ready: true);

            IReadOnlyList<GameEvent> events =
                TestFactory.Attack(engine, attacker.Id, TestFactory.EnemyHero(engine).Id).Events;

            Assert.That(events.Select(e => e.GetType().Name), Is.EqualTo(new List<string>
            {
                nameof(AttackDeclaredEvent),
                nameof(DamageDealtEvent)
            }));
        }

        [Test]
        public void Armor_is_spent_before_health()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 5, health: 5, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);
            enemyHero.Armor = 3;

            DamageDealtEvent damage = TestFactory.Attack(engine, attacker.Id, enemyHero.Id)
                .Events.OfType<DamageDealtEvent>().Single();

            Assert.That(enemyHero.Armor, Is.EqualTo(0));
            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(28), "Five damage, three soaked, two through.");
            Assert.That(damage.Amount, Is.EqualTo(5), "The blow was still worth five.");
            Assert.That(damage.AbsorbedByArmor, Is.EqualTo(3));
            Assert.That(damage.RemainingHealth, Is.EqualTo(28));
        }

        [Test]
        public void Armor_alone_can_absorb_the_whole_blow()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 5, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);
            enemyHero.Armor = 3;

            TestFactory.Attack(engine, attacker.Id, enemyHero.Id);

            Assert.That(enemyHero.Armor, Is.EqualTo(1));
            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(30), "Not a point of health lost.");
            Assert.That(enemyHero.Damage, Is.EqualTo(0));
        }

        [Test]
        public void Armor_exactly_matching_the_blow_leaves_health_alone()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 3, health: 5, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);
            enemyHero.Armor = 3;

            TestFactory.Attack(engine, attacker.Id, enemyHero.Id);

            Assert.That(enemyHero.Armor, Is.EqualTo(0));
            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(30));
        }

        [Test]
        public void Armor_protects_against_fatigue_too()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(active);
            TestFactory.EmptyDeck(player);
            player.Hero.Armor = 1;

            // Same damage rules whatever the source: one shared implementation.
            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            Assert.That(player.Hero.Armor, Is.EqualTo(0));
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(30));
        }

        [Test]
        public void A_lethal_attack_ends_the_match_once()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(startingHeroHealth: 4));
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 4, health: 5, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            IReadOnlyList<GameEvent> events = TestFactory.Attack(engine, attacker.Id, enemyHero.Id).Events;

            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(0));
            Assert.That(enemyHero.HasDied, Is.True);
            Assert.That(engine.State.HasEnded, Is.True);
            Assert.That(engine.State.Winner, Is.EqualTo(active));

            Assert.That(events.OfType<HeroDiedEvent>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<GameEndedEvent>().Count(), Is.EqualTo(1));
            Assert.That(events.Last(), Is.TypeOf<GameEndedEvent>(), "The end is the last thing reported.");
        }

        [Test]
        public void Armor_can_keep_a_hero_alive_through_lethal_damage()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(startingHeroHealth: 3));
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 5, health: 5, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);
            enemyHero.Armor = 10;

            TestFactory.Attack(engine, attacker.Id, enemyHero.Id);

            Assert.That(enemyHero.Armor, Is.EqualTo(5));
            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(3));
            Assert.That(engine.State.HasEnded, Is.False);
        }
    }
}
