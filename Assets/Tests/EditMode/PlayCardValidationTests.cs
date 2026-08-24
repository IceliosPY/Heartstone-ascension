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
    /// What the engine refuses, and the guarantee that a refusal costs nothing:
    /// no mana, no card moved, no entity created, no event.
    /// </summary>
    public sealed class PlayCardValidationTests
    {
        /// <summary>Everything a refused command must leave untouched.</summary>
        private sealed class Snapshot
        {
            private readonly int _entityCount;
            private readonly int _handCount;
            private readonly int _boardCount;
            private readonly int _availableMana;
            private readonly int _turnNumber;
            private readonly GameEngine _engine;
            private readonly PlayerId _player;

            public Snapshot(GameEngine engine, PlayerId player)
            {
                _engine = engine;
                _player = player;
                Player p = engine.State.GetPlayer(player);
                _entityCount = engine.State.EntityCount;
                _handCount = p.Hand.Count;
                _boardCount = p.Board.Count;
                _availableMana = p.AvailableMana;
                _turnNumber = engine.State.TurnNumber;
            }

            public void AssertUnchanged(CommandResult result, RejectionReason expected)
            {
                Player p = _engine.State.GetPlayer(_player);

                Assert.That(result.IsAccepted, Is.False);
                Assert.That(result.Reason, Is.EqualTo(expected));
                Assert.That(result.Events, Is.Empty, "A refused command reports nothing.");
                Assert.That(p.AvailableMana, Is.EqualTo(_availableMana), "No mana was spent.");
                Assert.That(p.Hand.Count, Is.EqualTo(_handCount), "No card moved.");
                Assert.That(p.Board.Count, Is.EqualTo(_boardCount), "Nothing reached the board.");
                Assert.That(_engine.State.EntityCount, Is.EqualTo(_entityCount), "No entity was created.");
                Assert.That(_engine.State.TurnNumber, Is.EqualTo(_turnNumber));
            }
        }

        [Test]
        public void Not_enough_mana_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 1);
            CardInstance card = TestFactory.PutCardInHand(engine, active);

            Snapshot before = new Snapshot(engine, active);

            before.AssertUnchanged(TestFactory.PlayCard(engine, card.Id), RejectionReason.NotEnoughMana);
        }

        [Test]
        public void Exactly_enough_mana_is_allowed()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 2);
            CardInstance card = TestFactory.PutCardInHand(engine, active);

            CommandResult result = TestFactory.PlayCard(engine, card.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.State.GetPlayer(active).AvailableMana, Is.EqualTo(0));
        }

        [Test]
        public void A_card_that_is_not_in_hand_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            EntityId cardInDeck = engine.State.GetPlayer(active).Deck[0].Id;
            Snapshot before = new Snapshot(engine, active);

            before.AssertUnchanged(TestFactory.PlayCard(engine, cardInDeck), RejectionReason.CardNotInHand);
        }

        [Test]
        public void An_unknown_entity_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            Snapshot before = new Snapshot(engine, active);

            before.AssertUnchanged(
                TestFactory.PlayCard(engine, new EntityId(99999)),
                RejectionReason.CardNotInHand);
        }

        [Test]
        public void An_opponents_card_cannot_be_played()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            CardInstance theirCard = TestFactory.PutCardInHand(engine, active.Opponent);
            Snapshot before = new Snapshot(engine, active);

            before.AssertUnchanged(TestFactory.PlayCard(engine, theirCard.Id), RejectionReason.CardNotInHand);
        }

        [Test]
        public void A_card_that_is_not_a_minion_is_refused_for_now()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            CardInstance spell = TestFactory.PutCardInHand(engine, active, TestFactory.SpellCardId);
            Snapshot before = new Snapshot(engine, active);

            before.AssertUnchanged(TestFactory.PlayCard(engine, spell.Id), RejectionReason.CardTypeNotPlayable);
        }

        [Test]
        public void The_idle_player_cannot_play_a_card()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId idle = engine.State.CurrentPlayer.Opponent;
            TestFactory.GiveMana(engine, idle, 10);
            CardInstance card = TestFactory.PutCardInHand(engine, idle);

            Snapshot before = new Snapshot(engine, idle);
            CommandResult result = engine.Execute(new PlayCardCommand(idle, card.Id));

            before.AssertUnchanged(result, RejectionReason.NotYourTurn);
        }

        [Test]
        public void A_card_cannot_be_played_during_the_mulligan()
        {
            GameEngine engine = TestFactory.MatchInMulligan();
            PlayerId seat = engine.State.StartingPlayer;
            CardInstance card = engine.State.GetPlayer(seat).Hand[0];

            CommandResult result = engine.Execute(new PlayCardCommand(seat, card.Id));

            Assert.That(result.Reason, Is.EqualTo(RejectionReason.WrongPhase));
            Assert.That(engine.State.GetPlayer(seat).Board.Count, Is.EqualTo(0));
        }

        [Test]
        public void A_card_cannot_be_played_once_the_match_is_over()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);
            CardInstance card = TestFactory.PutCardInHand(engine, active);

            TestFactory.Damage(engine, engine.State.GetPlayer(active).Hero.Id, 30);

            Snapshot before = new Snapshot(engine, active);
            CommandResult result = engine.Execute(new PlayCardCommand(active, card.Id));

            before.AssertUnchanged(result, RejectionReason.GameAlreadyEnded);
        }

        [Test]
        public void A_command_naming_no_player_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            CommandResult result = engine.Execute(new PlayCardCommand(PlayerId.None, card.Id));

            Assert.That(result.Reason, Is.EqualTo(RejectionReason.UnknownPlayer));
        }

        [Test]
        public void The_same_card_cannot_be_played_twice()
        {
            GameEngine engine = TestFactory.StartedMatch();
            CardInstance card = TestFactory.ReadyToPlay(engine);

            Assert.That(TestFactory.PlayCard(engine, card.Id).IsAccepted, Is.True);
            CommandResult again = TestFactory.PlayCard(engine, card.Id);

            Assert.That(again.Reason, Is.EqualTo(RejectionReason.CardNotInHand));
            Assert.That(engine.State.GetPlayer(engine.State.CurrentPlayer).Board.Count, Is.EqualTo(1));
        }

        [Test]
        public void Refusals_never_burn_an_entity_id()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 1);
            CardInstance card = TestFactory.PutCardInHand(engine, active);

            int before = engine.State.EntityCount;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                TestFactory.PlayCard(engine, card.Id);
            }

            Assert.That(engine.State.EntityCount, Is.EqualTo(before),
                "Five refused attempts must leave the id generator exactly where it was.");
        }
    }
}
