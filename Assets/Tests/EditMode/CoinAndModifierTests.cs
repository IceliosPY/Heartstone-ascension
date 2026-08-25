using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The Coin, and lasting changes to a minion's statistics.
    ///
    /// The Coin is the card this whole phase was measured against. It works
    /// because its definition says so and for no other reason, and the last test
    /// here proves the negative directly: nothing anywhere reads its id.
    /// </summary>
    public sealed class CoinAndModifierTests
    {
        private static GameEngine Ready(out PlayerId active, int mana = 2)
        {
            GameEngine engine = TestFactory.StartedMatch();
            active = engine.State.CurrentPlayer;

            Player player = engine.State.GetPlayer(active);
            player.MaxMana = mana;
            player.AvailableMana = mana;
            player.TemporaryMana = 0;

            return engine;
        }

        // ------------------------------------------------------------------
        //  The Coin
        // ------------------------------------------------------------------

        [Test]
        public void The_coin_is_a_free_spell_that_can_be_played()
        {
            GameEngine engine = Ready(out PlayerId active);
            CardInstance coin = TestFactory.PutCardInHand(engine, active, "the_coin");

            CardDefinition definition = engine.State.Catalog.Get(coin.CardId);

            Assert.That(definition.Type, Is.EqualTo(CardType.Spell));
            Assert.That(definition.ManaCost, Is.Zero);
            Assert.That(definition.Collectible, Is.False);

            Assert.That(
                engine.CanExecute(new PlayCardCommand(active, coin.Id)),
                Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void The_coin_grants_one_spendable_mana_without_granting_a_crystal()
        {
            GameEngine engine = Ready(out PlayerId active);
            Player player = engine.State.GetPlayer(active);

            CardInstance coin = TestFactory.PutCardInHand(engine, active, "the_coin");

            Assert.That(player.AvailableMana, Is.EqualTo(2));
            Assert.That(player.MaxMana, Is.EqualTo(2));

            CommandResult result = TestFactory.PlayCard(engine, coin.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(player.AvailableMana, Is.EqualTo(3), "The Coin should have given a third mana.");
            Assert.That(player.MaxMana, Is.EqualTo(2), "The Coin must never grant a crystal.");
            Assert.That(player.TemporaryMana, Is.EqualTo(1));
        }

        [Test]
        public void The_mana_from_the_coin_can_actually_be_spent()
        {
            GameEngine engine = Ready(out PlayerId active);
            Player player = engine.State.GetPlayer(active);

            CardInstance coin = TestFactory.PutCardInHand(engine, active, "the_coin");

            // Test Sharpshooter costs three, one more than the two crystals.
            CardInstance expensive = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");
            EntityId enemyHero = engine.State.GetPlayer(active.Opponent).Hero.Id;

            Assert.That(
                engine.CanExecute(new PlayCardCommand(active, expensive.Id, 0, enemyHero)),
                Is.EqualTo(RejectionReason.NotEnoughMana));

            TestFactory.PlayCard(engine, coin.Id);

            Assert.That(
                engine.Execute(new PlayCardCommand(active, expensive.Id, 0, enemyHero)).IsAccepted,
                Is.True,
                "The third mana was not spendable.");

            Assert.That(player.AvailableMana, Is.Zero);
        }

        [Test]
        public void Temporary_mana_is_gone_at_the_next_turn()
        {
            GameEngine engine = Ready(out PlayerId active);
            Player player = engine.State.GetPlayer(active);

            CardInstance coin = TestFactory.PutCardInHand(engine, active, "the_coin");
            TestFactory.PlayCard(engine, coin.Id);

            Assert.That(player.TemporaryMana, Is.EqualTo(1));

            TestFactory.AdvanceToNextTurnOf(engine, active);

            Assert.That(player.TemporaryMana, Is.Zero, "Temporary mana outlived its turn.");
            Assert.That(player.AvailableMana, Is.EqualTo(player.MaxMana));
        }

        [Test]
        public void The_coin_ends_up_in_the_graveyard()
        {
            GameEngine engine = Ready(out PlayerId active);
            Player player = engine.State.GetPlayer(active);

            CardInstance coin = TestFactory.PutCardInHand(engine, active, "the_coin");
            TestFactory.PlayCard(engine, coin.Id);

            Assert.That(player.Hand.Contains(coin), Is.False);
            Assert.That(player.Board.Count, Is.Zero, "A spell must not put anything on the board.");
            Assert.That(player.Graveyard.Contains(coin), Is.True);
            Assert.That(coin.Zone, Is.EqualTo(ZoneType.Graveyard));
        }

        /// <summary>
        /// The negative, checked directly. If any rule recognised The Coin by
        /// name, a card with a different id and the same effect would behave
        /// differently. It does not.
        /// </summary>
        [Test]
        public void A_different_card_with_the_same_effect_behaves_identically()
        {
            CardDefinition impostor = new CardDefinition(
                new CardId("test_not_the_coin"), "Not The Coin", CardType.Spell,
                manaCost: 0, collectible: false,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.OnPlay,
                        new SelectorDefinition(SelectorKind.FriendlyHero),
                        new EffectActionDefinition(EffectActionKind.GainTemporaryMana, 1))
                });

            List<CardDefinition> all = new List<CardDefinition>(TestFactory.StandardCards()) { impostor };

            GameEngine engine = TestFactory.StartedMatch(catalog: new CardCatalog(all));
            PlayerId active = engine.State.CurrentPlayer;
            Player player = engine.State.GetPlayer(active);

            player.MaxMana = 2;
            player.AvailableMana = 2;

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_not_the_coin");
            TestFactory.PlayCard(engine, card.Id);

            Assert.That(player.AvailableMana, Is.EqualTo(3));
            Assert.That(player.MaxMana, Is.EqualTo(2));
            Assert.That(player.TemporaryMana, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------
        //  Modifiers
        // ------------------------------------------------------------------

        [Test]
        public void A_buff_changes_the_effective_statistics_and_not_the_printed_card()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            Minion target = TestFactory.PutMinionOnBoard(engine, active);

            Assert.That(target.Attack, Is.EqualTo(2));
            Assert.That(target.MaxHealth, Is.EqualTo(3));

            CardInstance buff = TestFactory.PutCardInHand(engine, active, "test_buff");
            CommandResult result = engine.Execute(new PlayCardCommand(active, buff.Id, 0, target.Id));

            Assert.That(result.IsAccepted, Is.True);

            Assert.That(target.Attack, Is.EqualTo(3));
            Assert.That(target.MaxHealth, Is.EqualTo(4));
            Assert.That(target.CurrentHealth, Is.EqualTo(4));

            Assert.That(target.IsModified, Is.True);
            Assert.That(target.Modifiers.Count, Is.EqualTo(1));
            Assert.That(target.Modifiers[0].AttackDelta, Is.EqualTo(1));
            Assert.That(target.Modifiers[0].HealthDelta, Is.EqualTo(1));

            // The printed card is untouched, here and in every other match.
            CardDefinition printed = engine.State.Catalog.Get(target.CardId);

            Assert.That(printed.Attack, Is.EqualTo(2));
            Assert.That(printed.Health, Is.EqualTo(3));
            Assert.That(target.BaseAttack, Is.EqualTo(2));
            Assert.That(target.BaseHealth, Is.EqualTo(3));
        }

        /// <summary>
        /// Health goes up, damage does not. A three health minion on one damage
        /// given plus two health has four effective health, not three.
        /// </summary>
        [Test]
        public void Extra_health_leaves_damage_already_taken_alone()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion minion = TestFactory.PutMinionOnBoard(engine, active);
            minion.Damage = 1;

            Assert.That(minion.CurrentHealth, Is.EqualTo(2));

            minion.AddModifier(0, 2);

            Assert.That(minion.MaxHealth, Is.EqualTo(5));
            Assert.That(minion.Damage, Is.EqualTo(1), "Damage must not be touched by a health buff.");
            Assert.That(minion.CurrentHealth, Is.EqualTo(4));
        }

        [Test]
        public void Modifiers_keep_the_order_they_were_applied_in()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion minion = TestFactory.PutMinionOnBoard(engine, engine.State.CurrentPlayer);

            StatModifier first = minion.AddModifier(1, 0);
            StatModifier second = minion.AddModifier(0, 3);
            StatModifier third = minion.AddModifier(2, 2);

            Assert.That(first.Order, Is.EqualTo(1));
            Assert.That(second.Order, Is.EqualTo(2));
            Assert.That(third.Order, Is.EqualTo(3));

            Assert.That(minion.AttackModifier, Is.EqualTo(3));
            Assert.That(minion.HealthModifier, Is.EqualTo(5));

            Assert.That(minion.RemoveModifier(second.Order), Is.True);
            Assert.That(minion.HealthModifier, Is.EqualTo(2));
            Assert.That(minion.Modifiers.Count, Is.EqualTo(2));
        }

        [Test]
        public void A_buff_changes_the_state_fingerprint()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion minion = TestFactory.PutMinionOnBoard(engine, engine.State.CurrentPlayer);

            string before = StateFingerprint.Of(engine.State);

            minion.AddModifier(1, 1);

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }

        [Test]
        public void The_readable_dump_shows_what_a_buff_did()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion minion = TestFactory.PutMinionOnBoard(engine, engine.State.CurrentPlayer);

            minion.AddModifier(1, 1);

            string dump = StateDump.Readable(engine.State);

            Assert.That(dump, Does.Contain("base=2/3"));
            Assert.That(dump, Does.Contain("+1/+1"));
        }

        [Test]
        public void Temporary_mana_is_part_of_the_state_fingerprint()
        {
            GameEngine engine = Ready(out PlayerId active);

            string before = StateFingerprint.Of(engine.State);

            engine.State.GetPlayer(active).TemporaryMana = 1;

            Assert.That(StateFingerprint.Of(engine.State), Is.Not.EqualTo(before));
        }
    }
}
