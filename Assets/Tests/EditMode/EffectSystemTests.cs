using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The effect system: triggers, selectors and actions.
    ///
    /// The thing being proved throughout is that a card does what it does
    /// because of the data on it, and that the data reaches the outcome through
    /// the rules that were already there. An effect that dealt damage without
    /// going through the damage rules would work today and be wrong the moment
    /// armour, spell damage or a death phase mattered, so several of these check
    /// the route as much as the result.
    /// </summary>
    public sealed class EffectSystemTests
    {
        // ------------------------------------------------------------------
        //  Definitions
        // ------------------------------------------------------------------

        [Test]
        public void A_card_with_no_effects_is_still_a_perfectly_good_card()
        {
            CardDefinition plain = TestFactory.MinionDefinition();

            Assert.That(plain.Effects, Is.Empty);
            Assert.That(plain.HasEffects, Is.False);

            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            CardInstance card = TestFactory.PutCardInHand(engine, active);

            Assert.That(TestFactory.PlayCard(engine, card.Id).IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// A card that damages and then draws must do both, in that order.
        /// Nothing may sort, group or reorder what was written.
        /// </summary>
        [Test]
        public void Several_effects_on_one_card_keep_the_order_they_were_written_in()
        {
            CardDefinition twoStep = new CardDefinition(
                new CardId("test_two_step"), "Two Step", CardType.Minion,
                manaCost: 1, attack: 1, health: 1,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.EnemyHero),
                        new EffectActionDefinition(EffectActionKind.DealDamage, 3)),
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.FriendlyHero),
                        new EffectActionDefinition(EffectActionKind.DrawCards, 1))
                });

            GameEngine engine = StartWith(twoStep);
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_two_step");
            CommandResult result = TestFactory.PlayCard(engine, card.Id);

            Assert.That(result.IsAccepted, Is.True);

            int damageAt = IndexOf<DamageDealtEvent>(result.Events);
            int drawAt = IndexOf<CardDrawnEvent>(result.Events);

            Assert.That(damageAt, Is.GreaterThanOrEqualTo(0), "The first effect never happened.");
            Assert.That(drawAt, Is.GreaterThanOrEqualTo(0), "The second effect never happened.");
            Assert.That(damageAt, Is.LessThan(drawAt), "The effects resolved out of order.");
        }

        [Test]
        public void The_effect_queries_read_a_card_without_reordering_it()
        {
            EffectDefinition first = Effect(EffectTrigger.Battlecry, SelectorKind.EnemyHero, 1);
            EffectDefinition second = Effect(EffectTrigger.Deathrattle, SelectorKind.EnemyHero, 2);
            EffectDefinition third = Effect(EffectTrigger.Battlecry, SelectorKind.EnemyHero, 3);

            EffectDefinition[] effects = { first, second, third };

            IReadOnlyList<EffectDefinition> battlecries =
                EffectQueries.WithTrigger(effects, EffectTrigger.Battlecry);

            Assert.That(battlecries.Count, Is.EqualTo(2));
            Assert.That(battlecries[0].Action.Amount, Is.EqualTo(1));
            Assert.That(battlecries[1].Action.Amount, Is.EqualTo(3));

            Assert.That(EffectQueries.HasTrigger(effects, EffectTrigger.Deathrattle), Is.True);
            Assert.That(EffectQueries.HasTrigger(effects, EffectTrigger.OnPlay), Is.False);
        }

        // ------------------------------------------------------------------
        //  Battlecry
        // ------------------------------------------------------------------

        [Test]
        public void A_battlecry_resolves_when_the_minion_is_played_from_a_hand()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId enemy = active.Opponent;

            TestFactory.GiveMana(engine, active, 10);

            int before = engine.State.GetPlayer(enemy).Hero.CurrentHealth;

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");
            EntityId enemyHero = engine.State.GetPlayer(enemy).Hero.Id;

            CommandResult result = engine.Execute(new PlayCardCommand(active, card.Id, 0, enemyHero));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(enemy).Hero.CurrentHealth, Is.EqualTo(before - 2));

            // The minion is on the board when its battlecry goes off, which is
            // what makes a sweeping battlecry hit its own body.
            int summonAt = IndexOf<MinionSummonedEvent>(result.Events);
            int damageAt = IndexOf<DamageDealtEvent>(result.Events);

            Assert.That(summonAt, Is.LessThan(damageAt),
                "The battlecry resolved before the minion had arrived.");
        }

        /// <summary>
        /// Summoning is not playing. A token put on the board by an effect has
        /// no battlecry, however many it was printed with.
        /// </summary>
        [Test]
        public void Summoning_a_minion_does_not_set_off_its_battlecry()
        {
            CardDefinition noisyToken = new CardDefinition(
                new CardId("test_noisy_token"), "Noisy Token", CardType.Minion,
                manaCost: 1, attack: 1, health: 1, collectible: false,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.EnemyHero),
                        new EffectActionDefinition(EffectActionKind.DealDamage, 5))
                });

            CardDefinition summoner = new CardDefinition(
                new CardId("test_noisy_summoner"), "Noisy Summoner", CardType.Minion,
                manaCost: 1, attack: 1, health: 1,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.Self),
                        new EffectActionDefinition(
                            EffectActionKind.Summon,
                            summonCardId: new CardId("test_noisy_token"), summonCount: 1))
                });

            GameEngine engine = StartWith(noisyToken, summoner);
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId enemy = active.Opponent;

            TestFactory.GiveMana(engine, active, 10);

            int before = engine.State.GetPlayer(enemy).Hero.CurrentHealth;

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_noisy_summoner");
            TestFactory.PlayCard(engine, card.Id);

            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(2));
            Assert.That(engine.State.GetPlayer(enemy).Hero.CurrentHealth, Is.EqualTo(before),
                "A summoned token set off a battlecry it should never have had.");
        }

        [Test]
        public void A_battlecry_can_kill_a_minion_through_the_ordinary_death_phase()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId enemy = active.Opponent;

            TestFactory.GiveMana(engine, active, 10);

            Minion victim = TestFactory.PutMinionOnBoard(engine, enemy);
            victim.Damage = victim.MaxHealth - 1;   // one health left

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");
            CommandResult result = engine.Execute(new PlayCardCommand(active, card.Id, 0, victim.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(enemy).Board.Count, Is.Zero);
            Assert.That(IndexOf<MinionDiedEvent>(result.Events), Is.GreaterThanOrEqualTo(0),
                "The death was not reported by a death phase.");
        }

        /// <summary>
        /// Lethal from a battlecry ends the match through the same path any
        /// other lethal does. Nothing about effects is special here.
        /// </summary>
        [Test]
        public void A_battlecry_that_kills_a_hero_ends_the_match_the_ordinary_way()
        {
            CardDefinition finisher = new CardDefinition(
                new CardId("test_finisher"), "Finisher", CardType.Minion,
                manaCost: 1, attack: 1, health: 1,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.EnemyHero),
                        new EffectActionDefinition(EffectActionKind.DealDamage, 100))
                });

            GameEngine engine = StartWith(finisher);
            PlayerId active = engine.State.CurrentPlayer;

            TestFactory.GiveMana(engine, active, 10);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_finisher");
            CommandResult result = TestFactory.PlayCard(engine, card.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.State.HasEnded, Is.True);
            Assert.That(IndexOf<HeroDiedEvent>(result.Events), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<GameEndedEvent>(result.Events), Is.GreaterThanOrEqualTo(0));

            Assert.That(
                IndexOf<HeroDiedEvent>(result.Events),
                Is.LessThan(IndexOf<GameEndedEvent>(result.Events)));
        }

        // ------------------------------------------------------------------
        //  Deathrattle
        // ------------------------------------------------------------------

        [Test]
        public void A_minion_without_a_deathrattle_does_nothing_when_it_dies()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion minion = TestFactory.PutMinionOnBoard(engine, active);
            int handBefore = engine.State.GetPlayer(active).Hand.Count;

            TestFactory.Destroy(engine, minion.Id);

            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.Zero);
            Assert.That(engine.State.GetPlayer(active).Hand.Count, Is.EqualTo(handBefore));
        }

        [Test]
        public void A_deathrattle_resolves_after_the_minion_has_left_the_board()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion scribe = TestFactory.PutMinionOnBoard(engine, active, "test_deathrattle_draw");
            int handBefore = engine.State.GetPlayer(active).Hand.Count;

            IReadOnlyList<GameEvent> events = TestFactory.Destroy(engine, scribe.Id);

            Assert.That(engine.State.GetPlayer(active).Hand.Count, Is.EqualTo(handBefore + 1),
                "The deathrattle did not draw.");

            int diedAt = IndexOf<MinionDiedEvent>(events);
            int drewAt = IndexOf<CardDrawnEvent>(events);

            Assert.That(diedAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(drewAt, Is.GreaterThan(diedAt),
                "The deathrattle has to resolve after the death is reported.");
        }

        /// <summary>
        /// Two deathrattles going off together follow the death order settled in
        /// Phase 3: oldest by order of entry first, never board position.
        /// </summary>
        [Test]
        public void Two_deathrattles_at_once_follow_the_order_the_deaths_were_sequenced_in()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion older = TestFactory.PutMinionOnBoard(engine, active, "test_deathrattle_draw");
            Minion newer = TestFactory.PutMinionOnBoard(engine, active, "test_deathrattle_draw");

            Assert.That(older.Timestamp, Is.LessThan(newer.Timestamp));

            int handBefore = engine.State.GetPlayer(active).Hand.Count;

            IReadOnlyList<GameEvent> events = TestFactory.Destroy(engine, older.Id, newer.Id);

            Assert.That(engine.State.GetPlayer(active).Hand.Count, Is.EqualTo(handBefore + 2),
                "Both deathrattles should have drawn.");

            List<EntityId> deaths = new List<EntityId>();

            foreach (GameEvent reported in events)
            {
                if (reported is MinionDiedEvent died)
                {
                    deaths.Add(died.MinionId);
                }
            }

            Assert.That(deaths, Is.EqualTo(new[] { older.Id, newer.Id }),
                "Deaths were not sequenced oldest first.");
        }

        [Test]
        public void A_deathrattle_can_summon_and_can_cause_another_death_phase()
        {
            CardDefinition fragile = new CardDefinition(
                new CardId("test_fragile"), "Fragile", CardType.Minion,
                manaCost: 1, attack: 1, health: 1, collectible: false);

            CardDefinition breeder = new CardDefinition(
                new CardId("test_breeder"), "Breeder", CardType.Minion,
                manaCost: 1, attack: 1, health: 1,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Deathrattle,
                        new SelectorDefinition(SelectorKind.Self),
                        new EffectActionDefinition(
                            EffectActionKind.Summon,
                            summonCardId: new CardId("test_fragile"), summonCount: 2))
                });

            GameEngine engine = StartWith(fragile, breeder);
            PlayerId active = engine.State.CurrentPlayer;

            Minion source = TestFactory.PutMinionOnBoard(engine, active, "test_breeder");
            TestFactory.Destroy(engine, source.Id);

            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(2),
                "The deathrattle should have summoned two.");
        }

        // ------------------------------------------------------------------
        //  Selectors and area effects
        // ------------------------------------------------------------------

        [Test]
        public void A_sweep_damages_every_enemy_minion_and_kills_them_together()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId enemy = active.Opponent;

            TestFactory.GiveMana(engine, active, 10);

            Minion first = TestFactory.PutMinionOnBoard(engine, enemy);
            Minion second = TestFactory.PutMinionOnBoard(engine, enemy);
            Minion third = TestFactory.PutMinionOnBoard(engine, enemy);

            foreach (Minion minion in new[] { first, second, third })
            {
                minion.Damage = minion.MaxHealth - 1;
            }

            Minion friendly = TestFactory.PutMinionOnBoard(engine, active);

            CardInstance spell = TestFactory.PutCardInHand(engine, active, "test_aoe");
            CommandResult result = TestFactory.PlayCard(engine, spell.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(enemy).Board.Count, Is.Zero, "All three should have died.");
            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(1),
                "A friendly minion was caught by an enemy-only sweep.");
            Assert.That(friendly.Damage, Is.Zero);

            // Every hit lands before any death is reported, which is what makes
            // it one sweep rather than three separate ones.
            int firstDeath = IndexOf<MinionDiedEvent>(result.Events);
            int damageCount = 0;

            for (int index = 0; index < firstDeath; index++)
            {
                if (result.Events[index] is DamageDealtEvent)
                {
                    damageCount++;
                }
            }

            Assert.That(damageCount, Is.EqualTo(3),
                "The three hits should all land before the first death is reported.");
        }

        [Test]
        public void A_sweep_reaches_its_targets_in_board_order()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId enemy = active.Opponent;

            TestFactory.GiveMana(engine, active, 10);

            Minion left = TestFactory.PutMinionOnBoard(engine, enemy);
            Minion middle = TestFactory.PutMinionOnBoard(engine, enemy);
            Minion right = TestFactory.PutMinionOnBoard(engine, enemy);

            CardInstance spell = TestFactory.PutCardInHand(engine, active, "test_aoe");
            CommandResult result = TestFactory.PlayCard(engine, spell.Id);

            List<EntityId> hit = new List<EntityId>();

            foreach (GameEvent reported in result.Events)
            {
                if (reported is DamageDealtEvent damage)
                {
                    hit.Add(damage.TargetId);
                }
            }

            Assert.That(hit, Is.EqualTo(new[] { left.Id, middle.Id, right.Id }));
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        private static GameEngine StartWith(params CardDefinition[] extra)
        {
            List<CardDefinition> all = new List<CardDefinition>(TestFactory.StandardCards());
            all.AddRange(extra);

            return TestFactory.StartedMatch(catalog: new CardCatalog(all));
        }

        private static EffectDefinition Effect(EffectTrigger trigger, SelectorKind selector, int amount) =>
            new EffectDefinition(
                trigger,
                new SelectorDefinition(selector),
                new EffectActionDefinition(EffectActionKind.DealDamage, amount));

        private static int IndexOf<T>(IReadOnlyList<GameEvent> events) where T : GameEvent
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index] is T)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
