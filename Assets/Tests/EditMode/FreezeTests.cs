using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Frozen, the generic state that takes away a character's next attack
    /// opportunity - exercised directly through <see cref="TestFactory.Freeze"/>,
    /// independent of any card. Ice Barrage's own tests (<see cref="IceBarrageTests"/>)
    /// prove the card uses this mechanism; these prove the mechanism itself.
    /// </summary>
    public sealed class FreezeTests
    {
        [Test]
        public void Frozen_during_the_opponents_turn_blocks_the_owners_next_turn_then_thaws()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId owner = active.Opponent;
            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, ready: true);
            int turn = engine.State.TurnNumber;

            TestFactory.Freeze(engine, minion.Id);

            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn + 1),
                "Frozen on the opponent's turn must expire at the end of the owner's very next turn.");

            TestFactory.EndTurn(engine);

            Assert.That(engine.State.CurrentPlayer, Is.EqualTo(owner));
            Assert.That(CombatRules.ValidateAttacker(engine.State, owner, minion.Id, out _),
                Is.EqualTo(RejectionReason.Frozen), "The minion must not be able to attack during that turn.");

            TestFactory.EndTurn(engine);

            Assert.That(minion.IsFrozen, Is.False, "It must thaw at the end of that turn.");
        }

        [Test]
        public void Frozen_on_its_own_turn_before_attacking_forbids_the_attack_immediately_then_thaws()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, ready: true);
            int turn = engine.State.TurnNumber;

            TestFactory.Freeze(engine, minion.Id);

            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn),
                "A real, unspent attack opportunity this turn is the one lost, so it expires this turn.");
            Assert.That(CombatRules.ValidateAttacker(engine.State, owner, minion.Id, out _),
                Is.EqualTo(RejectionReason.Frozen), "The attack must be forbidden immediately.");

            TestFactory.EndTurn(engine);

            Assert.That(minion.IsFrozen, Is.False, "It must thaw at the end of the very turn it was frozen on.");
        }

        [Test]
        public void Frozen_on_its_own_turn_after_attacking_stays_frozen_through_the_owners_next_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);
            int turn = engine.State.TurnNumber;

            CommandResult attack = TestFactory.Attack(engine, minion.Id, enemyHero.Id);
            Assert.That(attack.IsAccepted, Is.True);
            Assert.That(minion.AttacksThisTurn, Is.GreaterThanOrEqualTo(minion.MaxAttacksPerTurn));

            TestFactory.Freeze(engine, minion.Id);

            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn + 2),
                "The attack was already spent, so this turn is not the one lost - the owner's next turn is.");

            TestFactory.EndTurn(engine); // opponent's turn
            Assert.That(minion.IsFrozen, Is.True, "It must not thaw during the intervening opponent's turn.");

            TestFactory.EndTurn(engine); // owner's next turn
            Assert.That(engine.State.TurnNumber, Is.EqualTo(turn + 2));
            Assert.That(CombatRules.ValidateAttacker(engine.State, owner, minion.Id, out _),
                Is.EqualTo(RejectionReason.Frozen), "It must lose the attack on the owner's next turn.");

            TestFactory.EndTurn(engine);
            Assert.That(minion.IsFrozen, Is.False, "It must thaw once that turn ends.");
        }

        [Test]
        public void A_summoning_sick_minion_frozen_on_its_own_turn_does_not_thaw_prematurely()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, ready: false);
            int turn = engine.State.TurnNumber;

            Assert.That(minion.IsSummoningSick(turn), Is.True);

            TestFactory.Freeze(engine, minion.Id);

            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn + 2),
                "Summoning sickness already blocked this turn's attack, so it was never available to lose.");

            TestFactory.EndTurn(engine); // opponent's turn
            Assert.That(minion.IsFrozen, Is.True, "Must not thaw prematurely.");

            TestFactory.EndTurn(engine); // owner's next turn - no longer sick
            Assert.That(minion.IsSummoningSick(engine.State.TurnNumber), Is.False);
            Assert.That(CombatRules.ValidateAttacker(engine.State, owner, minion.Id, out _),
                Is.EqualTo(RejectionReason.Frozen), "It must block this, its first real attack opportunity.");

            TestFactory.EndTurn(engine);
            Assert.That(minion.IsFrozen, Is.False);
        }

        [Test]
        public void Refreezing_can_extend_but_never_shorten()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, ready: true);
            int turn = engine.State.TurnNumber;

            // Case A: a real attack opportunity is available - expires this turn.
            TestFactory.Freeze(engine, minion.Id);
            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn));

            // Case B: no attack opportunity available - a later expiry. Extends.
            minion.AttacksThisTurn = minion.MaxAttacksPerTurn;
            TestFactory.Freeze(engine, minion.Id);
            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn + 2),
                "A freeze computing a later expiry must extend an earlier, shorter one.");

            // A hypothetical attack-opportunity reset would recompute an earlier
            // turn again; re-freezing must not adopt it.
            minion.AttacksThisTurn = 0;
            TestFactory.Freeze(engine, minion.Id);
            Assert.That(minion.FrozenUntilTurn, Is.EqualTo(turn + 2),
                "Re-freezing must never shorten an existing Freeze.");
        }

        [Test]
        public void Freeze_does_not_remove_other_keywords()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId owner = engine.State.CurrentPlayer;
            Minion minion = TestFactory.PutMinionOnBoard(engine, owner, TestFactory.SkeletalWarriorCardId, ready: true);

            Assert.That(minion.HasKeyword(CardKeywords.Rush), Is.True);

            TestFactory.Freeze(engine, minion.Id);

            Assert.That(minion.IsFrozen, Is.True);
            Assert.That(minion.HasKeyword(CardKeywords.Rush), Is.True,
                "Freeze must not touch a keyword it has nothing to do with.");
        }

        [Test]
        public void A_frozen_minion_remains_a_legal_attack_target()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;
            PlayerId owner = active.Opponent;

            Minion defender = TestFactory.PutMinionOnBoard(engine, owner, ready: true);
            Minion attacker = TestFactory.PutMinionOnBoard(engine, active, ready: true);

            TestFactory.Freeze(engine, defender.Id);

            Assert.That(CombatRules.ValidateTarget(engine.State, attacker, defender.Id), Is.EqualTo(RejectionReason.None),
                "Being Frozen changes what a character may do, not what may be done to it.");
        }

        [Test]
        public void Fingerprint_distinguishes_frozen_from_not_and_different_expirations()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Minion minion = TestFactory.PutMinionOnBoard(engine, engine.State.CurrentPlayer, ready: true);

            string beforeFreeze = StateFingerprint.Describe(engine.State);

            TestFactory.Freeze(engine, minion.Id);
            string frozen = StateFingerprint.Describe(engine.State);

            Assert.That(frozen, Is.Not.EqualTo(beforeFreeze), "Being Frozen must show up in the fingerprint.");

            minion.AttacksThisTurn = minion.MaxAttacksPerTurn;
            TestFactory.Freeze(engine, minion.Id);
            string extended = StateFingerprint.Describe(engine.State);

            Assert.That(extended, Is.Not.EqualTo(frozen),
                "A different FrozenUntilTurn must produce a different description.");
        }

        [Test]
        public void Freeze_is_deterministic_across_identical_sequences()
        {
            GameEngine engineA = TestFactory.StartedMatch();
            Minion minionA = TestFactory.PutMinionOnBoard(engineA, engineA.State.CurrentPlayer, ready: true);
            TestFactory.Freeze(engineA, minionA.Id);
            TestFactory.EndTurn(engineA);
            TestFactory.EndTurn(engineA);

            GameEngine engineB = TestFactory.StartedMatch();
            Minion minionB = TestFactory.PutMinionOnBoard(engineB, engineB.State.CurrentPlayer, ready: true);
            TestFactory.Freeze(engineB, minionB.Id);
            TestFactory.EndTurn(engineB);
            TestFactory.EndTurn(engineB);

            Assert.That(StateFingerprint.Of(engineA.State), Is.EqualTo(StateFingerprint.Of(engineB.State)));
        }

        [Test]
        public void A_hero_can_be_frozen_at_the_state_level_with_no_attack_system_needed()
        {
            GameEngine engine = TestFactory.StartedMatch();
            Hero hero = TestFactory.EnemyHero(engine);
            int turn = engine.State.TurnNumber;

            TestFactory.Freeze(engine, hero.Id);

            Assert.That(hero.IsFrozen, Is.True);
            Assert.That(hero.FrozenUntilTurn, Is.EqualTo(turn + 1));

            TestFactory.EndTurn(engine);
            Assert.That(hero.IsFrozen, Is.True, "Still frozen through the owner's turn until it ends.");

            TestFactory.EndTurn(engine);
            Assert.That(hero.IsFrozen, Is.False, "Thaws once that turn ends, exactly like a minion.");
        }
    }
}
