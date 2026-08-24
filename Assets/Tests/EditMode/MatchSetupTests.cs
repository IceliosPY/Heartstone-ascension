using System.Collections.Generic;
using System.Linq;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Match setup is where randomness first touches a match. Everything it
    /// decides, the deck orders and who goes first, has to come from the seed
    /// alone so that the same inputs always rebuild the same match.
    /// </summary>
    public sealed class MatchSetupTests
    {
        private static List<int> DeckOrder(GameEngine engine, PlayerId seat) =>
            engine.State.GetPlayer(seat).Deck.Select(card => card.Id.Value).ToList();

        [Test]
        public void Setup_leaves_the_match_in_the_mulligan_phase()
        {
            GameEngine engine = TestFactory.MatchInMulligan();

            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Mulligan));
            Assert.That(engine.State.CurrentPlayer.IsNone, Is.True, "No turn has begun yet.");
            Assert.That(engine.State.TurnNumber, Is.EqualTo(0));
        }

        [Test]
        public void The_same_seed_picks_the_same_starting_player()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                PlayerId left = TestFactory.MatchInMulligan(seed).State.StartingPlayer;
                PlayerId right = TestFactory.MatchInMulligan(seed).State.StartingPlayer;

                Assert.That(right, Is.EqualTo(left), "Divergence for seed " + seed);
            }
        }

        [Test]
        public void Both_players_can_end_up_starting()
        {
            HashSet<PlayerId> seen = new HashSet<PlayerId>();

            for (ulong seed = 1; seed <= 30; seed++)
            {
                seen.Add(TestFactory.MatchInMulligan(seed).State.StartingPlayer);
            }

            Assert.That(seen, Has.Count.EqualTo(2), "The coin flip should reach both seats.");
        }

        [Test]
        public void The_starting_player_is_always_a_real_player()
        {
            GameEngine engine = TestFactory.MatchInMulligan();

            Assert.That(engine.State.StartingPlayer.IsNone, Is.False);
            Assert.That(
                engine.State.StartingPlayer.Opponent,
                Is.EqualTo(engine.State.StartingPlayer == PlayerId.One ? PlayerId.Two : PlayerId.One));
        }

        [Test]
        public void The_same_seed_shuffles_both_decks_the_same_way()
        {
            GameEngine left = TestFactory.MatchInMulligan(seed: 777UL);
            GameEngine right = TestFactory.MatchInMulligan(seed: 777UL);

            Assert.That(DeckOrder(right, PlayerId.One), Is.EqualTo(DeckOrder(left, PlayerId.One)));
            Assert.That(DeckOrder(right, PlayerId.Two), Is.EqualTo(DeckOrder(left, PlayerId.Two)));
        }

        [Test]
        public void Different_seeds_shuffle_decks_differently()
        {
            HashSet<string> orders = new HashSet<string>();

            for (ulong seed = 1; seed <= 8; seed++)
            {
                orders.Add(string.Join(",", DeckOrder(TestFactory.MatchInMulligan(seed), PlayerId.One)));
            }

            Assert.That(orders.Count, Is.GreaterThan(1), "Eight seeds should not all shuffle alike.");
        }

        [Test]
        public void The_two_decks_are_shuffled_independently()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 4242UL);

            // Both seats were built from the same deck list, so identical order
            // after shuffling would mean the two shuffles were correlated.
            List<int> seatOne = DeckOrder(engine, PlayerId.One).Select(id => id - 2).ToList();
            List<int> seatTwo = DeckOrder(engine, PlayerId.Two).Select(id => id - 32).ToList();

            Assert.That(seatTwo, Is.Not.EqualTo(seatOne));
        }

        [Test]
        public void Shuffling_keeps_every_card_of_the_deck_list()
        {
            GameEngine engine = TestFactory.MatchInMulligan(deckSize: 30);
            Player seatOne = engine.State.GetPlayer(PlayerId.One);

            // Three or four went to the opening hand, the rest are still there.
            int inHand = seatOne.Hand.Count;
            Assert.That(seatOne.Deck.Count + inHand, Is.EqualTo(30));
            Assert.That(DeckOrder(engine, PlayerId.One).Distinct().Count(), Is.EqualTo(seatOne.Deck.Count));
        }

        [Test]
        public void The_starting_player_is_dealt_three_cards()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            Player starting = engine.State.GetPlayer(engine.State.StartingPlayer);

            Assert.That(starting.Hand.Count, Is.EqualTo(3));
        }

        [Test]
        public void The_other_player_is_dealt_four_cards_and_no_extra_card_yet()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            Player second = engine.State.GetPlayer(engine.State.StartingPlayer.Opponent);

            Assert.That(second.Hand.Count, Is.EqualTo(4));
            Assert.That(
                second.Hand.Any(card => card.CardId == TestFactory.CoinCardId),
                Is.False,
                "The extra card is only handed over once the mulligan is done.");
        }

        [Test]
        public void Dealt_cards_leave_the_deck()
        {
            GameEngine engine = TestFactory.MatchInMulligan(deckSize: 30);
            Player starting = engine.State.GetPlayer(engine.State.StartingPlayer);
            Player second = engine.State.GetPlayer(engine.State.StartingPlayer.Opponent);

            Assert.That(starting.Deck.Count, Is.EqualTo(27));
            Assert.That(second.Deck.Count, Is.EqualTo(26));
        }

        [Test]
        public void Cards_in_hand_know_they_are_in_hand()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            Player starting = engine.State.GetPlayer(engine.State.StartingPlayer);

            Assert.That(starting.Hand.All(card => card.Zone == ZoneType.Hand), Is.True);
            Assert.That(starting.Deck.All(card => card.Zone == ZoneType.Deck), Is.True);
        }

        [Test]
        public void Setting_up_twice_is_refused()
        {
            GameEngine engine = TestFactory.MatchInMulligan();

            Assert.Throws<System.InvalidOperationException>(
                () => engine.StartMatch(TestFactory.Deck(), TestFactory.Deck()));
        }
    }
}
