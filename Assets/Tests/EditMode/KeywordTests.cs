using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Rush, Taunt and Stealth, as generic minion abilities.
    ///
    /// Every test here reaches the behaviour through a keyword rather than
    /// through a card: what is under test is that a minion carrying
    /// <see cref="CardKeywords.Rush"/> may hit minions and not the hero, not
    /// that Skeletal Warrior may. The Necromancer's servants are the first
    /// cards to use these, and the last thing the rules know about.
    ///
    /// The displayed names - Provocation for Taunt, Camouflage for Stealth -
    /// are presentation. The engine only ever says Taunt and Stealth, which is
    /// why nothing here mentions the other two.
    /// </summary>
    public sealed class KeywordTests
    {
        /// <summary>
        /// A minion with chosen keywords, on the board, ready to act.
        ///
        /// Built by putting a plain body down and giving it the keywords,
        /// because these tests are about the keywords and not about which card
        /// happens to print them.
        /// </summary>
        private static Minion Put(
            GameEngine engine,
            PlayerId controller,
            CardKeywords keywords,
            int attack = 2,
            int health = 3,
            bool ready = true)
        {
            Minion minion = TestFactory.PutMinionOnBoard(
                engine, controller, attack, health, ready: ready);

            minion.Keywords = keywords;
            return minion;
        }

        private static bool CanTarget(GameEngine engine, Minion attacker, EntityId target) =>
            Contains(engine.GetLegalAttackTargets(attacker.Controller, attacker.Id), target);

        private static bool Contains(IReadOnlyList<EntityId> ids, EntityId wanted)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                if (ids[index] == wanted)
                {
                    return true;
                }
            }

            return false;
        }

        // ==================================================================
        //  Rush
        // ==================================================================

        [Test]
        public void A_rushing_minion_can_attack_a_minion_the_turn_it_arrives()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion rusher = Put(engine, active, CardKeywords.Rush, ready: false);
            Minion victim = Put(engine, active.Opponent, CardKeywords.None, ready: true);

            Assert.That(rusher.IsSummoningSick(engine.State.TurnNumber), Is.True,
                "This test is only meaningful while the attacker really is newly summoned.");

            Assert.That(engine.CanAttack(active, rusher.Id), Is.EqualTo(RejectionReason.None));
            Assert.That(CanTarget(engine, rusher, victim.Id), Is.True);

            Assert.That(TestFactory.Attack(engine, rusher.Id, victim.Id).IsAccepted, Is.True);
        }

        [Test]
        public void A_rushing_minion_cannot_attack_the_hero_the_turn_it_arrives()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion rusher = Put(engine, active, CardKeywords.Rush, ready: false);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            Assert.That(CanTarget(engine, rusher, enemyHero.Id), Is.False,
                "Rush is not Charge. The hero must not be offered.");

            CommandResult result = TestFactory.Attack(engine, rusher.Id, enemyHero.Id);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.InvalidTarget));
            Assert.That(enemyHero.Damage, Is.Zero);
        }

        [Test]
        public void A_minion_without_rush_still_cannot_attack_at_all_the_turn_it_arrives()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion fresh = Put(engine, active, CardKeywords.None, ready: false);
            Put(engine, active.Opponent, CardKeywords.None, ready: true);

            Assert.That(engine.CanAttack(active, fresh.Id),
                Is.EqualTo(RejectionReason.SummoningSickness));
        }

        [Test]
        public void A_rushing_minion_can_go_to_the_face_on_a_later_turn()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion rusher = Put(engine, active, CardKeywords.Rush, ready: false);

            TestFactory.AdvanceToNextTurnOf(engine, active);

            Hero enemyHero = TestFactory.EnemyHero(engine);

            Assert.That(rusher.IsSummoningSick(engine.State.TurnNumber), Is.False);
            Assert.That(CanTarget(engine, rusher, enemyHero.Id), Is.True);
            Assert.That(TestFactory.Attack(engine, rusher.Id, enemyHero.Id).IsAccepted, Is.True);
        }

        [Test]
        public void Rush_grants_no_extra_attacks()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion rusher = Put(engine, active, CardKeywords.Rush, ready: false);
            Minion first = Put(engine, active.Opponent, CardKeywords.None, health: 20, ready: true);
            Minion second = Put(engine, active.Opponent, CardKeywords.None, health: 20, ready: true);

            Assert.That(TestFactory.Attack(engine, rusher.Id, first.Id).IsAccepted, Is.True);

            CommandResult again = TestFactory.Attack(engine, rusher.Id, second.Id);

            Assert.That(again.IsAccepted, Is.False);
            Assert.That(again.Reason, Is.EqualTo(RejectionReason.AlreadyAttacked));
        }

        [Test]
        public void A_rushing_minion_still_has_to_go_through_taunt()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion rusher = Put(engine, active, CardKeywords.Rush, ready: false);
            Minion plain = Put(engine, active.Opponent, CardKeywords.None, ready: true);
            Minion taunt = Put(engine, active.Opponent, CardKeywords.Taunt, ready: true);

            Assert.That(CanTarget(engine, rusher, taunt.Id), Is.True);
            Assert.That(CanTarget(engine, rusher, plain.Id), Is.False);
        }

        // ==================================================================
        //  Taunt / Provocation
        // ==================================================================

        [Test]
        public void A_taunt_minion_shields_the_hero_and_the_rest_of_the_board()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion attacker = Put(engine, active, CardKeywords.None);
            Minion plain = Put(engine, active.Opponent, CardKeywords.None, ready: true);
            Minion taunt = Put(engine, active.Opponent, CardKeywords.Taunt, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            Assert.That(CanTarget(engine, attacker, enemyHero.Id), Is.False);
            Assert.That(CanTarget(engine, attacker, plain.Id), Is.False);
            Assert.That(CanTarget(engine, attacker, taunt.Id), Is.True);

            Assert.That(TestFactory.Attack(engine, attacker.Id, enemyHero.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget));

            Assert.That(TestFactory.Attack(engine, attacker.Id, plain.Id).Reason,
                Is.EqualTo(RejectionReason.InvalidTarget));
        }

        [Test]
        public void Any_of_several_taunts_may_be_attacked()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion attacker = Put(engine, active, CardKeywords.None);
            Minion first = Put(engine, active.Opponent, CardKeywords.Taunt, ready: true);
            Minion second = Put(engine, active.Opponent, CardKeywords.Taunt, ready: true);

            Assert.That(CanTarget(engine, attacker, first.Id), Is.True);
            Assert.That(CanTarget(engine, attacker, second.Id), Is.True);
        }

        [Test]
        public void Ordinary_targets_come_back_once_the_last_taunt_is_gone()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion attacker = Put(engine, active, CardKeywords.None);
            Minion plain = Put(engine, active.Opponent, CardKeywords.None, ready: true);
            Minion taunt = Put(engine, active.Opponent, CardKeywords.Taunt, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            Assert.That(CanTarget(engine, attacker, enemyHero.Id), Is.False);

            TestFactory.Destroy(engine, taunt.Id);

            Assert.That(CanTarget(engine, attacker, enemyHero.Id), Is.True);
            Assert.That(CanTarget(engine, attacker, plain.Id), Is.True);
        }

        /// <summary>
        /// A taunt nobody can see does not compel anybody. Otherwise a hidden
        /// minion would lock the whole board out of attacking while being
        /// untargetable itself.
        /// </summary>
        [Test]
        public void A_stealthed_taunt_does_not_compel_attackers()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion attacker = Put(engine, active, CardKeywords.None);
            Put(engine, active.Opponent, CardKeywords.Taunt | CardKeywords.Stealth, ready: true);
            Hero enemyHero = TestFactory.EnemyHero(engine);

            Assert.That(CanTarget(engine, attacker, enemyHero.Id), Is.True);
        }

        // ==================================================================
        //  Stealth / Camouflage
        // ==================================================================

        [Test]
        public void A_stealthed_minion_cannot_be_attacked()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion attacker = Put(engine, active, CardKeywords.None);
            Minion hidden = Put(engine, active.Opponent, CardKeywords.Stealth, ready: true);

            Assert.That(CanTarget(engine, attacker, hidden.Id), Is.False);

            CommandResult result = TestFactory.Attack(engine, attacker.Id, hidden.Id);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.InvalidTarget));
            Assert.That(hidden.Damage, Is.Zero);
        }

        [Test]
        public void Stealth_survives_arriving_on_the_board_and_the_turn_passing()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion hidden = Put(engine, active, CardKeywords.Stealth, ready: false);

            Assert.That(hidden.HasKeyword(CardKeywords.Stealth), Is.True);

            TestFactory.AdvanceToNextTurnOf(engine, active);

            Assert.That(hidden.HasKeyword(CardKeywords.Stealth), Is.True,
                "Stealth is lost by attacking, not by time passing.");
        }

        [Test]
        public void A_stealthed_minion_cannot_be_chosen_by_a_hostile_targeted_effect()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion hidden = Put(engine, active.Opponent, CardKeywords.Stealth, ready: true);
            Minion visible = Put(engine, active.Opponent, CardKeywords.None, ready: true);

            TestFactory.GiveMana(engine, active, 10);
            CardInstance bolt = TestFactory.PutCardInHand(engine, active, "test_targeted_spell");

            IReadOnlyList<EntityId> legal = engine.GetLegalPlayTargets(active, bolt.Id);

            Assert.That(Contains(legal, hidden.Id), Is.False,
                "A hidden minion was offered as a target for an enemy spell.");

            Assert.That(Contains(legal, visible.Id), Is.True,
                "The minion beside it must still be targetable.");
        }

        /// <summary>
        /// Its owner is not the one being hidden from. A friendly buff must
        /// still be able to find it.
        /// </summary>
        [Test]
        public void The_owner_can_still_target_their_own_stealthed_minion()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion hidden = Put(engine, active, CardKeywords.Stealth, ready: true);

            TestFactory.GiveMana(engine, active, 10);
            CardInstance buff = TestFactory.PutCardInHand(engine, active, "test_buff");

            Assert.That(Contains(engine.GetLegalPlayTargets(active, buff.Id), hidden.Id), Is.True);
        }

        /// <summary>
        /// Camouflage is not immunity. An effect that never picked a target has
        /// nothing to be hidden from, and must still land.
        /// </summary>
        [Test]
        public void A_stealthed_minion_is_still_hit_by_an_effect_that_chooses_nothing()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion hidden = Put(engine, active.Opponent, CardKeywords.Stealth, health: 5, ready: true);

            TestFactory.GiveMana(engine, active, 10);
            CardInstance volley = TestFactory.PutCardInHand(engine, active, "test_aoe");

            Assert.That(TestFactory.PlayCard(engine, volley.Id).IsAccepted, Is.True);

            Assert.That(hidden.Damage, Is.GreaterThan(0),
                "Stealth stopped area damage. It hides a minion from being chosen, " +
                "not from being hit.");
        }

        [Test]
        public void Attacking_removes_stealth_and_the_minion_becomes_attackable()
        {
            GameEngine engine = TestFactory.StartedMatch();
            PlayerId active = engine.State.CurrentPlayer;

            Minion hidden = Put(engine, active, CardKeywords.Stealth, health: 20, ready: true);
            Minion victim = Put(engine, active.Opponent, CardKeywords.None,
                attack: 1, health: 20, ready: true);

            Assert.That(TestFactory.Attack(engine, hidden.Id, victim.Id).IsAccepted, Is.True);

            Assert.That(hidden.HasKeyword(CardKeywords.Stealth), Is.False,
                "Striking is what reveals a hidden minion.");

            // And the other side can now reach it.
            TestFactory.AdvanceToNextTurnOf(engine, active.Opponent);

            Assert.That(CanTarget(engine, victim, hidden.Id), Is.True);
        }
    }
}
