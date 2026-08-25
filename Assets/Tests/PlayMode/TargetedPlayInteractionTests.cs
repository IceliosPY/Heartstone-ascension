using System.Collections;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Aiming a card with the mouse, through every gesture a player can use to
    /// do it.
    ///
    /// Hearthstone lets you play a card two ways, and so does this: drag it onto
    /// the board and let go, or click it up, carry it and click it down. Both
    /// arrive at the same question, and everything after that question is one
    /// path. Which is exactly why both have to be driven here. One of them was
    /// broken while the other worked, and a suite that only ever pressed the
    /// mouse button without releasing it could not tell.
    ///
    /// So every click in this file is a whole click.
    /// </summary>
    public sealed class TargetedPlayInteractionTests : InteractionTestBase
    {
        private TargetingArrow Arrow => Object.FindFirstObjectByType<TargetingArrow>();

        private MinionView EnemyMinion()
        {
            PlayerId enemy = Session.State.CurrentPlayer.Opponent;
            Player player = Session.State.GetPlayer(enemy);

            Assert.That(player.Board.Count, Is.GreaterThan(0), "The enemy has no minion to aim at.");
            Assert.That(Presenter.TryGetMinionView(player.Board[0].Id, out MinionView view), Is.True);
            return view;
        }

        /// <summary>Everything the engine says is aimable, for comparison.</summary>
        private IReadOnlyList<EntityId> LegalFor(CardView card) =>
            Session.GetLegalPlayTargets(Session.State.CurrentPlayer, card.EntityId);

        private static Minion MinionOf(GameState state, EntityId id)
        {
            state.TryGetEntity(id, out Entity entity);
            return entity as Minion;
        }

        // ------------------------------------------------------------------
        //  Reaching the question, by either gesture
        // ------------------------------------------------------------------

        /// <summary>
        /// The regression this file exists for.
        ///
        /// Click the card up, carry it over the board, click it down. The click
        /// that puts the card down asks the question; the release of that same
        /// click must not answer it. It used to. The distance travelled while
        /// the card was being carried was still on the clock when the arrow
        /// appeared, so the release read as a deliberate answer, found nothing
        /// under the pointer but an empty slot, and cancelled the whole play.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_a_targeted_card_down_asks_for_a_target_and_the_same_click_does_not_answer()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);

            Assert.That(card, Is.Not.Null);

            IReadOnlyList<EntityId> legal = LegalFor(card);
            Assert.That(legal, Is.Not.Empty);

            int handBefore = Session.State.GetPlayer(acting).Hand.Count;
            int manaBefore = Session.State.GetPlayer(acting).AvailableMana;

            Click(card.transform.position);
            Assert.That(Input.State, Is.EqualTo(InteractionState.DraggingHandCard),
                "Clicking a card should pick it up.");

            CarryTo(NearBoardRight);
            yield return null;

            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay),
                "The card was put down and the question was dropped with it.");
            Assert.That(Arrow.IsVisible, Is.True, "No arrow after the card was put down.");
            Assert.That(Input.HighlightedTargets, Is.EquivalentTo(legal),
                "The highlighted targets are not the engine's list.");

            yield return Settle();

            // Still waiting, and still having changed nothing at all.
            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay),
                "The question was withdrawn before it could be answered.");

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.Zero, "The minion arrived without being aimed.");
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore));
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore));
        }

        /// <summary>The other gesture reaches the same question.</summary>
        [UnityTest]
        public IEnumerator Dragging_a_targeted_card_onto_the_board_asks_the_same_question()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            IReadOnlyList<EntityId> legal = LegalFor(card);

            Press(card.transform.position);
            CarryTo(NearBoardRight);
            Release(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));
            Assert.That(Arrow.IsVisible, Is.True);
            Assert.That(Input.HighlightedTargets, Is.EquivalentTo(legal));

            yield return null;
        }

        // ------------------------------------------------------------------
        //  Answering it
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator A_battlecry_clicked_onto_an_enemy_minion_deals_its_damage()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId enemy = acting.Opponent;

            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            MinionView victim = EnemyMinion();
            EntityId victimId = victim.EntityId;

            int before = Session.State.GetPlayer(enemy).Board[0].CurrentHealth;

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));

            CarryTo(victim.transform.position);
            Click(victim.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1),
                "The minion never arrived.");

            Minion hit = MinionOf(Session.State, victimId);

            Assert.That(hit, Is.Not.Null, "The minion that was pointed at is gone.");
            Assert.That(hit.CurrentHealth, Is.EqualTo(before - 2),
                "The battlecry did not land on the minion that was pointed at.");

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False, "The arrow outlived the play.");
            Assert.That(Input.HighlightedTargets, Is.Empty);
        }

        /// <summary>
        /// The selector asks for a chosen enemy character, and a hero is a
        /// character. Nothing in the view knows that; it points at the list it
        /// was handed.
        /// </summary>
        [UnityTest]
        public IEnumerator A_battlecry_clicked_onto_the_enemy_hero_deals_its_damage()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId enemy = acting.Opponent;

            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            HeroView hero = HeroViewOf(enemy);

            Assert.That(LegalFor(card), Has.Member(hero.EntityId),
                "The enemy hero should be aimable by a chosen enemy character.");

            int before = Session.State.GetPlayer(enemy).Hero.CurrentHealth;

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            CarryTo(hero.transform.position);
            Click(hero.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(enemy).Hero.CurrentHealth, Is.EqualTo(before - 2),
                "The battlecry did not reach the hero.");
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1));
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
        }

        [UnityTest]
        public IEnumerator A_buff_clicked_onto_a_friendly_minion_changes_only_what_it_should()
        {
            yield return LoadWithScenario(DebugScenarios.BuffId);

            PlayerId acting = Session.State.CurrentPlayer;
            EntityId targetId = Session.State.GetPlayer(acting).Board[0].Id;

            Assert.That(Presenter.TryGetMinionView(targetId, out MinionView target), Is.True);

            CardView card = FindCardInHand(DebugScenarios.TestBuff);

            Assert.That(card, Is.Not.Null);
            Assert.That(LegalFor(card), Has.Member(targetId));

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay),
                "A friendly target is still a target, and still has to be asked for.");

            CarryTo(target.transform.position);
            Click(target.transform.position);
            yield return Settle();

            Minion buffed = MinionOf(Session.State, targetId);

            Assert.That(buffed, Is.Not.Null, "The buffed minion is gone.");
            Assert.That(buffed.BaseAttack, Is.EqualTo(2), "The printed attack was changed.");
            Assert.That(buffed.BaseHealth, Is.EqualTo(3), "The printed health was changed.");
            Assert.That(buffed.Modifiers.Count, Is.EqualTo(1), "No modifier was recorded.");
            Assert.That(buffed.Attack, Is.EqualTo(3), "The effective attack is wrong.");
            Assert.That(buffed.MaxHealth, Is.EqualTo(4), "The effective health is wrong.");
        }

        // ------------------------------------------------------------------
        //  Declining to answer
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Clicking_somewhere_illegal_cancels_and_spends_nothing()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            EntityId id = card.EntityId;

            int handBefore = Session.State.GetPlayer(acting).Hand.Count;
            int manaBefore = Session.State.GetPlayer(acting).AvailableMana;

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));

            CarryTo(EmptySpace);
            Click(EmptySpace);
            yield return Settle();

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False, "The arrow stayed after a cancel.");
            Assert.That(Input.HighlightedTargets, Is.Empty, "The highlights stayed after a cancel.");

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.Zero, "The card was played anyway.");
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore), "The card left the hand.");
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore), "Mana was spent on a cancel.");
            Assert.That(Presenter.TryGetCardView(id, out CardView _), Is.True,
                "The card lost its view and could not be played again.");
        }

        /// <summary>
        /// A character of the wrong kind is not a target, and clicking one
        /// cancels rather than playing the card at it.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_a_character_the_filter_forbids_cancels()
        {
            yield return LoadWithScenario(DebugScenarios.BuffId);

            PlayerId acting = Session.State.CurrentPlayer;
            int manaBefore = Session.State.GetPlayer(acting).AvailableMana;

            CardView card = FindCardInHand(DebugScenarios.TestBuff);
            Assert.That(card, Is.Not.Null);

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));

            // The enemy hero is a character, but the buff wants a friendly
            // minion, so the engine never offered it.
            HeroView enemyHero = HeroViewOf(acting.Opponent);
            Assert.That(Input.HighlightedTargets, Has.No.Member(enemyHero.EntityId));

            CarryTo(enemyHero.transform.position);
            Click(enemyHero.transform.position);
            yield return Settle();

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1),
                "Something was played at a target the filter forbids.");
            Assert.That(Session.State.GetPlayer(acting).AvailableMana, Is.EqualTo(manaBefore));
            Assert.That(Session.State.GetPlayer(acting.Opponent).Hero.CurrentHealth, Is.EqualTo(30));
        }

        // ------------------------------------------------------------------
        //  Nothing to aim at
        // ------------------------------------------------------------------

        /// <summary>
        /// A targeted battlecry with nowhere to point is played anyway, and no
        /// question is asked.
        ///
        /// This is the half of the rule that differs from a spell, and it has to
        /// hold through the mouse as much as through the engine: no arrow, no
        /// waiting state, no card stuck to the pointer. The buff wants a
        /// friendly minion, and the second seat's board is empty.
        /// </summary>
        [UnityTest]
        public IEnumerator A_targeted_battlecry_with_nothing_to_aim_at_is_played_without_asking()
        {
            yield return LoadWithScenario(DebugScenarios.BuffId);
            yield return EndTurn();

            PlayerId acting = Session.State.CurrentPlayer;

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.Zero,
                "This seat was supposed to have an empty board.");

            CardView card = FindCardInHand(DebugScenarios.TestBuff);

            Assert.That(card, Is.Not.Null, "This seat is not holding the buff.");
            Assert.That(Session.GetPlayTargetRequirement(acting, card.EntityId),
                Is.EqualTo(PlayTargetRequirement.Optional));
            Assert.That(Session.GetLegalPlayTargets(acting, card.EntityId), Is.Empty,
                "There should be nothing to aim the buff at.");
            Assert.That(card.IsPlayable, Is.True,
                "A minion whose battlecry has nowhere to point is still a body.");

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);
            yield return Settle();

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle),
                "A question was asked when there was nothing to ask about.");
            Assert.That(Arrow.IsVisible, Is.False, "An arrow was drawn with nothing to point at.");

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.EqualTo(1), "The minion was not played.");
            Assert.That(player.Board[0].Modifiers.Count, Is.Zero,
                "The battlecry found somebody after all, and it buffed itself.");
            Assert.That(Presenter.TryGetMinionView(player.Board[0].Id, out MinionView _), Is.True);
        }

        // ------------------------------------------------------------------
        //  Hotseat
        // ------------------------------------------------------------------

        /// <summary>
        /// The same code, from the other seat. The player at the bottom of the
        /// screen is the player holding the turn, and the effect belongs to
        /// them.
        /// </summary>
        [UnityTest]
        public IEnumerator Both_seats_can_aim_a_battlecry_with_the_same_code()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId first = Session.State.CurrentPlayer;
            yield return AimTheBattlecryAtTheEnemyHero(first);

            yield return EndTurn();

            PlayerId second = Session.State.CurrentPlayer;

            Assert.That(second, Is.Not.EqualTo(first), "The turn did not change hands.");
            Assert.That(Presenter.NearHero.PlayerId, Is.EqualTo(second),
                "The near hero is not the player whose turn it is.");

            yield return AimTheBattlecryAtTheEnemyHero(second);
        }

        /// <summary>
        /// And after several changes of perspective, which is where a screen to
        /// world mistake would show itself.
        /// </summary>
        [UnityTest]
        public IEnumerator Aiming_still_works_after_several_changes_of_perspective()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            yield return EndTurn();
            yield return EndTurn();
            yield return EndTurn();
            yield return EndTurn();

            PlayerId acting = Session.State.CurrentPlayer;

            Assert.That(Presenter.NearHero.PlayerId, Is.EqualTo(acting));

            yield return AimTheBattlecryAtTheEnemyHero(acting);
        }

        private IEnumerator AimTheBattlecryAtTheEnemyHero(PlayerId acting)
        {
            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);

            Assert.That(card, Is.Not.Null, "This seat is not holding the battlecry card.");

            HeroView hero = HeroViewOf(acting.Opponent);
            int before = Session.State.GetPlayer(acting.Opponent).Hero.CurrentHealth;
            int boardBefore = Session.State.GetPlayer(acting).Board.Count;

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay),
                "This seat was never asked for a target.");

            CarryTo(hero.transform.position);
            Click(hero.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(acting.Opponent).Hero.CurrentHealth,
                Is.EqualTo(before - 2), "The battlecry did not land from this seat.");
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(boardBefore + 1));
            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
        }

        // ------------------------------------------------------------------
        //  Discipline
        // ------------------------------------------------------------------

        /// <summary>
        /// Nothing may be aimed while the queue is replaying. The card is not
        /// picked up, so no question is ever asked.
        /// </summary>
        [UnityTest]
        public IEnumerator No_aiming_starts_while_the_queue_is_playing()
        {
            yield return LoadWithScenario(DebugScenarios.AoeId);

            PlayerId acting = Session.State.CurrentPlayer;
            CardView spell = FindCardInHand(DebugScenarios.TestAoe);

            Assert.That(spell, Is.Not.Null);

            // A sweep that kills three minions gives the queue plenty to do.
            Session.Submit(new PlayCardCommand(acting, spell.EntityId));
            yield return null;

            if (Session.IsBusy)
            {
                Click(NearBoardRight);
                Assert.That(Input.State, Is.EqualTo(InteractionState.Resolving),
                    "An interaction started while the queue was playing.");
                Assert.That(Arrow.IsVisible, Is.False);
            }

            yield return Settle();

            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.Resolving),
                "The input never came back after the queue emptied.");
        }

        /// <summary>
        /// An aimed play recorded through the pointer replays to the same
        /// position, target included.
        /// </summary>
        [UnityTest]
        public IEnumerator A_play_aimed_with_the_mouse_replays_identically()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            CoH.App.MatchDebugTools tools = Object.FindFirstObjectByType<CoH.App.MatchDebugTools>();

            Assert.That(tools, Is.Not.Null);

            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            MinionView victim = EnemyMinion();

            Click(card.transform.position);
            CarryTo(NearBoardRight);
            Click(NearBoardRight);
            CarryTo(victim.transform.position);
            Click(victim.transform.position);
            yield return Settle();
            yield return EndTurn();

            bool sawTheTarget = false;

            foreach (ReplayEntry entry in tools.Recording.Entries)
            {
                if (entry.Command.Kind == ReplayCommandKind.PlayCard && !entry.Command.TargetId.IsNone)
                {
                    sawTheTarget = true;
                }
            }

            Assert.That(sawTheTarget, Is.True,
                "The target the mouse chose was never written into the recording.");

            ReplayVerificationResult result = tools.VerifyCurrentReplay();
            Assert.That(result.Success, Is.True, result.Describe());
        }
    }
}
