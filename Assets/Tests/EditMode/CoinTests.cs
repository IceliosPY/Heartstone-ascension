using System.Linq;
using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The extra card handed to the player going second, The Coin in Hearthstone
    /// terms.
    ///
    /// It is an ordinary non-collectible card as far as the engine is concerned:
    /// its id comes from configuration, nothing asks "is this The Coin?", and it
    /// grants nothing on its own. Its mana effect arrives with the effect
    /// system, like any other card's.
    /// </summary>
    public sealed class CoinTests
    {
        private static Player Starting(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.StartingPlayer);

        private static Player Second(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.StartingPlayer.Opponent);

        private static int CoinCount(Player player) =>
            player.Hand.Count(card => card.CardId == TestFactory.CoinCardId);

        [Test]
        public void Only_the_player_going_second_receives_it()
        {
            GameEngine engine = TestFactory.StartedMatch();

            Assert.That(CoinCount(Second(engine)), Is.EqualTo(1));
            Assert.That(CoinCount(Starting(engine)), Is.EqualTo(0));
        }

        [Test]
        public void It_arrives_only_once_the_mulligan_is_resolved()
        {
            GameEngine inMulligan = TestFactory.MatchInMulligan();
            Assert.That(CoinCount(Second(inMulligan)), Is.EqualTo(0));

            GameEngine started = TestFactory.StartedMatch();
            Assert.That(CoinCount(Second(started)), Is.EqualTo(1));
        }

        [Test]
        public void It_is_the_last_card_of_the_opening_hand()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Player second = Second(engine);

            Assert.That(second.Hand.Count, Is.EqualTo(5), "Four dealt plus the extra card.");
            Assert.That(second.Hand[4].CardId, Is.EqualTo(TestFactory.CoinCardId));
        }

        [Test]
        public void It_is_not_collectible()
        {
            CardDefinition definition = TestFactory.Catalog().Get(TestFactory.CoinCardId);

            Assert.That(definition.Collectible, Is.False);
            Assert.That(TestFactory.MinionDefinition().Collectible, Is.True);
        }

        [Test]
        public void It_never_came_from_a_deck()
        {
            GameEngine engine = TestFactory.StartedMatch(deckSize: 30);
            Player second = Second(engine);

            // Four cards left the deck for the opening hand, and none of the
            // deck's cards is the extra one.
            Assert.That(second.Deck.Count, Is.EqualTo(26));
            Assert.That(second.Deck.Any(card => card.CardId == TestFactory.CoinCardId), Is.False);
        }

        [Test]
        public void Holding_it_grants_no_mana_by_itself()
        {
            GameEngine engine = TestFactory.StartedMatch();

            // Play through to the second player's first turn.
            TestFactory.EndTurn(engine);

            Player second = Second(engine);
            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(second.Id));
            Assert.That(CoinCount(second), Is.EqualTo(1));
            Assert.That(second.TemporaryMana, Is.EqualTo(0));
            Assert.That(second.MaxMana, Is.EqualTo(1));
            Assert.That(second.AvailableMana, Is.EqualTo(1), "The extra card must not add mana on its own.");
        }

        [Test]
        public void It_is_a_normal_entity_owned_by_its_holder()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Player second = Second(engine);
            CardInstance coin = second.Hand.First(card => card.CardId == TestFactory.CoinCardId);

            Assert.That(coin.Owner, Is.EqualTo(second.Id));
            Assert.That(coin.Controller, Is.EqualTo(second.Id));
            Assert.That(coin.Zone, Is.EqualTo(ZoneType.Hand));
            Assert.That(engine.State.GetEntity(coin.Id), Is.SameAs(coin));
        }

        [Test]
        public void The_extra_card_comes_from_configuration_not_from_a_hardcoded_coin()
        {
            CardId customBonus = new CardId("custom_bonus");
            GameConfig config = new GameConfig(secondPlayerExtraCard: customBonus);
            CardCatalog catalog = TestFactory.Catalog(
                TestFactory.MinionDefinition(),
                new CardDefinition(customBonus, "Custom Bonus", CardType.Spell, 0, collectible: false));

            GameEngine engine = TestFactory.StartedMatch(config: config, catalog: catalog);
            Player second = Second(engine);

            Assert.That(second.Hand[4].CardId, Is.EqualTo(customBonus));
            Assert.That(CoinCount(second), Is.EqualTo(0), "Nothing in the engine knows about The Coin by name.");
        }

        [Test]
        public void Setup_fails_fast_when_the_configured_extra_card_is_missing()
        {
            GameConfig config = new GameConfig(secondPlayerExtraCard: new CardId("not_in_catalog"));

            Assert.Throws<System.InvalidOperationException>(
                () => TestFactory.MatchInMulligan(config: config));
        }
    }
}
