using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The split between an immutable card definition and its mutable runtime
    /// state is the single most important invariant of the data model. If a
    /// buff ever leaked into the definition, every copy of that card, in every
    /// match, would silently change.
    /// </summary>
    public sealed class CardStateTests
    {
        private static CardId MinionId => new CardId(TestFactory.MinionCardId);

        [Test]
        public void Changing_a_card_instance_leaves_its_definition_alone()
        {
            GameState game = TestFactory.Game();
            CardDefinition definition = game.Catalog.Get(MinionId);

            CardInstance card = game.CreateCardInstance(MinionId, PlayerId.One);
            card.CostModifier = -1;
            card.AttackModifier = 2;
            card.HealthModifier = 2;

            Assert.That(definition.ManaCost, Is.EqualTo(2), "Printed cost must not move.");
            Assert.That(definition.Attack, Is.EqualTo(2));
            Assert.That(definition.Health, Is.EqualTo(3));

            // The instance reflects the change, the definition does not.
            Assert.That(card.GetCost(game.Catalog), Is.EqualTo(1));
        }

        [Test]
        public void Two_instances_of_a_card_are_independent()
        {
            GameState game = TestFactory.Game();

            CardInstance discounted = game.CreateCardInstance(MinionId, PlayerId.One);
            CardInstance untouched = game.CreateCardInstance(MinionId, PlayerId.One);

            discounted.CostModifier = -2;

            Assert.That(discounted.GetCost(game.Catalog), Is.EqualTo(0));
            Assert.That(untouched.GetCost(game.Catalog), Is.EqualTo(2));
            Assert.That(discounted.Id, Is.Not.EqualTo(untouched.Id));
        }

        [Test]
        public void The_catalog_always_hands_back_the_same_definition_object()
        {
            GameState game = TestFactory.Game();

            Assert.That(game.Catalog.Get(MinionId), Is.SameAs(game.Catalog.Get(MinionId)));
        }

        [Test]
        public void A_minion_copies_its_base_statistics_from_the_definition()
        {
            GameState game = TestFactory.Game();

            Minion minion = game.CreateMinion(MinionId, PlayerId.One);

            Assert.That(minion.CardId, Is.EqualTo(MinionId));
            Assert.That(minion.BaseAttack, Is.EqualTo(2));
            Assert.That(minion.BaseHealth, Is.EqualTo(3));
            Assert.That(minion.Attack, Is.EqualTo(2));
            Assert.That(minion.MaxHealth, Is.EqualTo(3));
            Assert.That(minion.CurrentHealth, Is.EqualTo(3));
            Assert.That(minion.MaxAttacksPerTurn, Is.EqualTo(1));
            Assert.That(minion.AttacksThisTurn, Is.EqualTo(0));
        }

        [Test]
        public void A_buffed_and_damaged_minion_needs_no_change_to_its_definition()
        {
            GameState game = TestFactory.Game();
            CardDefinition definition = game.Catalog.Get(MinionId);

            // The example from the design brief: a printed 2 mana 2/3 that is a
            // 4/5 on the board having taken 2 damage.
            Minion minion = game.CreateMinion(MinionId, PlayerId.One);
            minion.AttackModifier = 2;
            minion.HealthModifier = 2;
            minion.Damage = 2;

            Assert.That(minion.Attack, Is.EqualTo(4));
            Assert.That(minion.MaxHealth, Is.EqualTo(5));
            Assert.That(minion.CurrentHealth, Is.EqualTo(3));
            Assert.That(minion.IsDamaged, Is.True);

            Assert.That(definition.Attack, Is.EqualTo(2));
            Assert.That(definition.Health, Is.EqualTo(3));
            Assert.That(definition.ManaCost, Is.EqualTo(2));
        }

        [Test]
        public void Damage_survives_an_expiring_health_buff()
        {
            GameState game = TestFactory.Game();

            // This is exactly why damage is stored instead of current health.
            Minion minion = game.CreateMinion(MinionId, PlayerId.One);
            minion.HealthModifier = 2;   // 2/5
            minion.Damage = 4;           // 1 health left

            Assert.That(minion.CurrentHealth, Is.EqualTo(1));

            minion.HealthModifier = 0;   // the buff expires, back to 3 max health

            Assert.That(minion.MaxHealth, Is.EqualTo(3));
            Assert.That(minion.CurrentHealth, Is.EqualTo(-1));
        }

        [Test]
        public void A_hero_starts_at_full_health_with_no_armor()
        {
            GameState game = TestFactory.Game();
            Hero hero = game.GetPlayer(PlayerId.One).Hero;

            Assert.That(hero.MaxHealth, Is.EqualTo(30));
            Assert.That(hero.CurrentHealth, Is.EqualTo(30));
            Assert.That(hero.Armor, Is.EqualTo(0));
            Assert.That(hero.Attack, Is.EqualTo(0));
            Assert.That(hero.MaxAttacksPerTurn, Is.EqualTo(1));
        }

        [Test]
        public void A_hero_tracks_armor_and_damage_separately()
        {
            GameState game = TestFactory.Game();
            Hero hero = game.GetPlayer(PlayerId.One).Hero;

            hero.Armor = 5;
            hero.Damage = 4;

            // How armor absorbs damage is a rule and is not implemented yet;
            // what matters here is that both values can be represented at once.
            Assert.That(hero.Armor, Is.EqualTo(5));
            Assert.That(hero.CurrentHealth, Is.EqualTo(26));
        }

        [Test]
        public void A_new_match_starts_with_empty_zones_and_no_current_player()
        {
            GameState game = TestFactory.Game();
            Player player = game.GetPlayer(PlayerId.One);

            Assert.That(player.Deck.Count, Is.EqualTo(0));
            Assert.That(player.Hand.Count, Is.EqualTo(0));
            Assert.That(player.Board.Count, Is.EqualTo(0));
            Assert.That(player.Graveyard.Count, Is.EqualTo(0));
            Assert.That(player.Hand.Capacity, Is.EqualTo(10));
            Assert.That(player.Board.Capacity, Is.EqualTo(7));
            Assert.That(player.Deck.HasCapacityLimit, Is.False);

            // Nothing has started: choosing who goes first is a rule.
            Assert.That(game.CurrentPlayer.IsNone, Is.True);
            Assert.That(game.TurnNumber, Is.EqualTo(0));
            Assert.That(player.MaxMana, Is.EqualTo(0));
            Assert.That(player.FatigueCounter, Is.EqualTo(0));
        }

        [Test]
        public void Players_face_each_other()
        {
            GameState game = TestFactory.Game();

            Assert.That(game.Players.Count, Is.EqualTo(2));
            Assert.That(game.GetOpponentOf(PlayerId.One), Is.SameAs(game.GetPlayer(PlayerId.Two)));
            Assert.That(game.GetOpponentOf(PlayerId.Two), Is.SameAs(game.GetPlayer(PlayerId.One)));
        }

        [Test]
        public void Creating_a_card_outside_the_catalog_is_refused()
        {
            GameState game = TestFactory.Game();

            Assert.Throws<System.ArgumentException>(
                () => game.CreateMinion(new CardId("does_not_exist"), PlayerId.One));
        }
    }
}
