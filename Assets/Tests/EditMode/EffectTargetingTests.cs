using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Rules.Effects;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Pointing at something when a card asks you to.
    ///
    /// The rule differs between a spell and a minion, and that difference is
    /// Hearthstone's rather than ours: a spell is only its effect, so with
    /// nothing legal to aim at there is nothing to buy; a minion is also a body,
    /// so it goes down and its battlecry finds nobody. Both halves are pinned
    /// down here, because a single blanket rule would have been simpler and
    /// wrong.
    /// </summary>
    public sealed class EffectTargetingTests
    {
        private static GameEngine Ready(out PlayerId active)
        {
            GameEngine engine = TestFactory.StartedMatch();
            active = engine.State.CurrentPlayer;
            TestFactory.GiveMana(engine, active, 10);
            return engine;
        }

        // ------------------------------------------------------------------
        //  Asking
        // ------------------------------------------------------------------

        [Test]
        public void A_plain_card_asks_for_nothing()
        {
            GameEngine engine = Ready(out PlayerId active);
            CardInstance card = TestFactory.PutCardInHand(engine, active);

            Assert.That(
                engine.GetPlayTargetRequirement(active, card.Id),
                Is.EqualTo(PlayTargetRequirement.None));

            Assert.That(engine.GetLegalPlayTargets(active, card.Id), Is.Empty);
        }

        [Test]
        public void A_targeted_spell_requires_a_target_and_a_targeted_minion_only_takes_one()
        {
            GameEngine engine = Ready(out PlayerId active);

            CardInstance minion = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");
            CardInstance spell = TestFactory.PutCardInHand(engine, active, "test_targeted_spell");

            Assert.That(
                engine.GetPlayTargetRequirement(active, minion.Id),
                Is.EqualTo(PlayTargetRequirement.Optional),
                "A minion is also a body, so it can go down without a target.");

            Assert.That(
                engine.GetPlayTargetRequirement(active, spell.Id),
                Is.EqualTo(PlayTargetRequirement.Required),
                "A spell is only its effect, so it needs somewhere to point.");
        }

        [Test]
        public void The_legal_targets_respect_the_filter_and_are_measured_from_the_controller()
        {
            GameEngine engine = Ready(out PlayerId active);
            PlayerId enemy = active.Opponent;

            Minion mine = TestFactory.PutMinionOnBoard(engine, active);
            Minion theirs = TestFactory.PutMinionOnBoard(engine, enemy);

            CardInstance sharpshooter = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");
            IReadOnlyList<EntityId> enemies = engine.GetLegalPlayTargets(active, sharpshooter.Id);

            Assert.That(enemies, Contains.Item(theirs.Id));
            Assert.That(enemies, Contains.Item(engine.State.GetPlayer(enemy).Hero.Id));
            Assert.That(enemies, Has.No.Member(mine.Id), "An enemy filter reached a friendly minion.");
            Assert.That(enemies, Has.No.Member(engine.State.GetPlayer(active).Hero.Id));

            CardInstance quartermaster = TestFactory.PutCardInHand(engine, active, "test_buff");
            IReadOnlyList<EntityId> friends = engine.GetLegalPlayTargets(active, quartermaster.Id);

            Assert.That(friends, Contains.Item(mine.Id));
            Assert.That(friends, Has.No.Member(theirs.Id));
            Assert.That(friends, Has.No.Member(engine.State.GetPlayer(active).Hero.Id),
                "A friendly minion filter reached a hero.");
        }

        // ------------------------------------------------------------------
        //  Accepting and refusing
        // ------------------------------------------------------------------

        [Test]
        public void A_legal_target_is_accepted()
        {
            GameEngine engine = Ready(out PlayerId active);
            Minion victim = TestFactory.PutMinionOnBoard(engine, active.Opponent);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");

            Assert.That(
                engine.CanExecute(new PlayCardCommand(active, card.Id, 0, victim.Id)),
                Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void An_illegal_target_is_refused_and_changes_nothing()
        {
            GameEngine engine = Ready(out PlayerId active);

            Minion friendly = TestFactory.PutMinionOnBoard(engine, active);
            TestFactory.PutMinionOnBoard(engine, active.Opponent);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");

            AssertRefused(engine, active, new PlayCardCommand(active, card.Id, 0, friendly.Id));
        }

        [Test]
        public void A_target_that_does_not_exist_is_refused()
        {
            GameEngine engine = Ready(out PlayerId active);
            TestFactory.PutMinionOnBoard(engine, active.Opponent);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");

            AssertRefused(engine, active, new PlayCardCommand(active, card.Id, 0, new EntityId(9999)));
        }

        [Test]
        public void A_missing_target_is_refused_when_something_could_have_been_pointed_at()
        {
            GameEngine engine = Ready(out PlayerId active);
            TestFactory.PutMinionOnBoard(engine, active.Opponent);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");

            // Hearthstone gives no option to decline when a target exists.
            AssertRefused(engine, active, new PlayCardCommand(active, card.Id));
        }

        [Test]
        public void A_card_that_asks_for_nothing_is_refused_a_target()
        {
            GameEngine engine = Ready(out PlayerId active);
            Minion minion = TestFactory.PutMinionOnBoard(engine, active);

            CardInstance plain = TestFactory.PutCardInHand(engine, active);

            AssertRefused(engine, active, new PlayCardCommand(active, plain.Id, 0, minion.Id));
        }

        // ------------------------------------------------------------------
        //  Nothing to aim at
        // ------------------------------------------------------------------

        /// <summary>
        /// The rule that could not be generalised. A spell with nowhere to point
        /// is unplayable; the minion beside it is not.
        /// </summary>
        [Test]
        public void With_no_legal_target_a_spell_is_unplayable_and_a_minion_is_not()
        {
            GameEngine engine = Ready(out PlayerId active);

            // Nothing friendly on the board, so a friendly minion filter finds
            // nobody. The quartermaster is not on the board yet either.
            CardInstance quartermaster = TestFactory.PutCardInHand(engine, active, "test_buff");
            CardInstance spell = TestFactory.PutCardInHand(engine, active, "test_targeted_spell");

            Assert.That(engine.GetLegalPlayTargets(active, quartermaster.Id), Is.Empty);

            Assert.That(
                engine.CanExecute(new PlayCardCommand(active, quartermaster.Id)),
                Is.EqualTo(RejectionReason.None),
                "A minion whose battlecry has nothing to aim at is still a body.");

            // And the spell, with the same empty board, cannot be cast at all.
            Assert.That(
                engine.CanExecute(new PlayCardCommand(active, spell.Id)),
                Is.EqualTo(RejectionReason.InvalidTarget),
                "A spell with nowhere to point cannot be cast.");
        }

        [Test]
        public void A_battlecry_with_nothing_to_aim_at_simply_does_not_happen()
        {
            GameEngine engine = Ready(out PlayerId active);

            CardInstance quartermaster = TestFactory.PutCardInHand(engine, active, "test_buff");
            CommandResult result = TestFactory.PlayCard(engine, quartermaster.Id);

            Assert.That(result.IsAccepted, Is.True);

            Player player = engine.State.GetPlayer(active);

            Assert.That(player.Board.Count, Is.EqualTo(1), "The body should still have arrived.");
            Assert.That(player.Board[0].IsModified, Is.False, "It buffed itself with a fizzled battlecry.");
            Assert.That(player.Board[0].Attack, Is.EqualTo(1));
        }

        /// <summary>
        /// A battlecry minion cannot target itself, and nothing had to be
        /// written to arrange that: the legal targets are worked out before the
        /// card is played, when the minion is not on the board yet.
        /// </summary>
        [Test]
        public void A_battlecry_minion_is_not_among_its_own_legal_targets()
        {
            GameEngine engine = Ready(out PlayerId active);
            Minion existing = TestFactory.PutMinionOnBoard(engine, active);

            CardInstance quartermaster = TestFactory.PutCardInHand(engine, active, "test_buff");
            IReadOnlyList<EntityId> legal = engine.GetLegalPlayTargets(active, quartermaster.Id);

            Assert.That(legal, Is.EqualTo(new[] { existing.Id }));
        }

        // ------------------------------------------------------------------
        //  What a chosen target actually reaches
        // ------------------------------------------------------------------

        /// <summary>
        /// A hero is a character, so a battlecry that wants a chosen enemy
        /// character may be aimed at one. Nothing about heroes is written into
        /// the action; the selector hands one over and the damage goes where the
        /// damage always goes.
        /// </summary>
        [Test]
        public void A_targeted_battlecry_can_be_aimed_at_the_enemy_hero()
        {
            GameEngine engine = Ready(out PlayerId active);
            Hero enemy = engine.State.GetPlayer(active.Opponent).Hero;

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_battlecry_damage");

            Assert.That(engine.GetLegalPlayTargets(active, card.Id), Has.Member(enemy.Id),
                "The enemy hero should be among the legal targets.");

            int before = enemy.CurrentHealth;
            CommandResult result = engine.Execute(new PlayCardCommand(active, card.Id, 0, enemy.Id));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(before - 2),
                "The battlecry did not reach the hero.");
            Assert.That(engine.State.GetPlayer(active).Board.Count, Is.EqualTo(1),
                "The minion should still have arrived.");
        }

        /// <summary>
        /// The friendly half of the same machinery: a buff aimed at a minion
        /// leaves the printed card alone and adds one modifier.
        /// </summary>
        [Test]
        public void A_targeted_buff_lands_on_the_friendly_minion_that_was_chosen()
        {
            GameEngine engine = Ready(out PlayerId active);

            Minion chosen = TestFactory.PutMinionOnBoard(engine, active);
            Minion other = TestFactory.PutMinionOnBoard(engine, active);

            CardInstance card = TestFactory.PutCardInHand(engine, active, "test_buff");
            CommandResult result = engine.Execute(new PlayCardCommand(active, card.Id, 0, chosen.Id));

            Assert.That(result.IsAccepted, Is.True);

            Assert.That(chosen.Modifiers.Count, Is.EqualTo(1), "No modifier was recorded.");
            Assert.That(chosen.BaseAttack, Is.EqualTo(2), "The printed attack was changed.");
            Assert.That(chosen.BaseHealth, Is.EqualTo(3), "The printed health was changed.");
            Assert.That(chosen.Attack, Is.EqualTo(3));
            Assert.That(chosen.MaxHealth, Is.EqualTo(4));

            Assert.That(other.Modifiers, Is.Empty,
                "The buff reached a minion nobody pointed at.");
        }

        /// <summary>
        /// A chosen target that is no longer there is simply not reached.
        ///
        /// Rare with a synchronous resolution, but the policy has to be written
        /// down somewhere, and it is this: the effect resolves on nothing. It
        /// does not crash, and it does not quietly pick somebody else.
        /// </summary>
        [Test]
        public void A_chosen_target_that_has_gone_is_reached_by_nobody()
        {
            GameEngine engine = Ready(out PlayerId active);
            Minion victim = TestFactory.PutMinionOnBoard(engine, active.Opponent);
            Hero enemyHero = engine.State.GetPlayer(active.Opponent).Hero;

            int heroBefore = enemyHero.CurrentHealth;

            EffectContext context = new EffectContext(
                sourceEntityId: victim.Id,
                sourceCardInstanceId: EntityId.None,
                sourceCardId: new CardId("test_battlecry_damage"),
                owner: active,
                controller: active,
                chosenTargetId: new EntityId(9999),
                sourceBoardPosition: 0);

            List<EntityId> found = new List<EntityId>();

            SelectorResolver.Resolve(
                engine.State,
                new SelectorDefinition(SelectorKind.ChosenTarget, TargetFilter.EnemyCharacter),
                context,
                found);

            // The selector hands the id over as it was given; the action is what
            // finds nothing behind it.
            Assert.That(found, Is.EqualTo(new[] { new EntityId(9999) }),
                "The selector invented a target of its own.");

            Assert.That(enemyHero.CurrentHealth, Is.EqualTo(heroBefore));
            Assert.That(victim.CurrentHealth, Is.EqualTo(victim.MaxHealth),
                "Something was damaged by an effect aimed at nobody.");
        }

        private static void AssertRefused(GameEngine engine, PlayerId active, PlayCardCommand command)
        {
            Player player = engine.State.GetPlayer(active);

            int handBefore = player.Hand.Count;
            int boardBefore = player.Board.Count;
            int manaBefore = player.AvailableMana;
            string stateBefore = StateFingerprint.Of(engine.State);

            CommandResult result = engine.Execute(command);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(RejectionReason.InvalidTarget));
            Assert.That(result.Events, Is.Empty);

            Assert.That(player.Hand.Count, Is.EqualTo(handBefore), "The card left the hand.");
            Assert.That(player.Board.Count, Is.EqualTo(boardBefore), "Something reached the board.");
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore), "Mana was spent.");
            Assert.That(StateFingerprint.Of(engine.State), Is.EqualTo(stateBefore),
                "A refused command changed the match.");
        }
    }
}
