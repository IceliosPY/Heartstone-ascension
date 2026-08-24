using CoH.Core.Identifiers;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Runtime identifiers must be incremental and reproducible: a match
    /// replayed from the same seed and the same command log has to hand out
    /// exactly the same ids, or nothing referring to an entity would line up.
    /// </summary>
    public sealed class IdentifierTests
    {
        [Test]
        public void Generator_hands_out_consecutive_ids_starting_at_one()
        {
            EntityIdGenerator generator = new EntityIdGenerator();

            Assert.That(generator.Next().Value, Is.EqualTo(1));
            Assert.That(generator.Next().Value, Is.EqualTo(2));
            Assert.That(generator.Next().Value, Is.EqualTo(3));
            Assert.That(generator.IssuedCount, Is.EqualTo(3));
        }

        [Test]
        public void Two_generators_produce_the_same_sequence()
        {
            EntityIdGenerator left = new EntityIdGenerator();
            EntityIdGenerator right = new EntityIdGenerator();

            for (int step = 0; step < 50; step++)
            {
                Assert.That(right.Next(), Is.EqualTo(left.Next()), "Divergence at step " + step);
            }
        }

        [Test]
        public void Zero_is_reserved_for_none()
        {
            Assert.That(EntityId.None.Value, Is.EqualTo(0));
            Assert.That(EntityId.None.IsNone, Is.True);
            Assert.That(default(EntityId), Is.EqualTo(EntityId.None));
            Assert.That(new EntityIdGenerator().Next().IsNone, Is.False);
        }

        [Test]
        public void Default_player_id_is_none_rather_than_first_player()
        {
            Assert.That(default(PlayerId).IsNone, Is.True);
            Assert.That(PlayerId.One.IsNone, Is.False);
            Assert.That(PlayerId.One, Is.Not.EqualTo(PlayerId.Two));
        }

        [Test]
        public void Player_index_and_opponent_are_consistent()
        {
            Assert.That(PlayerId.One.Index, Is.EqualTo(0));
            Assert.That(PlayerId.Two.Index, Is.EqualTo(1));
            Assert.That(PlayerId.FromIndex(0), Is.EqualTo(PlayerId.One));
            Assert.That(PlayerId.FromIndex(1), Is.EqualTo(PlayerId.Two));
            Assert.That(PlayerId.One.Opponent, Is.EqualTo(PlayerId.Two));
            Assert.That(PlayerId.Two.Opponent, Is.EqualTo(PlayerId.One));
        }

        [Test]
        public void Card_ids_compare_by_ordinal_value()
        {
            Assert.That(new CardId("test_minion"), Is.EqualTo(new CardId("test_minion")));
            Assert.That(new CardId("test_minion"), Is.Not.EqualTo(new CardId("Test_Minion")));
            Assert.That(default(CardId).IsNone, Is.True);
            Assert.That(new CardId(string.Empty).IsNone, Is.True);
        }

        [Test]
        public void Game_state_assigns_incremental_ids_and_registers_entities()
        {
            GameState game = TestFactory.Game();

            // The two heroes are the first entities the match ever creates.
            Assert.That(game.EntityCount, Is.EqualTo(2));
            Assert.That(game.GetPlayer(PlayerId.One).Hero.Id.Value, Is.EqualTo(1));
            Assert.That(game.GetPlayer(PlayerId.Two).Hero.Id.Value, Is.EqualTo(2));

            CardInstance card = game.CreateCardInstance(new CardId(TestFactory.MinionCardId), PlayerId.One);
            Minion minion = game.CreateMinion(new CardId(TestFactory.MinionCardId), PlayerId.One);

            Assert.That(card.Id.Value, Is.EqualTo(3));
            Assert.That(minion.Id.Value, Is.EqualTo(4));
            Assert.That(game.EntityCount, Is.EqualTo(4));
            Assert.That(game.GetEntity(minion.Id), Is.SameAs(minion));
        }

        [Test]
        public void Two_game_states_with_the_same_seed_assign_the_same_ids()
        {
            GameState left = TestFactory.Game(seed: 12345UL);
            GameState right = TestFactory.Game(seed: 12345UL);

            CardId cardId = new CardId(TestFactory.MinionCardId);

            for (int step = 0; step < 10; step++)
            {
                Minion fromLeft = left.CreateMinion(cardId, PlayerId.One);
                Minion fromRight = right.CreateMinion(cardId, PlayerId.One);
                Assert.That(fromRight.Id, Is.EqualTo(fromLeft.Id), "Divergence at step " + step);
            }
        }

        [Test]
        public void Timestamps_are_monotonic_and_start_at_one()
        {
            GameState game = TestFactory.Game();

            Assert.That(game.NextTimestamp(), Is.EqualTo(1));
            Assert.That(game.NextTimestamp(), Is.EqualTo(2));
            Assert.That(game.NextTimestamp(), Is.EqualTo(3));
        }

        [Test]
        public void A_freshly_created_entity_has_no_timestamp_yet()
        {
            GameState game = TestFactory.Game();

            Minion minion = game.CreateMinion(new CardId(TestFactory.MinionCardId), PlayerId.One);

            // Zero means "has not entered play". Stamping happens when a rule
            // actually puts the minion on the board, which does not exist yet.
            Assert.That(minion.Timestamp, Is.EqualTo(0));
        }

        [Test]
        public void An_entity_starts_controlled_by_its_owner()
        {
            GameState game = TestFactory.Game();

            Minion minion = game.CreateMinion(new CardId(TestFactory.MinionCardId), PlayerId.Two);

            Assert.That(minion.Owner, Is.EqualTo(PlayerId.Two));
            Assert.That(minion.Controller, Is.EqualTo(PlayerId.Two));

            // Control can be stolen later; ownership never changes.
            minion.Controller = PlayerId.One;

            Assert.That(minion.Controller, Is.EqualTo(PlayerId.One));
            Assert.That(minion.Owner, Is.EqualTo(PlayerId.Two));
        }
    }
}
