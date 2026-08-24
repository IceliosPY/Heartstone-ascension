using System.Collections.Generic;
using System.Linq;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Summoning sickness, as state only. Whether a sick minion may still act,
    /// which Charge and Rush change, belongs to the combat rules.
    ///
    /// Also covers the event order of playing a card, which is part of the
    /// engine's contract with the presentation layer.
    /// </summary>
    public sealed class SummoningSicknessTests
    {
        private static Player Active(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.CurrentPlayer);

        [Test]
        public void A_minion_played_this_turn_is_summoning_sick()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = Active(engine).Board[0];

            Assert.That(minion.IsSummoningSick(engine.State.TurnNumber), Is.True);
        }

        [Test]
        public void It_is_still_sick_during_the_opponents_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);
            PlayerId owner = engine.State.CurrentPlayer;

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = engine.State.GetPlayer(owner).Board[0];

            TestFactory.EndTurn(engine);

            // The turn number moved on, but it is not this minion's turn, so
            // the question does not arise yet. What matters is that by its own
            // next turn it is free.
            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(owner.Opponent));
            Assert.That(minion.SummonedOnTurn, Is.LessThan(engine.State.TurnNumber));
        }

        [Test]
        public void It_is_free_by_its_controllers_next_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);
            PlayerId owner = engine.State.CurrentPlayer;

            TestFactory.PlayCard(engine, card.Id);
            Minion minion = engine.State.GetPlayer(owner).Board[0];

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(owner), "Back to the minion's controller.");
            Assert.That(minion.IsSummoningSick(engine.State.TurnNumber), Is.False);
        }

        [Test]
        public void A_minion_summoned_on_a_later_turn_is_sick_again()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            CardInstance early = TestFactory.ReadyToPlay(engine);
            TestFactory.PlayCard(engine, early.Id);

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            TestFactory.GiveMana(engine, owner, 10);
            CardInstance late = TestFactory.PutCardInHand(engine, owner);
            TestFactory.PlayCard(engine, late.Id);

            // Both were appended to the right, so board order is play order.
            Zone<Minion> board = engine.State.GetPlayer(owner).Board;

            Assert.That(board[0].IsSummoningSick(engine.State.TurnNumber), Is.False, "Been around a turn.");
            Assert.That(board[1].IsSummoningSick(engine.State.TurnNumber), Is.True, "Just arrived.");
        }

        [Test]
        public void Playing_a_card_reports_mana_then_the_card_then_the_minion()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine, mana: 5);

            IReadOnlyList<GameEvent> events = TestFactory.PlayCard(engine, card.Id, 0).Events;

            Assert.That(events.Select(e => e.GetType().Name), Is.EqualTo(new List<string>
            {
                nameof(ManaSpentEvent),
                nameof(CardPlayedEvent),
                nameof(MinionSummonedEvent)
            }));
        }

        [Test]
        public void The_reported_card_and_minion_line_up_with_the_state()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine, mana: 5);

            IReadOnlyList<GameEvent> events = TestFactory.PlayCard(engine, card.Id, 0).Events;

            CardPlayedEvent played = events.OfType<CardPlayedEvent>().Single();
            MinionSummonedEvent summoned = events.OfType<MinionSummonedEvent>().Single();

            Assert.That(played.CardInstanceId, Is.EqualTo(card.Id));
            Assert.That(played.CardId, Is.EqualTo(card.CardId));
            Assert.That(played.TargetId.IsNone, Is.True, "A vanilla minion targets nothing.");
            Assert.That(summoned.MinionId, Is.EqualTo(Active(engine).Board[0].Id));
            Assert.That(summoned.CardId, Is.EqualTo(card.CardId));
        }

        [Test]
        public void Playing_a_card_runs_through_the_normal_pipeline()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            // A doomed minion is already waiting. Playing a card must run a
            // death phase like any other resolution, not take a private path.
            Minion doomed = TestFactory.PutMinionOnBoard(engine, active, health: 3);
            doomed.Damage = 3;

            CardInstance card = TestFactory.PutCardInHand(engine, active);
            IReadOnlyList<GameEvent> events = TestFactory.PlayCard(engine, card.Id).Events;

            Assert.That(events.OfType<MinionDiedEvent>().Count(), Is.EqualTo(1));
            Assert.That(doomed.IsInPlay, Is.False);
            Assert.That(Active(engine).Board.Count, Is.EqualTo(1), "Only the new minion is left.");
        }
    }
}
