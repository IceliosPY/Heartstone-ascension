using System.Linq;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The turn draw and the two ways it can go wrong: a full hand, and an
    /// empty deck.
    /// </summary>
    public sealed class DrawAndFatigueTests
    {
        /// <summary>
        /// Ends turns until the given player is active again, returning the
        /// result of the command that brought their turn round.
        /// </summary>
        private static CommandResult PassBackTo(GameEngine engine, PlayerId player)
        {
            CommandResult result = TestFactory.EndTurn(engine);

            if (engine.State.CurrentPlayer != player && !engine.State.HasEnded)
            {
                result = TestFactory.EndTurn(engine);
            }

            return result;
        }

        [Test]
        public void Starting_a_turn_draws_one_card()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);

            // Three dealt plus the first turn draw.
            Assert.That(player.Hand.Count, Is.EqualTo(4));

            int handBefore = player.Hand.Count;
            PassBackTo(engine, starting);

            Assert.That(player.Hand.Count, Is.EqualTo(handBefore + 1));
        }

        [Test]
        public void Drawing_takes_the_card_off_the_top_of_the_deck()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);

            EntityId expected = player.Deck[0].Id;
            int deckBefore = player.Deck.Count;

            PassBackTo(engine, starting);

            Assert.That(player.Deck.Count, Is.EqualTo(deckBefore - 1));
            Assert.That(player.Hand.Last().Id, Is.EqualTo(expected));
            Assert.That(player.Deck.Any(card => card.Id == expected), Is.False);
        }

        [Test]
        public void Drawing_preserves_the_order_of_the_rest_of_the_deck()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);

            var expectedRemainder = player.Deck.Skip(1).Select(card => card.Id.Value).ToList();

            PassBackTo(engine, starting);

            Assert.That(player.Deck.Select(card => card.Id.Value), Is.EqualTo(expectedRemainder));
        }

        [Test]
        public void A_drawn_card_is_marked_as_being_in_hand()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Player player = engine.State.GetPlayer(engine.State.StartingPlayer);

            Assert.That(player.Hand.All(card => card.Zone == ZoneType.Hand), Is.True);
        }

        [Test]
        public void A_full_hand_burns_the_next_card()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);

            TestFactory.FillHandFromDeck(player, 10);
            EntityId doomed = player.Deck[0].Id;
            int deckBefore = player.Deck.Count;

            PassBackTo(engine, starting);

            Assert.That(player.Hand.Count, Is.EqualTo(10), "The hand cannot grow past its capacity.");
            Assert.That(player.Deck.Count, Is.EqualTo(deckBefore - 1), "The card still left the deck.");
            Assert.That(player.Hand.Any(card => card.Id == doomed), Is.False);
            Assert.That(player.Deck.Any(card => card.Id == doomed), Is.False);
            Assert.That(player.Graveyard.Any(card => card.Id == doomed), Is.True);
        }

        [Test]
        public void Burning_a_card_reports_it()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);
            TestFactory.FillHandFromDeck(player, 10);

            TestFactory.EndTurn(engine);
            CommandResult result = TestFactory.EndTurn(engine);

            Assert.That(result.Events.OfType<CoH.Core.Events.CardBurnedEvent>().Count(), Is.EqualTo(1));
            Assert.That(result.Events.OfType<CoH.Core.Events.CardDrawnEvent>()
                .Any(drawn => drawn.PlayerId == starting), Is.False,
                "A burned card is never reported as drawn.");
        }

        [Test]
        public void An_empty_deck_deals_one_two_then_three_fatigue()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);
            TestFactory.EmptyDeck(player);

            PassBackTo(engine, starting);
            Assert.That(player.FatigueCounter, Is.EqualTo(1));
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(29));

            PassBackTo(engine, starting);
            Assert.That(player.FatigueCounter, Is.EqualTo(2));
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(27));

            PassBackTo(engine, starting);
            Assert.That(player.FatigueCounter, Is.EqualTo(3));
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(24));

            PassBackTo(engine, starting);
            Assert.That(player.FatigueCounter, Is.EqualTo(4));
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(20));
        }

        [Test]
        public void Fatigue_only_hits_the_player_whose_deck_is_empty()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            TestFactory.EmptyDeck(engine.State.GetPlayer(starting));

            PassBackTo(engine, starting);

            Assert.That(engine.State.GetPlayer(starting).FatigueCounter, Is.EqualTo(1));
            Assert.That(engine.State.GetPlayer(starting.Opponent).FatigueCounter, Is.EqualTo(0));
            Assert.That(engine.State.GetPlayer(starting.Opponent).Hero.CurrentHealth, Is.EqualTo(30));
        }

        [Test]
        public void Fatigue_is_absorbed_by_armor_before_health()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);
            TestFactory.EmptyDeck(player);
            player.Hero.Armor = 1;

            PassBackTo(engine, starting);

            Assert.That(player.Hero.Armor, Is.EqualTo(0));
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(30), "One point of armor soaked the first tick.");
        }

        [Test]
        public void Fatigue_can_end_the_match()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(startingHeroHealth: 3));
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);
            TestFactory.EmptyDeck(player);

            PassBackTo(engine, starting);
            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(2));
            Assert.That(engine.State.HasEnded, Is.False);

            PassBackTo(engine, starting);

            Assert.That(player.Hero.CurrentHealth, Is.EqualTo(0));
            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Ended));
            Assert.That(engine.State.Winner, Is.EqualTo(starting.Opponent));
            Assert.That(engine.State.CurrentPlayer.IsNone, Is.True);
        }

        [Test]
        public void Ending_a_turn_after_the_match_is_over_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(startingHeroHealth: 3));
            PlayerId starting = engine.State.StartingPlayer;
            TestFactory.EmptyDeck(engine.State.GetPlayer(starting));

            PassBackTo(engine, starting);
            PassBackTo(engine, starting);
            Assert.That(engine.State.HasEnded, Is.True);

            CommandResult result = engine.Execute(new EndTurnCommand(starting));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.GameAlreadyEnded));
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void The_match_reports_a_winner_exactly_once()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(startingHeroHealth: 3));
            PlayerId starting = engine.State.StartingPlayer;
            TestFactory.EmptyDeck(engine.State.GetPlayer(starting));

            PassBackTo(engine, starting);
            CommandResult lethal = PassBackTo(engine, starting);

            var endings = lethal.Events.OfType<CoH.Core.Events.GameEndedEvent>().ToList();
            Assert.That(endings, Has.Count.EqualTo(1));
            Assert.That(endings[0].IsDraw, Is.False);
            Assert.That(endings[0].Winner, Is.EqualTo(starting.Opponent));
        }
    }
}
