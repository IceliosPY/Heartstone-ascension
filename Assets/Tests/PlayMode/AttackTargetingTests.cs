using System.Collections;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Grabbing a minion, aiming, letting go.
    ///
    /// Every legal target in here comes from the engine and is only painted.
    /// The tests check that what is highlighted is exactly what the engine
    /// listed, and that releasing anywhere else changes nothing at all.
    /// </summary>
    public sealed class AttackTargetingTests : InteractionTestBase
    {
        private TargetingArrow Arrow => Object.FindFirstObjectByType<TargetingArrow>();

        /// <summary>
        /// One minion each, and the first player's is old enough to swing.
        /// Returns with that player acting.
        ///
        /// Specifically Test Soldier for both sides, not merely "a minion":
        /// several tests fed by this drag an attack into a defender and then
        /// check that the attacker survived it (a two-for-three trade), an
        /// arithmetic tuned to Test Soldier's own 2/3 and not guaranteed for
        /// whatever else a hand might hold. Loops rather than assuming one
        /// attempt each side suffices, since a hand can go a turn or two
        /// holding nothing else playable first - a spell waiting for a
        /// target that does not exist yet, for one.
        /// </summary>
        private IEnumerator SetUpATrade()
        {
            yield return LoadMatch();

            for (int guard = 0; guard < 40; guard++)
            {
                PlayerId a = Session.State.CurrentPlayer;
                PlayerId b = a.Opponent;

                if (Session.State.GetPlayer(a).Board.Count >= 1 &&
                    Session.State.GetPlayer(b).Board.Count >= 1)
                {
                    yield break;
                }

                if (Session.State.GetPlayer(a).Board.Count < 1)
                {
                    CardView soldier = FindCardInHand("test_soldier");

                    if (soldier != null && soldier.IsPlayable)
                    {
                        Session.Submit(new PlayCardCommand(a, soldier.EntityId));
                        yield return Settle();
                    }
                }

                yield return EndTurn();
            }

            Assert.Fail("Both players never reached one Test Soldier each.");
        }

        [UnityTest]
        public IEnumerator A_ready_minion_starts_targeting_and_shows_the_engines_targets()
        {
            yield return SetUpATrade();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;
            MinionView attacker = FirstMinionOf(acting);

            Press(attacker.transform.position);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingAttack),
                "Grabbing a ready minion did not start aiming. The pointer landed on " + Input.LastHit + ".");

            MoveTo(NearBoardAt(2f));

            Assert.That(Arrow, Is.Not.Null, "The scene has no targeting arrow.");
            Assert.That(Arrow.IsVisible, Is.True, "No arrow is drawn while aiming.");

            // The highlights have to be the engine's list, exactly.
            IReadOnlyList<EntityId> expected =
                Session.GetLegalAttackTargets(acting, attacker.EntityId);

            Assert.That(Input.HighlightedTargets, Is.EquivalentTo(expected),
                "The highlighted targets are not the ones the engine listed.");

            Assert.That(expected, Contains.Item(Session.State.GetPlayer(waiting).Board[0].Id),
                "The enemy minion should be a legal target.");
            Assert.That(expected, Contains.Item(Session.State.GetPlayer(waiting).Hero.Id),
                "The enemy hero should be a legal target.");
            Assert.That(expected, Has.No.Member(Session.State.GetPlayer(acting).Hero.Id),
                "A player may not attack their own hero.");

            Release(EmptySpace);
            Assert.That(Arrow.IsVisible, Is.False, "The arrow survived the cancel.");
        }

        [UnityTest]
        public IEnumerator A_minion_summoned_this_turn_cannot_be_aimed()
        {
            yield return LoadMatch();
            yield return AdvanceUntilSomethingIsPlayable();

            PlayerId acting = Session.State.CurrentPlayer;
            yield return PlayOneMinionDirectly();

            MinionView minion = FirstMinionOf(acting);

            Assert.That(Session.CanAttack(acting, minion.EntityId),
                Is.EqualTo(RejectionReason.SummoningSickness),
                "This test needs a minion that was just summoned.");

            Press(minion.transform.position);

            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.TargetingAttack),
                "A minion summoned this turn started aiming.");
            Assert.That(Arrow.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator A_minion_that_has_attacked_waits_for_its_next_turn()
        {
            yield return SetUpATrade();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            MinionView attacker = FirstMinionOf(acting);
            MinionView defender = FirstMinionOf(waiting);
            EntityId attackerId = attacker.EntityId;

            Drag(attacker.transform.position, defender.transform.position);
            yield return Settle();

            Assert.That(Session.CanAttack(acting, attackerId), Is.EqualTo(RejectionReason.AlreadyAttacked));

            Assert.That(Presenter.TryGetMinionView(attackerId, out MinionView again), Is.True,
                "The attacker did not survive a two for three trade.");

            Press(again.transform.position);

            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.TargetingAttack),
                "A minion that already swung started aiming again.");

            // Its next turn gives it back.
            yield return RoundTrip();

            Assert.That(Session.CanAttack(acting, attackerId), Is.EqualTo(RejectionReason.None),
                "The minion did not get its attack back on its next turn.");

            Assert.That(Presenter.TryGetMinionView(attackerId, out MinionView refreshed), Is.True);
            Press(refreshed.transform.position);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingAttack),
                "A rested minion could not be aimed.");

            Release(EmptySpace);
        }

        [UnityTest]
        public IEnumerator Releasing_on_an_enemy_minion_attacks_it()
        {
            yield return SetUpATrade();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            MinionView attacker = FirstMinionOf(acting);
            MinionView defender = FirstMinionOf(waiting);

            int attackerHealth = Session.State.GetPlayer(acting).Board[0].CurrentHealth;
            int defenderHealth = Session.State.GetPlayer(waiting).Board[0].CurrentHealth;

            Drag(attacker.transform.position, defender.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(waiting).Board[0].CurrentHealth,
                Is.LessThan(defenderHealth), "The defender took no damage.");
            Assert.That(Session.State.GetPlayer(acting).Board[0].CurrentHealth,
                Is.LessThan(attackerHealth), "The attacker took no damage back.");

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False, "The arrow survived the attack.");
        }

        /// <summary>
        /// A hero is a target because the engine lists it, and for no other
        /// reason. Nothing in HeroView knows anything about attacking.
        /// </summary>
        [UnityTest]
        public IEnumerator Releasing_on_the_enemy_hero_attacks_it()
        {
            yield return SetUpATrade();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            MinionView attacker = FirstMinionOf(acting);
            HeroView enemyHero = HeroViewOf(waiting);

            int before = Session.State.GetPlayer(waiting).Hero.CurrentHealth;

            Drag(attacker.transform.position, enemyHero.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(waiting).Hero.CurrentHealth, Is.LessThan(before),
                "The enemy hero took no damage. The release landed on " + Input.LastHit + ".");
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Releasing_on_nothing_cancels_and_costs_the_attack_nothing()
        {
            yield return SetUpATrade();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            MinionView attacker = FirstMinionOf(acting);
            EntityId attackerId = attacker.EntityId;

            int enemyHealth = Session.State.GetPlayer(waiting).Hero.CurrentHealth;

            Drag(attacker.transform.position, EmptySpace);
            yield return Settle();

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False, "The arrow stayed after a cancel.");
            Assert.That(Input.HighlightedTargets, Is.Empty, "Highlights stayed after a cancel.");
            Assert.That(Session.State.GetPlayer(waiting).Hero.CurrentHealth, Is.EqualTo(enemyHealth),
                "Cancelling still hurt somebody.");

            // And the attack was not spent.
            Assert.That(Session.CanAttack(acting, attackerId), Is.EqualTo(RejectionReason.None),
                "Cancelling used up the attack.");
        }

        [UnityTest]
        public IEnumerator Releasing_on_a_friendly_character_cancels()
        {
            yield return SetUpATrade();

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId waiting = acting.Opponent;

            MinionView attacker = FirstMinionOf(acting);
            EntityId attackerId = attacker.EntityId;
            HeroView ownHero = HeroViewOf(acting);

            int ownHealth = Session.State.GetPlayer(acting).Hero.CurrentHealth;
            int enemyHealth = Session.State.GetPlayer(waiting).Hero.CurrentHealth;

            Drag(attacker.transform.position, ownHero.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(acting).Hero.CurrentHealth, Is.EqualTo(ownHealth),
                "A minion attacked its own hero.");
            Assert.That(Session.State.GetPlayer(waiting).Hero.CurrentHealth, Is.EqualTo(enemyHealth));
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False);
            Assert.That(Session.CanAttack(acting, attackerId), Is.EqualTo(RejectionReason.None),
                "Aiming at a friendly character used up the attack.");
        }
    }
}
