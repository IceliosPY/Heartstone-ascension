using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Turn order, mana progression, and who is allowed to end a turn.
    /// </summary>
    public sealed class TurnFlowTests
    {
        private static Player Active(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.CurrentPlayer);

        [Test]
        public void The_match_enters_the_playing_phase_after_the_mulligan()
        {
            GameEngine engine = TestFactory.StartedMatch();

            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(engine.State.HasEnded, Is.False);
        }

        [Test]
        public void The_starting_player_takes_the_first_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(engine.State.StartingPlayer));
            Assert.That(engine.State.TurnNumber, Is.EqualTo(1));
        }

        [Test]
        public void Turn_number_counts_the_whole_match_while_turns_taken_counts_the_player()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;

            Assert.That(engine.State.TurnNumber, Is.EqualTo(1));
            Assert.That(engine.State.GetPlayer(starting).TurnsTaken, Is.EqualTo(1));
            Assert.That(engine.State.GetPlayer(starting.Opponent).TurnsTaken, Is.EqualTo(0));

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            // Three turns have been played across the match, but each player
            // has only had two and one respectively.
            Assert.That(engine.State.TurnNumber, Is.EqualTo(3));
            Assert.That(engine.State.GetPlayer(starting).TurnsTaken, Is.EqualTo(2));
            Assert.That(engine.State.GetPlayer(starting.Opponent).TurnsTaken, Is.EqualTo(1));
        }

        [Test]
        public void The_first_turn_grants_exactly_one_crystal()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Player starting = engine.State.GetPlayer(engine.State.StartingPlayer);

            Assert.That(starting.MaxMana, Is.EqualTo(1));
            Assert.That(starting.AvailableMana, Is.EqualTo(1));
            Assert.That(engine.State.GetPlayer(engine.State.StartingPlayer.Opponent).MaxMana, Is.EqualTo(0));
        }

        [Test]
        public void Each_player_gains_a_crystal_on_their_own_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;

            TestFactory.EndTurn(engine);
            Assert.That(engine.State.GetPlayer(starting.Opponent).MaxMana, Is.EqualTo(1));
            Assert.That(engine.State.GetPlayer(starting).MaxMana, Is.EqualTo(1), "Unchanged while not their turn.");

            TestFactory.EndTurn(engine);
            Assert.That(engine.State.GetPlayer(starting).MaxMana, Is.EqualTo(2));
        }

        [Test]
        public void Mana_never_goes_past_ten()
        {
            GameEngine engine = TestFactory.StartedMatch();

            for (int turn = 0; turn < 24; turn++)
            {
                TestFactory.EndTurn(engine);
            }

            Assert.That(engine.State.GetPlayer(PlayerId.One).MaxMana, Is.EqualTo(10));
            Assert.That(engine.State.GetPlayer(PlayerId.Two).MaxMana, Is.EqualTo(10));
            Assert.That(engine.State.GetPlayer(PlayerId.One).AvailableMana, Is.LessThanOrEqualTo(10));
        }

        [Test]
        public void A_custom_cap_is_respected()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(maxManaCrystals: 3));

            for (int turn = 0; turn < 10; turn++)
            {
                TestFactory.EndTurn(engine);
            }

            Assert.That(engine.State.GetPlayer(PlayerId.One).MaxMana, Is.EqualTo(3));
        }

        [Test]
        public void Mana_is_refilled_at_the_start_of_a_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;

            // Spend everything, as a card would.
            engine.State.GetPlayer(starting).AvailableMana = 0;

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            Player refreshed = engine.State.GetPlayer(starting);
            Assert.That(refreshed.MaxMana, Is.EqualTo(2));
            Assert.That(refreshed.AvailableMana, Is.EqualTo(2));
        }

        [Test]
        public void Temporary_mana_does_not_survive_the_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            engine.State.GetPlayer(starting).TemporaryMana = 3;

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            Assert.That(engine.State.GetPlayer(starting).TemporaryMana, Is.EqualTo(0));
        }

        [Test]
        public void Ending_a_turn_hands_play_to_the_opponent()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;

            CommandResult result = engine.Execute(new EndTurnCommand(starting));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(starting.Opponent));
            Assert.That(engine.State.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void Turns_keep_alternating()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;

            for (int turn = 1; turn <= 6; turn++)
            {
                PlayerId expected = turn % 2 == 1 ? starting : starting.Opponent;
                Assert.That(engine.State.CurrentPlayer, Is.EqualTo(expected), "At match turn " + turn);
                TestFactory.EndTurn(engine);
            }
        }

        [Test]
        public void The_idle_player_cannot_end_the_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId idle = engine.State.StartingPlayer.Opponent;
            int turnBefore = engine.State.TurnNumber;

            CommandResult result = engine.Execute(new EndTurnCommand(idle));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.NotYourTurn));
            Assert.That(result.Events, Is.Empty);
            Assert.That(engine.State.TurnNumber, Is.EqualTo(turnBefore), "A refused command changes nothing.");
        }

        [Test]
        public void A_command_naming_no_player_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch();

            CommandResult result = engine.Execute(new EndTurnCommand(PlayerId.None));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.UnknownPlayer));
        }

        [Test]
        public void A_turn_cannot_be_ended_during_the_mulligan()
        {
            GameEngine engine = TestFactory.MatchInMulligan();

            CommandResult result = engine.Execute(new EndTurnCommand(engine.State.StartingPlayer));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.WrongPhase));
        }

        [Test]
        public void Per_turn_counters_are_reset()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId starting = engine.State.StartingPlayer;
            Player player = engine.State.GetPlayer(starting);

            player.HasUsedHeroPowerThisTurn = true;
            player.Hero.AttacksThisTurn = 1;

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            Assert.That(player.HasUsedHeroPowerThisTurn, Is.False);
            Assert.That(player.Hero.AttacksThisTurn, Is.EqualTo(0));
        }
    }
}
