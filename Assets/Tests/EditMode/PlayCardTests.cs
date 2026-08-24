using System.Collections.Generic;
using System.Linq;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Playing a vanilla minion from hand: what it costs, what leaves the hand,
    /// and what arrives on the board.
    /// </summary>
    public sealed class PlayCardTests
    {
        private static Player Active(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.CurrentPlayer);

        [Test]
        public void A_minion_card_can_be_played()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            CommandResult result = TestFactory.PlayCard(engine, card.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.None));
            Assert.That(Active(engine).Board.Count, Is.EqualTo(1));
        }

        [Test]
        public void The_cost_is_taken_from_the_pool()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine, mana: 5);

            IReadOnlyList<GameEvent> events = TestFactory.PlayCard(engine, card.Id).Events;

            Assert.That(Active(engine).AvailableMana, Is.EqualTo(3), "A 2 mana card out of 5.");
            Assert.That(Active(engine).MaxMana, Is.EqualTo(5), "Crystals owned do not change.");

            ManaSpentEvent spent = events.OfType<ManaSpentEvent>().Single();
            Assert.That(spent.Amount, Is.EqualTo(2));
            Assert.That(spent.RemainingMana, Is.EqualTo(3));
        }

        [Test]
        public void The_cost_comes_from_the_instance_so_a_discount_is_honoured()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine, mana: 5);
            card.CostModifier = -2;

            TestFactory.PlayCard(engine, card.Id);

            Assert.That(Active(engine).AvailableMana, Is.EqualTo(5), "The discount made it free.");
        }

        [Test]
        public void A_free_card_reports_no_mana_spent()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine, mana: 5);
            card.CostModifier = -10;

            IReadOnlyList<GameEvent> events = TestFactory.PlayCard(engine, card.Id).Events;

            Assert.That(events.OfType<ManaSpentEvent>(), Is.Empty);
            Assert.That(Active(engine).AvailableMana, Is.EqualTo(5), "A cost cannot go below zero and refund mana.");
        }

        [Test]
        public void The_card_leaves_the_hand()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);
            int handBefore = Active(engine).Hand.Count;

            TestFactory.PlayCard(engine, card.Id);

            Player player = Active(engine);
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore - 1));
            Assert.That(player.Hand.Contains(card), Is.False);
            Assert.That(card.Zone, Is.EqualTo(ZoneType.Graveyard));
            Assert.That(player.Graveyard.Contains(card), Is.True);
        }

        [Test]
        public void A_runtime_minion_is_created_from_the_definition()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);
            PlayerId active = engine.State.CurrentPlayer;

            TestFactory.PlayCard(engine, card.Id);

            Minion minion = Active(engine).Board[0];

            Assert.That(minion.CardId, Is.EqualTo(card.CardId));
            Assert.That(minion.BaseAttack, Is.EqualTo(2));
            Assert.That(minion.BaseHealth, Is.EqualTo(3));
            Assert.That(minion.Attack, Is.EqualTo(2));
            Assert.That(minion.CurrentHealth, Is.EqualTo(3));
            Assert.That(minion.Owner, Is.EqualTo(active));
            Assert.That(minion.Controller, Is.EqualTo(active));
            Assert.That(minion.Zone, Is.EqualTo(ZoneType.Play));
            Assert.That(minion.IsInPlay, Is.True);
        }

        [Test]
        public void The_minion_is_a_different_entity_from_the_card()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = Active(engine).Board[0];

            Assert.That(minion.Id, Is.Not.EqualTo(card.Id), "A card and the minion it summons are two entities.");
            Assert.That(engine.State.GetEntity(minion.Id), Is.SameAs(minion));
            Assert.That(engine.State.GetEntity(card.Id), Is.SameAs(card));
        }

        [Test]
        public void The_minion_is_stamped_with_its_entry_into_play()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            Assert.That(card.Timestamp, Is.EqualTo(0), "A card in hand has not entered play.");

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = Active(engine).Board[0];

            Assert.That(minion.Timestamp, Is.GreaterThan(0));
            Assert.That(minion.Timestamp, Is.GreaterThan(engine.State.GetPlayer(PlayerId.One).Hero.Timestamp));
        }

        [Test]
        public void Minions_are_stamped_in_the_order_they_were_played()
        {
            GameEngine engine = TestFactory.StartedMatch();
            TestFactory.GiveMana(engine, engine.State.CurrentPlayer, 10);

            CardInstance first = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);
            CardInstance second = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            TestFactory.PlayCard(engine, first.Id, 0);
            TestFactory.PlayCard(engine, second.Id, 0);

            Zone<Minion> board = Active(engine).Board;

            // The newest sits on the left but is still the youngest.
            Assert.That(board[0].Timestamp, Is.GreaterThan(board[1].Timestamp));
        }

        [Test]
        public void The_minion_records_the_turn_it_arrived_on()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);
            int turn = engine.State.TurnNumber;

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = Active(engine).Board[0];

            Assert.That(minion.SummonedOnTurn, Is.EqualTo(turn));
        }

        [Test]
        public void A_played_minion_starts_with_no_attacks_used()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = Active(engine).Board[0];

            Assert.That(minion.AttacksThisTurn, Is.EqualTo(0));
            Assert.That(minion.MaxAttacksPerTurn, Is.EqualTo(1));
        }

        [Test]
        public void The_same_seed_and_the_same_play_reach_the_same_state()
        {
            List<string> Run()
            {
                GameEngine engine = TestFactory.StartedMatch(seed: 616UL);
                CardInstance card = TestFactory.ReadyToPlay(engine, mana: 4);
                List<GameEvent> all = new List<GameEvent>(TestFactory.PlayCard(engine, card.Id, 0).Events);
                all.AddRange(TestFactory.EndTurn(engine).Events);
                return all.Select(e => e.ToString()).ToList();
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
