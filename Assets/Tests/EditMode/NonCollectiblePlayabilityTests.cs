using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// What "not collectible" actually forbids.
    ///
    /// It means one thing only: a player may not put the card in a deck. It has
    /// never meant that the card cannot exist, cannot sit in a hand, or cannot
    /// be played, and the difference matters the moment anything returns a
    /// summoned minion to its owner's hand - at which point it is an ordinary
    /// one-mana card that its owner can pay for and play.
    ///
    /// The temptation is a single line in the play validator reading
    /// <c>if (!definition.Collectible) return Illegal</c>. It would look
    /// reasonable, pass every test that existed before this one, and quietly
    /// make The Coin unplayable. These are the tests that stop it.
    /// </summary>
    public sealed class NonCollectiblePlayabilityTests
    {
        private static CardDefinition Definition(GameEngine engine, string cardId)
        {
            Assert.That(engine.State.Catalog.TryGet(new CardId(cardId), out CardDefinition found), Is.True);
            return found;
        }

        [Test]
        public void The_servants_are_all_non_collectible()
        {
            GameEngine engine = TestFactory.StartedMatch();

            for (int index = 0; index < TestFactory.ServantCardIds.Length; index++)
            {
                Assert.That(Definition(engine, TestFactory.ServantCardIds[index]).Collectible, Is.False);
            }
        }

        /// <summary>
        /// The other half of the contract, and the one worth guarding: a
        /// non-collectible card in a hand is a normal card.
        /// </summary>
        [Test]
        public void A_non_collectible_servant_can_be_played_from_hand_for_its_printed_cost()
        {
            foreach (string cardId in TestFactory.ServantCardIds)
            {
                GameEngine engine = TestFactory.StartedMatch();
                PlayerId active = engine.State.CurrentPlayer;
                Player player = engine.State.GetPlayer(active);

                CardDefinition definition = Definition(engine, cardId);
                Assert.That(definition.Collectible, Is.False);

                TestFactory.GiveMana(engine, active, definition.ManaCost);
                CardInstance card = TestFactory.PutCardInHand(engine, active, cardId);

                Assert.That(engine.CanPlayCard(active, card.Id), Is.EqualTo(RejectionReason.None),
                    cardId + " was refused from hand for being non-collectible.");

                CommandResult result = TestFactory.PlayCard(engine, card.Id);

                Assert.That(result.IsAccepted, Is.True, cardId + " could not be played from hand.");
                Assert.That(player.Board.Count, Is.EqualTo(1));
                Assert.That(player.Board[0].CardId.Value, Is.EqualTo(cardId));

                Assert.That(player.AvailableMana, Is.Zero,
                    "Playing it from hand costs its printed mana, unlike summoning it.");
            }
        }

        /// <summary>
        /// And it keeps everything the card printed. This is the model-level
        /// evidence that a returned servant would still be itself: nothing
        /// about being summoned rather than played changes what the minion is.
        /// </summary>
        [Test]
        public void A_servant_played_from_hand_is_identical_to_one_the_hero_power_summoned()
        {
            GameEngine summonedMatch = TestFactory.NecromancerMatch();
            TestFactory.UseHeroPower(summonedMatch, 0);

            Minion summoned = summonedMatch.State.GetPlayer(PlayerId.One).Board[0];

            GameEngine playedMatch = TestFactory.StartedMatch();
            PlayerId active = playedMatch.State.CurrentPlayer;

            TestFactory.GiveMana(playedMatch, active, 10);
            CardInstance card = TestFactory.PutCardInHand(
                playedMatch, active, TestFactory.SkeletalWarriorCardId);

            TestFactory.PlayCard(playedMatch, card.Id);

            Minion played = playedMatch.State.GetPlayer(active).Board[0];

            Assert.That(played.CardId, Is.EqualTo(summoned.CardId));
            Assert.That(played.Attack, Is.EqualTo(summoned.Attack));
            Assert.That(played.MaxHealth, Is.EqualTo(summoned.MaxHealth));
            Assert.That(played.Keywords, Is.EqualTo(summoned.Keywords));
        }

        /// <summary>
        /// A summoned minion still carries the id of the card it came from, so
        /// a bounce effect would have something real to put back in a hand.
        ///
        /// The project has no return-to-hand effect yet and this phase is not
        /// the place to build one. What can be checked now is the part that
        /// would make it work: the minion knows which card it is, and that card
        /// is in the catalog with its printed cost intact.
        /// </summary>
        [Test]
        public void A_summoned_servant_still_knows_which_card_it_came_from()
        {
            GameEngine engine = TestFactory.NecromancerMatch();

            TestFactory.UseHeroPower(engine, 1);

            Minion summoned = engine.State.GetPlayer(PlayerId.One).Board[0];

            Assert.That(summoned.CardId.Value, Is.EqualTo(TestFactory.SkeletalRogueCardId));

            Assert.That(engine.State.Catalog.TryGet(summoned.CardId, out CardDefinition definition), Is.True,
                "A summoned minion must resolve back to a real card definition.");

            Assert.That(definition.ManaCost, Is.EqualTo(1));
            Assert.That(definition.Attack, Is.EqualTo(0));
            Assert.That(definition.Health, Is.EqualTo(1));
            Assert.That(definition.Keywords, Is.EqualTo(CardKeywords.Stealth));
            Assert.That(definition.Collectible, Is.False);
        }

        /// <summary>
        /// The Coin proves the same rule from the other direction, and has done
        /// since long before the Necromancer existed.
        /// </summary>
        [Test]
        public void The_coin_is_non_collectible_and_still_playable()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Assert.That(Definition(engine, TestFactory.CoinCardId.Value).Collectible, Is.False);

            CardInstance coin = TestFactory.PutCardInHand(engine, active, TestFactory.CoinCardId.Value);

            Assert.That(engine.CanPlayCard(active, coin.Id), Is.EqualTo(RejectionReason.None));
        }
    }
}
