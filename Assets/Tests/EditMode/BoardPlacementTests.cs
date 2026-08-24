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
    /// Where a minion lands is the player's choice and it is game state, not a
    /// display detail.
    /// </summary>
    public sealed class BoardPlacementTests
    {
        private static Player Active(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.CurrentPlayer);

        /// <summary>A board of three minions, left to right, with mana to spare.</summary>
        private static GameEngine BoardOfThree(ulong seed = 1UL)
        {
            GameEngine engine = TestFactory.StartedMatch(seed);
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            for (int index = 0; index < 3; index++)
            {
                TestFactory.PutMinionOnBoard(engine, active);
            }

            return engine;
        }

        [Test]
        public void A_minion_can_be_played_on_the_left()
        {
            GameEngine engine = BoardOfThree();
            Minion oldLeft = Active(engine).Board[0];
            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            TestFactory.PlayCard(engine, card.Id, 0);

            Zone<Minion> board = Active(engine).Board;
            Assert.That(board.Count, Is.EqualTo(4));
            Assert.That(board[0].Id, Is.Not.EqualTo(oldLeft.Id));
            Assert.That(board[1], Is.SameAs(oldLeft));
        }

        [Test]
        public void A_minion_can_be_played_in_the_middle()
        {
            GameEngine engine = BoardOfThree();
            Zone<Minion> before = Active(engine).Board;
            Minion first = before[0];
            Minion second = before[1];
            Minion third = before[2];

            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);
            TestFactory.PlayCard(engine, card.Id, 1);

            Zone<Minion> board = Active(engine).Board;
            Assert.That(board.Count, Is.EqualTo(4));
            Assert.That(board[0], Is.SameAs(first));
            Assert.That(board[2], Is.SameAs(second), "The minions to the right shifted along.");
            Assert.That(board[3], Is.SameAs(third));
        }

        [Test]
        public void A_minion_can_be_played_on_the_right_by_index()
        {
            GameEngine engine = BoardOfThree();
            Minion oldRight = Active(engine).Board[2];
            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            TestFactory.PlayCard(engine, card.Id, 3);

            Zone<Minion> board = Active(engine).Board;
            Assert.That(board[2], Is.SameAs(oldRight));
            Assert.That(board[3].CardId, Is.EqualTo(card.CardId));
        }

        [Test]
        public void The_rightmost_shorthand_appends()
        {
            GameEngine engine = BoardOfThree();
            Minion oldRight = Active(engine).Board[2];
            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            TestFactory.PlayCard(engine, card.Id, PlayCardCommand.Rightmost);

            Zone<Minion> board = Active(engine).Board;
            Assert.That(board.Count, Is.EqualTo(4));
            Assert.That(board[2], Is.SameAs(oldRight));
        }

        [Test]
        public void The_reported_slot_matches_where_it_landed()
        {
            GameEngine engine = BoardOfThree();
            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            MinionSummonedEvent summoned = TestFactory.PlayCard(engine, card.Id, 1)
                .Events.OfType<MinionSummonedEvent>().Single();

            Assert.That(summoned.BoardPosition, Is.EqualTo(1));
            Assert.That(Active(engine).Board[1].Id, Is.EqualTo(summoned.MinionId));
        }

        [Test]
        public void An_out_of_range_slot_is_refused()
        {
            GameEngine engine = BoardOfThree();
            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            CommandResult tooFar = TestFactory.PlayCard(engine, card.Id, 4);
            CommandResult negative = TestFactory.PlayCard(engine, card.Id, -2);

            Assert.That(tooFar.Reason, Is.EqualTo(RejectionReason.InvalidBoardPosition));
            Assert.That(negative.Reason, Is.EqualTo(RejectionReason.InvalidBoardPosition));
            Assert.That(Active(engine).Board.Count, Is.EqualTo(3));
        }

        [Test]
        public void The_slot_just_past_the_last_minion_is_valid()
        {
            GameEngine engine = BoardOfThree();
            CardInstance card = TestFactory.PutCardInHand(engine, engine.State.CurrentPlayer);

            CommandResult result = TestFactory.PlayCard(engine, card.Id, 3);

            Assert.That(result.IsAccepted, Is.True);
        }

        [Test]
        public void A_full_board_refuses_an_eighth_minion()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            for (int index = 0; index < 7; index++)
            {
                TestFactory.PutMinionOnBoard(engine, active);
            }

            CardInstance card = TestFactory.PutCardInHand(engine, active);
            int entitiesBefore = engine.State.EntityCount;

            CommandResult result = TestFactory.PlayCard(engine, card.Id);

            Assert.That(result.Reason, Is.EqualTo(RejectionReason.BoardFull));
            Assert.That(Active(engine).Board.Count, Is.EqualTo(7));
            Assert.That(Active(engine).Hand.Contains(card), Is.True, "The card stays in hand.");
            Assert.That(engine.State.EntityCount, Is.EqualTo(entitiesBefore), "No minion was created.");
        }

        [Test]
        public void Each_player_has_their_own_seven_slots()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            for (int index = 0; index < 7; index++)
            {
                TestFactory.PutMinionOnBoard(engine, active);
                TestFactory.PutMinionOnBoard(engine, active.Opponent);
            }

            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(7));
            Assert.That(engine.State.GetPlayer(active.Opponent).Board.Count, Is.EqualTo(7));
        }

        [Test]
        public void A_minion_dying_frees_its_slot_again()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);

            Minion doomed = null;
            for (int index = 0; index < 7; index++)
            {
                Minion summoned = TestFactory.PutMinionOnBoard(engine, active, health: 1);
                if (index == 3)
                {
                    doomed = summoned;
                }
            }

            TestFactory.Damage(engine, doomed.Id, 1);

            CardInstance card = TestFactory.PutCardInHand(engine, active);
            CommandResult result = TestFactory.PlayCard(engine, card.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(Active(engine).Board.Count, Is.EqualTo(7));
        }
    }
}
