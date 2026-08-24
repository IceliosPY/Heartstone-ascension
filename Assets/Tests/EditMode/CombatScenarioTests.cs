using System.Collections.Generic;
using System.Linq;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Whole matches played through the public command surface only: no state
    /// is poked, no internal action is used. If these pass, the engine can run
    /// a small but genuine game of Hearthstone.
    /// </summary>
    public sealed class CombatScenarioTests
    {
        private static Player PlayerOf(GameEngine engine, PlayerId seat) => engine.State.GetPlayer(seat);

        /// <summary>Plays the first card in the active player's hand, at the right end.</summary>
        private static CommandResult PlayFirstCard(GameEngine engine)
        {
            PlayerId active = engine.State.CurrentPlayer;
            CardInstance card = PlayerOf(engine, active).Hand[0];
            return engine.Execute(new PlayCardCommand(active, card.Id));
        }

        [Test]
        public void A_small_match_up_to_the_first_trade()
        {
            GameEngine engine = TestFactory.StartedMatch(seed: 2024UL);
            PlayerId first = engine.State.StartingPlayer;
            PlayerId second = first.Opponent;

            // Turn 1 and 2: one crystal each, and Test Soldier costs two.
            Assert.That(PlayFirstCard(engine).Reason, Is.EqualTo(RejectionReason.NotEnoughMana));
            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            // Turn 3: the starting player can afford a Test Soldier.
            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(first));
            Assert.That(PlayerOf(engine, first).AvailableMana, Is.EqualTo(2));
            Assert.That(PlayFirstCard(engine).IsAccepted, Is.True);

            Minion firstMinion = PlayerOf(engine, first).Board[0];
            Assert.That(firstMinion.IsSummoningSick(engine.State.TurnNumber), Is.True);
            Assert.That(
                engine.Execute(new AttackCommand(first, firstMinion.Id, PlayerOf(engine, second).Hero.Id)).Reason,
                Is.EqualTo(RejectionReason.SummoningSickness),
                "It cannot attack the turn it arrived.");

            TestFactory.EndTurn(engine);

            // Turn 4: the other player answers with a minion of their own.
            Assert.That(PlayFirstCard(engine).IsAccepted, Is.True);
            Minion secondMinion = PlayerOf(engine, second).Board[0];

            TestFactory.EndTurn(engine);

            // Turn 5: the first minion has been around a turn and can trade.
            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(first));
            Assert.That(firstMinion.IsSummoningSick(engine.State.TurnNumber), Is.False);

            CommandResult trade = engine.Execute(new AttackCommand(first, firstMinion.Id, secondMinion.Id));

            Assert.That(trade.IsAccepted, Is.True);

            // Two 2/3 Test Soldiers trading: both take two and both live.
            Assert.That(firstMinion.CurrentHealth, Is.EqualTo(1));
            Assert.That(secondMinion.CurrentHealth, Is.EqualTo(1));
            Assert.That(PlayerOf(engine, first).Board.Count, Is.EqualTo(1));
            Assert.That(PlayerOf(engine, second).Board.Count, Is.EqualTo(1));
            Assert.That(firstMinion.AttacksThisTurn, Is.EqualTo(1));
            Assert.That(trade.Events.OfType<MinionDiedEvent>(), Is.Empty);
        }

        [Test]
        public void A_second_trade_the_following_turn_kills_both()
        {
            GameEngine engine = TestFactory.StartedMatch(seed: 2024UL);
            PlayerId first = engine.State.StartingPlayer;
            PlayerId second = first.Opponent;

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);
            PlayFirstCard(engine);
            TestFactory.EndTurn(engine);
            PlayFirstCard(engine);
            TestFactory.EndTurn(engine);

            Minion mine = PlayerOf(engine, first).Board[0];
            Minion theirs = PlayerOf(engine, second).Board[0];
            engine.Execute(new AttackCommand(first, mine.Id, theirs.Id));

            // Both are on one health now. Come round again and the trade is
            // lethal for both.
            TestFactory.AdvanceToNextTurnOf(engine,first);

            Assert.That(mine.AttacksThisTurn, Is.EqualTo(0), "The counter was reset by the new turn.");

            IReadOnlyList<GameEvent> events =
                engine.Execute(new AttackCommand(first, mine.Id, theirs.Id)).Events;

            Assert.That(mine.IsInPlay, Is.False);
            Assert.That(theirs.IsInPlay, Is.False);
            Assert.That(events.OfType<MinionDiedEvent>().Count(), Is.EqualTo(2));
            Assert.That(PlayerOf(engine, first).Board.Count, Is.EqualTo(0));
            Assert.That(PlayerOf(engine, second).Board.Count, Is.EqualTo(0));
        }

        [Test]
        public void A_minion_beats_a_hero_down_over_several_turns()
        {
            // Six health, so three swings of a Test Soldier finish it.
            GameEngine engine = TestFactory.StartedMatch(
                seed: 909UL,
                config: new GameConfig(startingHeroHealth: 6));

            PlayerId attackerSeat = engine.State.StartingPlayer;
            PlayerId victimSeat = attackerSeat.Opponent;
            Hero victim = PlayerOf(engine, victimSeat).Hero;

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);
            Assert.That(PlayFirstCard(engine).IsAccepted, Is.True);
            Minion soldier = PlayerOf(engine, attackerSeat).Board[0];

            List<int> healthAfterEachSwing = new List<int>();

            for (int swing = 0; swing < 3; swing++)
            {
                TestFactory.AdvanceToNextTurnOf(engine,attackerSeat);
                if (engine.State.HasEnded)
                {
                    break;
                }

                CommandResult result = engine.Execute(new AttackCommand(attackerSeat, soldier.Id, victim.Id));
                Assert.That(result.IsAccepted, Is.True, "Swing " + swing);
                healthAfterEachSwing.Add(victim.CurrentHealth);
            }

            Assert.That(healthAfterEachSwing, Is.EqualTo(new List<int> { 4, 2, 0 }));
            Assert.That(victim.HasDied, Is.True);
            Assert.That(engine.State.HasEnded, Is.True);
            Assert.That(engine.State.Result, Is.EqualTo(
                attackerSeat == PlayerId.One ? GameResult.PlayerOneWins : GameResult.PlayerTwoWins));
            Assert.That(engine.State.CurrentPlayer.IsNone, Is.True);
        }

        [Test]
        public void The_match_stops_accepting_commands_once_it_is_won()
        {
            GameEngine engine = TestFactory.StartedMatch(config: new GameConfig(startingHeroHealth: 2));
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 2, health: 5, ready: true);

            IReadOnlyList<GameEvent> lethal =
                TestFactory.Attack(engine, attacker.Id, TestFactory.EnemyHero(engine).Id).Events;

            Assert.That(lethal.OfType<GameEndedEvent>().Count(), Is.EqualTo(1));

            GameResult resultBefore = engine.State.Result;
            int turnBefore = engine.State.TurnNumber;

            CommandResult again = engine.Execute(new AttackCommand(active, attacker.Id, new EntityId(1)));
            CommandResult endTurn = engine.Execute(new EndTurnCommand(active));

            Assert.That(again.Reason, Is.EqualTo(RejectionReason.GameAlreadyEnded));
            Assert.That(endTurn.Reason, Is.EqualTo(RejectionReason.GameAlreadyEnded));
            Assert.That(again.Events, Is.Empty);
            Assert.That(endTurn.Events, Is.Empty);
            Assert.That(engine.State.Result, Is.EqualTo(resultBefore));
            Assert.That(engine.State.TurnNumber, Is.EqualTo(turnBefore));
        }

        [Test]
        public void The_same_seed_and_the_same_commands_replay_a_whole_match_identically()
        {
            List<string> Run()
            {
                GameEngine engine = TestFactory.StartedMatch(seed: 31337UL);
                PlayerId first = engine.State.StartingPlayer;

                List<GameEvent> stream = new List<GameEvent>();
                stream.AddRange(TestFactory.EndTurn(engine).Events);
                stream.AddRange(TestFactory.EndTurn(engine).Events);
                stream.AddRange(PlayFirstCard(engine).Events);
                stream.AddRange(TestFactory.EndTurn(engine).Events);
                stream.AddRange(PlayFirstCard(engine).Events);
                stream.AddRange(TestFactory.EndTurn(engine).Events);

                Minion mine = PlayerOf(engine, first).Board[0];
                Minion theirs = PlayerOf(engine, first.Opponent).Board[0];
                stream.AddRange(engine.Execute(new AttackCommand(first, mine.Id, theirs.Id)).Events);
                stream.AddRange(TestFactory.EndTurn(engine).Events);

                return stream.Select(e => e.ToString()).ToList();
            }

            List<string> left = Run();
            List<string> right = Run();

            Assert.That(right, Is.EqualTo(left));
            Assert.That(left, Is.Not.Empty);
        }

        [Test]
        public void Summoning_sickness_wears_off_on_the_controllers_next_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, owner, 10);

            CardInstance card = TestFactory.PutCardInHand(engine, owner);
            engine.Execute(new PlayCardCommand(owner, card.Id));
            Minion soldier = PlayerOf(engine, owner).Board[0];
            Hero enemyHero = PlayerOf(engine, owner.Opponent).Hero;

            Assert.That(engine.CanAttack(owner, soldier.Id), Is.EqualTo(RejectionReason.SummoningSickness));
            Assert.That(engine.GetLegalAttackTargets(owner, soldier.Id), Is.Empty);

            TestFactory.EndTurn(engine);
            TestFactory.EndTurn(engine);

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(owner));
            Assert.That(engine.CanAttack(owner, soldier.Id), Is.EqualTo(RejectionReason.None));
            Assert.That(engine.GetLegalAttackTargets(owner, soldier.Id).Contains(enemyHero.Id), Is.True);
            Assert.That(engine.Execute(new AttackCommand(owner, soldier.Id, enemyHero.Id)).IsAccepted, Is.True);
        }
    }
}
