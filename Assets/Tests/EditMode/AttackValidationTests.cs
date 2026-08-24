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
    /// Who may attack, what they may attack, and the promise that a refused
    /// attack costs nothing at all.
    /// </summary>
    public sealed class AttackValidationTests
    {
        /// <summary>Everything a refused attack must leave untouched.</summary>
        private sealed class Snapshot
        {
            private readonly GameEngine _engine;
            private readonly int _entityCount;
            private readonly int _attackerAttacks;
            private readonly int _attackerDamage;
            private readonly int _friendlyBoard;
            private readonly int _enemyBoard;
            private readonly int _enemyHeroHealth;
            private readonly Minion _attacker;

            public Snapshot(GameEngine engine, Minion attacker)
            {
                _engine = engine;
                _attacker = attacker;
                _entityCount = engine.State.EntityCount;
                _attackerAttacks = attacker.AttacksThisTurn;
                _attackerDamage = attacker.Damage;
                _friendlyBoard = engine.State.GetPlayer(attacker.Controller).Board.Count;
                _enemyBoard = engine.State.GetPlayer(attacker.Controller.Opponent).Board.Count;
                _enemyHeroHealth = engine.State.GetPlayer(attacker.Controller.Opponent).Hero.CurrentHealth;
            }

            public void AssertUnchanged(CommandResult result, RejectionReason expected)
            {
                Assert.That(result.IsAccepted, Is.False);
                Assert.That(result.Reason, Is.EqualTo(expected));
                Assert.That(result.Events, Is.Empty, "A refused attack reports nothing.");
                Assert.That(_attacker.AttacksThisTurn, Is.EqualTo(_attackerAttacks), "No attack was spent.");
                Assert.That(_attacker.Damage, Is.EqualTo(_attackerDamage), "No damage was taken.");
                Assert.That(
                    _engine.State.GetPlayer(_attacker.Controller).Board.Count,
                    Is.EqualTo(_friendlyBoard));
                Assert.That(
                    _engine.State.GetPlayer(_attacker.Controller.Opponent).Board.Count,
                    Is.EqualTo(_enemyBoard),
                    "No death phase ran.");
                Assert.That(
                    _engine.State.GetPlayer(_attacker.Controller.Opponent).Hero.CurrentHealth,
                    Is.EqualTo(_enemyHeroHealth));
                Assert.That(_engine.State.EntityCount, Is.EqualTo(_entityCount), "No entity id was burned.");
            }
        }

        [Test]
        public void A_ready_minion_can_attack_an_enemy_minion()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            CommandResult result = TestFactory.Attack(engine, attacker.Id, defender.Id);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(engine.CanAttack(active, attacker.Id), Is.EqualTo(RejectionReason.AlreadyAttacked));
        }

        [Test]
        public void An_enemy_minion_cannot_be_used_as_the_attacker()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion theirs = TestFactory.PutMinionOnBoard(engine, active.Opponent, ready: true);
            Minion mine = TestFactory.PutMinionOnBoard(engine, active, ready: true);

            Snapshot before = new Snapshot(engine, theirs);
            before.AssertUnchanged(
                TestFactory.Attack(engine, theirs.Id, mine.Id),
                RejectionReason.InvalidAttacker);
        }

        [Test]
        public void Something_that_is_not_a_minion_on_the_board_cannot_attack()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            EntityId cardInHand = engine.State.GetPlayer(active).Hand[0].Id;
            EntityId ownHero = engine.State.GetPlayer(active).Hero.Id;

            Assert.That(
                TestFactory.Attack(engine, cardInHand, defender.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidAttacker));
            Assert.That(
                TestFactory.Attack(engine, ownHero, defender.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidAttacker),
                "Heroes attack with weapons, which do not exist yet.");
            Assert.That(
                TestFactory.Attack(engine, new EntityId(98765), defender.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidAttacker));
        }

        [Test]
        public void A_minion_already_removed_cannot_attack()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, health: 1, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            TestFactory.Damage(engine, attacker.Id, 1);

            Assert.That(attacker.IsInPlay, Is.False);
            Assert.That(
                TestFactory.Attack(engine, attacker.Id, defender.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidAttacker));
        }

        [Test]
        public void The_idle_player_cannot_attack()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId idle = engine.State.CurrentPlayer.Opponent;
            Minion theirs = TestFactory.PutMinionOnBoard(engine, idle, ready: true);
            Minion mine = TestFactory.PutMinionOnBoard(engine, idle.Opponent, ready: true);

            Snapshot before = new Snapshot(engine, theirs);
            before.AssertUnchanged(
                engine.Execute(new AttackCommand(idle, theirs.Id, mine.Id)),
                RejectionReason.NotYourTurn);
        }

        [Test]
        public void A_minion_with_no_attack_cannot_attack()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion harmless = TestFactory.PutMinionOnBoard(engine, active, attack: 0, health: 4, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            Snapshot before = new Snapshot(engine, harmless);
            before.AssertUnchanged(
                TestFactory.Attack(engine, harmless.Id, defender.Id),
                RejectionReason.ZeroAttack);

            Assert.That(engine.GetLegalAttackTargets(active, harmless.Id), Is.Empty);
        }

        [Test]
        public void A_minion_that_has_used_its_attack_is_refused()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, attack: 1, health: 9, ready: true);
            Minion first = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 9);
            Minion second = TestFactory.PutMinionOnBoard(engine, active.Opponent, attack: 0, health: 9);

            Assert.That(TestFactory.Attack(engine, attacker.Id, first.Id).IsAccepted, Is.True);

            Snapshot before = new Snapshot(engine, attacker);
            before.AssertUnchanged(
                TestFactory.Attack(engine, attacker.Id, second.Id),
                RejectionReason.AlreadyAttacked);
        }

        [Test]
        public void Friendly_characters_are_never_legal_targets()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            Minion friend = TestFactory.PutMinionOnBoard(engine, active);

            Snapshot before = new Snapshot(engine, attacker);
            before.AssertUnchanged(
                TestFactory.Attack(engine, attacker.Id, friend.Id),
                RejectionReason.InvalidTarget);

            Assert.That(
                TestFactory.Attack(engine, attacker.Id, engine.State.GetPlayer(active).Hero.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void Things_that_are_not_characters_are_never_legal_targets()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            Player enemy = engine.State.GetPlayer(active.Opponent);

            Assert.That(
                TestFactory.Attack(engine, attacker.Id, enemy.Hand[0].Id).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget),
                "A card in hand cannot be attacked.");
            Assert.That(
                TestFactory.Attack(engine, attacker.Id, enemy.Deck[0].Id).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget),
                "A card in a deck cannot be attacked.");
            Assert.That(
                TestFactory.Attack(engine, attacker.Id, new EntityId(98765)).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void A_dead_minion_is_no_longer_a_legal_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            Minion doomed = TestFactory.PutMinionOnBoard(engine, active.Opponent, health: 1);

            TestFactory.Damage(engine, doomed.Id, 1);

            Assert.That(
                TestFactory.Attack(engine, attacker.Id, doomed.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget));
            Assert.That(engine.GetLegalAttackTargets(active, attacker.Id).Contains(doomed.Id), Is.False);
        }

        [Test]
        public void Legal_targets_are_the_enemy_minions_then_the_enemy_hero()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            TestFactory.PutMinionOnBoard(engine, active);

            Minion left = TestFactory.PutMinionOnBoard(engine, active.Opponent);
            Minion right = TestFactory.PutMinionOnBoard(engine, active.Opponent);
            Hero enemyHero = engine.State.GetPlayer(active.Opponent).Hero;

            IReadOnlyList<EntityId> targets = engine.GetLegalAttackTargets(active, attacker.Id);

            Assert.That(targets, Is.EqualTo(new List<EntityId> { left.Id, right.Id, enemyHero.Id }),
                "Enemy minions left to right, then the enemy hero.");
        }

        [Test]
        public void Legal_targets_are_empty_when_the_minion_cannot_attack()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion sick = TestFactory.PutMinionOnBoard(engine, active);

            Assert.That(engine.GetLegalAttackTargets(active, sick.Id), Is.Empty);
            Assert.That(engine.GetLegalAttackTargets(active.Opponent, sick.Id), Is.Empty);
        }

        [Test]
        public void Attacking_is_refused_outside_the_playing_phase()
        {
            GameEngine mulligan = TestFactory.MatchInMulligan();
            Assert.That(
                mulligan.CanAttack(mulligan.State.StartingPlayer, new EntityId(1)),
                Is.EqualTo(RejectionReason.WrongPhase));

            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            TestFactory.Damage(engine, engine.State.GetPlayer(active).Hero.Id, 30);

            Snapshot before = new Snapshot(engine, attacker);
            before.AssertUnchanged(
                engine.Execute(new AttackCommand(active, attacker.Id, defender.Id)),
                RejectionReason.GameAlreadyEnded);
        }

        [Test]
        public void The_engine_answers_the_same_question_twice_the_same_way()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            Minion sick = TestFactory.PutMinionOnBoard(engine, active);
            Minion defender = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            AttackCommand command = new AttackCommand(active, sick.Id, defender.Id);

            // What the presentation would ask, and what Execute decides, must
            // be the same answer from the same code.
            Assert.That(engine.CanExecute(command), Is.EqualTo(RejectionReason.SummoningSickness));
            Assert.That(engine.Execute(command).Reason, Is.EqualTo(RejectionReason.SummoningSickness));
        }
    }
}
