using System.Collections.Generic;
using System.Linq;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The mulligan, including the detail that makes it fair: a card a player
    /// throws away must not be able to come straight back as its own
    /// replacement.
    /// </summary>
    public sealed class MulliganTests
    {
        private static List<int> HandIds(Player player) =>
            player.Hand.Select(card => card.Id.Value).ToList();

        private static Player Starting(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.StartingPlayer);

        private static Player Second(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.StartingPlayer.Opponent);

        /// <summary>Confirms both mulligans, replacing the listed cards for the starting player.</summary>
        private static void ConfirmBoth(GameEngine engine, params EntityId[] startingPlayerReplaces)
        {
            engine.Execute(new MulliganCommand(engine.State.StartingPlayer, startingPlayerReplaces));
            engine.Execute(new MulliganCommand(engine.State.StartingPlayer.Opponent));
        }

        [Test]
        public void Keeping_every_card_leaves_the_hand_untouched()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 5UL);
            List<int> before = HandIds(Starting(engine));

            ConfirmBoth(engine);

            // The turn draw has since added one card, so compare the opening
            // cards only: they must be the very same instances, in order.
            List<int> after = HandIds(Starting(engine));
            Assert.That(after.Take(before.Count), Is.EqualTo(before));
        }

        [Test]
        public void Replacing_one_card_gives_exactly_one_new_card()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 11UL);
            Player starting = Starting(engine);
            List<int> before = HandIds(starting);
            EntityId replaced = starting.Hand[0].Id;

            ConfirmBoth(engine, replaced);

            List<int> after = HandIds(starting);
            Assert.That(after.Contains(replaced.Value), Is.False, "The replaced card left the hand.");
            Assert.That(after.Count(id => !before.Contains(id)), Is.EqualTo(2),
                "One replacement plus the first turn draw.");
        }

        [Test]
        public void Replacing_several_cards_gives_the_same_number_back()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 12UL);
            Player starting = Starting(engine);
            List<int> before = HandIds(starting);
            EntityId[] replaced = { starting.Hand[0].Id, starting.Hand[2].Id };

            ConfirmBoth(engine, replaced);

            Assert.That(starting.Hand.Count, Is.EqualTo(4), "Three kept plus the turn draw.");
            foreach (EntityId id in replaced)
            {
                Assert.That(HandIds(starting).Contains(id.Value), Is.False);
            }

            Assert.That(before.Except(replaced.Select(id => id.Value)).All(HandIds(starting).Contains), Is.True,
                "Cards that were not replaced stay put.");
        }

        [Test]
        public void A_set_aside_card_cannot_be_its_own_replacement()
        {
            for (ulong seed = 1; seed <= 15; seed++)
            {
                GameEngine engine = TestFactory.MatchInMulligan(seed);
                Player starting = Starting(engine);
                List<int> before = HandIds(starting);

                // Throw the whole opening hand away.
                ConfirmBoth(engine, starting.Hand.Select(card => card.Id).ToArray());

                // The replacements are the first cards of the new hand; the
                // last one is the first turn draw, which happens after the
                // set-aside cards went back and the deck was reshuffled, and
                // may legitimately return one of them.
                List<int> replacements = HandIds(starting).Take(before.Count).ToList();
                Assert.That(before.Any(replacements.Contains), Is.False,
                    "Seed " + seed + ": a replaced card came back as its own replacement.");
            }
        }

        [Test]
        public void Replaced_cards_go_back_into_the_deck()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 21UL);
            Player starting = Starting(engine);
            List<int> replacedIds = starting.Hand.Select(card => card.Id.Value).ToList();

            ConfirmBoth(engine, starting.Hand.Select(card => card.Id).ToArray());

            foreach (int id in replacedIds)
            {
                bool inDeck = starting.Deck.Any(card => card.Id.Value == id);
                bool inHand = starting.Hand.Any(card => card.Id.Value == id);
                Assert.That(inDeck || inHand, Is.True, "Card " + id + " was lost.");
            }

            // Only the single turn draw can have pulled one back out again.
            Assert.That(
                starting.Deck.Count(card => replacedIds.Contains(card.Id.Value)),
                Is.GreaterThanOrEqualTo(replacedIds.Count - 1));

            Assert.That(starting.Deck.All(card => card.Zone == ZoneType.Deck), Is.True,
                "No card may be left marked as set aside.");
        }

        [Test]
        public void The_total_number_of_cards_never_changes()
        {
            GameEngine engine = TestFactory.MatchInMulligan(seed: 33UL, deckSize: 30);
            Player starting = Starting(engine);

            ConfirmBoth(engine, starting.Hand[0].Id, starting.Hand[1].Id);

            Assert.That(starting.Deck.Count + starting.Hand.Count + starting.Graveyard.Count, Is.EqualTo(30));

            Player second = Second(engine);
            int fromDeck = second.Deck.Count + second.Hand.Count + second.Graveyard.Count - 1; // minus the extra card
            Assert.That(fromDeck, Is.EqualTo(30));
        }

        [Test]
        public void The_same_seed_and_the_same_choices_give_the_same_result()
        {
            GameEngine left = TestFactory.MatchInMulligan(seed: 909UL);
            GameEngine right = TestFactory.MatchInMulligan(seed: 909UL);

            ConfirmBoth(left, Starting(left).Hand[0].Id, Starting(left).Hand[1].Id);
            ConfirmBoth(right, Starting(right).Hand[0].Id, Starting(right).Hand[1].Id);

            Assert.That(HandIds(Starting(right)), Is.EqualTo(HandIds(Starting(left))));
            Assert.That(
                Starting(right).Deck.Select(card => card.Id.Value),
                Is.EqualTo(Starting(left).Deck.Select(card => card.Id.Value)));
        }

        [Test]
        public void Resolution_does_not_depend_on_who_confirms_first()
        {
            GameEngine left = TestFactory.MatchInMulligan(seed: 55UL);
            GameEngine right = TestFactory.MatchInMulligan(seed: 55UL);

            EntityId leftPick = Starting(left).Hand[0].Id;
            EntityId rightPick = Starting(right).Hand[0].Id;

            // Seat one confirms first on the left, seat two on the right.
            left.Execute(new MulliganCommand(PlayerId.One,
                left.State.StartingPlayer == PlayerId.One ? new[] { leftPick } : new EntityId[0]));
            left.Execute(new MulliganCommand(PlayerId.Two,
                left.State.StartingPlayer == PlayerId.Two ? new[] { leftPick } : new EntityId[0]));

            right.Execute(new MulliganCommand(PlayerId.Two,
                right.State.StartingPlayer == PlayerId.Two ? new[] { rightPick } : new EntityId[0]));
            right.Execute(new MulliganCommand(PlayerId.One,
                right.State.StartingPlayer == PlayerId.One ? new[] { rightPick } : new EntityId[0]));

            Assert.That(HandIds(Starting(right)), Is.EqualTo(HandIds(Starting(left))));
            Assert.That(HandIds(Second(right)), Is.EqualTo(HandIds(Second(left))));
        }

        [Test]
        public void Nothing_resolves_until_both_players_have_confirmed()
        {
            GameEngine engine = TestFactory.MatchInMulligan();

            engine.Execute(new MulliganCommand(engine.State.StartingPlayer));

            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Mulligan));
            Assert.That(engine.State.TurnNumber, Is.EqualTo(0));
            Assert.That(Second(engine).Hand.Count, Is.EqualTo(4), "Still no extra card.");
        }

        [Test]
        public void A_player_cannot_confirm_twice()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            PlayerId starting = engine.State.StartingPlayer;

            engine.Execute(new MulliganCommand(starting));
            CommandResult second = engine.Execute(new MulliganCommand(starting));

            Assert.That(second.IsAccepted, Is.False);
            Assert.That(second.Reason, Is.EqualTo(RejectionReason.AlreadyConfirmedMulligan));
        }

        [Test]
        public void A_card_that_is_not_in_hand_cannot_be_replaced()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            Player starting = Starting(engine);
            EntityId cardInDeck = starting.Deck[0].Id;

            CommandResult result = engine.Execute(new MulliganCommand(starting.Id, cardInDeck));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.InvalidMulliganSelection));
            Assert.That(starting.HasConfirmedMulligan, Is.False, "A refused command changes nothing.");
        }

        [Test]
        public void The_same_card_cannot_be_listed_twice()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            Player starting = Starting(engine);
            EntityId card = starting.Hand[0].Id;

            CommandResult result = engine.Execute(new MulliganCommand(starting.Id, card, card));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.InvalidMulliganSelection));
        }

        [Test]
        public void Mulligan_is_refused_once_the_match_is_being_played()
        {
            GameEngine engine = TestFactory.StartedMatch();

            CommandResult result = engine.Execute(new MulliganCommand(PlayerId.One));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.WrongPhase));
        }
    }
}
