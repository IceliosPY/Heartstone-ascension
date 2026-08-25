using System.Collections;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// The effect cards, played from the scene through the pointer.
    ///
    /// The point of these is that nothing new had to be built to show them. A
    /// battlecry that deals damage produces the damage events Phase 9 already
    /// animates; a deathrattle that draws produces a draw; a summon produces a
    /// summon. If any of that had needed its own presentation, the architecture
    /// would have been wrong.
    ///
    /// Aiming a card also reuses the arrow and the highlights the attack
    /// targeting was built from, rather than a second way to point at things.
    /// </summary>
    public sealed class EffectPresentationTests : InteractionTestBase
    {
        private TargetingArrow Arrow => Object.FindFirstObjectByType<TargetingArrow>();

        private List<GameEvent> Watch()
        {
            List<GameEvent> staged = new List<GameEvent>();
            Session.Queue.Staging += staged.Add;
            return staged;
        }

        private static bool Contains<T>(IReadOnlyList<GameEvent> events) where T : GameEvent
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index] is T)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        //  Aiming a card
        // ------------------------------------------------------------------

        /// <summary>
        /// Dropping a targeted card on the board asks a question rather than
        /// playing it, and the highlighted answers are the engine's list.
        /// </summary>
        [UnityTest]
        public IEnumerator A_targeted_card_asks_for_a_target_and_highlights_the_legal_ones()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);

            Assert.That(card, Is.Not.Null);
            Assert.That(card.IsPlayable, Is.True, "A card waiting to be aimed should read as playable.");

            IReadOnlyList<EntityId> expected = Session.GetLegalPlayTargets(acting, card.EntityId);
            Assert.That(expected, Is.Not.Empty);

            Drag(card.transform.position, NearBoardRight);

            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay),
                "Dropping it should have started the aiming step.");

            Assert.That(Input.HighlightedTargets, Is.EquivalentTo(expected),
                "The highlighted targets are not the ones the engine listed.");

            MoveTo(NearBoardAt(2f));
            Assert.That(Arrow.IsVisible, Is.True, "No arrow is drawn while aiming a card.");

            // Nothing has happened yet: the card is still in hand.
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.Zero);
            Assert.That(Presenter.TryGetCardView(card.EntityId, out CardView _), Is.True);
        }

        [UnityTest]
        public IEnumerator Releasing_on_a_legal_target_plays_the_card_and_its_battlecry_lands()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId enemy = acting.Opponent;

            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            EntityId victimId = Session.State.GetPlayer(enemy).Board[0].Id;

            Assert.That(Presenter.TryGetMinionView(victimId, out MinionView victim), Is.True);

            int before = Session.State.GetPlayer(enemy).Board[0].CurrentHealth;

            List<GameEvent> staged = Watch();

            Drag(card.transform.position, NearBoardRight);
            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));

            Press(victim.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.EqualTo(1),
                "The minion should have arrived.");
            Assert.That(Session.State.GetPlayer(enemy).Board[0].CurrentHealth, Is.EqualTo(before - 2),
                "The battlecry did not land.");

            // And it was shown with the animations that already existed.
            Assert.That(Contains<CardPlayedEvent>(staged), Is.True);
            Assert.That(Contains<MinionSummonedEvent>(staged), Is.True);
            Assert.That(Contains<DamageDealtEvent>(staged), Is.True);

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False, "The arrow survived the play.");
            Assert.That(Session.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator Releasing_somewhere_illegal_cancels_and_leaves_the_card_in_hand()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            PlayerId acting = Session.State.CurrentPlayer;
            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            EntityId id = card.EntityId;

            int handBefore = Session.State.GetPlayer(acting).Hand.Count;
            int manaBefore = Session.State.GetPlayer(acting).AvailableMana;

            Drag(card.transform.position, NearBoardRight);
            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));

            Press(EmptySpace);
            yield return Settle();

            Assert.That(Input.State, Is.EqualTo(InteractionState.Idle));
            Assert.That(Arrow.IsVisible, Is.False);
            Assert.That(Input.HighlightedTargets, Is.Empty, "Highlights stayed after a cancel.");

            Player player = Session.State.GetPlayer(acting);

            Assert.That(player.Board.Count, Is.Zero, "The card was played anyway.");
            Assert.That(player.Hand.Count, Is.EqualTo(handBefore), "The card left the hand.");
            Assert.That(player.AvailableMana, Is.EqualTo(manaBefore), "Mana was spent on a cancel.");
            Assert.That(Presenter.TryGetCardView(id, out CardView _), Is.True, "The card lost its view.");
        }

        // ------------------------------------------------------------------
        //  The rest of the set
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator A_deathrattle_draws_and_the_draw_is_animated()
        {
            yield return LoadWithScenario(DebugScenarios.DeathrattleId);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId enemy = acting.Opponent;

            EntityId scribeId = Session.State.GetPlayer(acting).Board[0].Id;
            EntityId defenderId = Session.State.GetPlayer(enemy).Board[0].Id;

            Assert.That(Presenter.TryGetMinionView(scribeId, out MinionView scribe), Is.True);
            Assert.That(Presenter.TryGetMinionView(defenderId, out MinionView defender), Is.True);

            int handBefore = Session.State.GetPlayer(acting).Hand.Count;

            List<GameEvent> staged = Watch();

            // One attack into a two for three finishes the scribe.
            Drag(scribe.transform.position, defender.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.Zero,
                "The scribe should have died.");
            Assert.That(Session.State.GetPlayer(acting).Hand.Count, Is.EqualTo(handBefore + 1),
                "The deathrattle did not draw.");

            Assert.That(Contains<MinionDiedEvent>(staged), Is.True);
            Assert.That(Contains<CardDrawnEvent>(staged), Is.True,
                "The draw was never staged, so no draw animation played.");

            Assert.That(Presenter.TryGetMinionView(scribeId, out MinionView _), Is.False);
        }

        [UnityTest]
        public IEnumerator A_summoning_battlecry_puts_real_minions_on_the_board()
        {
            yield return LoadWithScenario(DebugScenarios.SummonId);

            PlayerId acting = Session.State.CurrentPlayer;
            int before = Session.State.GetPlayer(acting).Board.Count;

            CardView card = FindCardInHand(DebugScenarios.TestSummoner);
            Assert.That(card, Is.Not.Null);

            List<GameEvent> staged = Watch();

            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            Player player = Session.State.GetPlayer(acting);

            // The summoner itself, plus its two tokens.
            Assert.That(player.Board.Count, Is.EqualTo(before + 3));

            int summons = 0;

            foreach (GameEvent reported in staged)
            {
                if (reported is MinionSummonedEvent)
                {
                    summons++;
                }
            }

            Assert.That(summons, Is.EqualTo(3), "Every arrival should have been staged.");

            // And every one of them has a view.
            for (int slot = 0; slot < player.Board.Count; slot++)
            {
                Assert.That(Presenter.TryGetMinionView(player.Board[slot].Id, out MinionView _), Is.True,
                    "A minion on the board has no view.");
            }
        }

        [UnityTest]
        public IEnumerator A_buff_shows_the_new_numbers_on_the_minion()
        {
            yield return LoadWithScenario(DebugScenarios.BuffId);

            PlayerId acting = Session.State.CurrentPlayer;
            EntityId targetId = Session.State.GetPlayer(acting).Board[0].Id;

            Assert.That(Presenter.TryGetMinionView(targetId, out MinionView target), Is.True);

            CardView card = FindCardInHand(DebugScenarios.TestBuff);
            Assert.That(card, Is.Not.Null);

            Drag(card.transform.position, NearBoardRight);
            Assert.That(Input.State, Is.EqualTo(InteractionState.TargetingPlay));

            Press(target.transform.position);
            yield return Settle();

            Minion buffed = null;
            Player player = Session.State.GetPlayer(acting);

            for (int slot = 0; slot < player.Board.Count; slot++)
            {
                if (player.Board[slot].Id == targetId)
                {
                    buffed = player.Board[slot];
                }
            }

            Assert.That(buffed, Is.Not.Null);
            Assert.That(buffed.Attack, Is.EqualTo(3));
            Assert.That(buffed.MaxHealth, Is.EqualTo(4));
            Assert.That(buffed.BaseAttack, Is.EqualTo(2), "The printed card was changed.");
        }

        [UnityTest]
        public IEnumerator A_sweep_damages_every_enemy_minion_and_the_views_end_up_correct()
        {
            yield return LoadWithScenario(DebugScenarios.AoeId);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId enemy = acting.Opponent;

            List<EntityId> victims = new List<EntityId>();
            Player enemyPlayer = Session.State.GetPlayer(enemy);

            for (int slot = 0; slot < enemyPlayer.Board.Count; slot++)
            {
                victims.Add(enemyPlayer.Board[slot].Id);
            }

            Assert.That(victims.Count, Is.EqualTo(3));

            CardView spell = FindCardInHand(DebugScenarios.TestAoe);
            Assert.That(spell, Is.Not.Null);

            List<GameEvent> staged = Watch();

            Drag(spell.transform.position, NearBoardRight);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(enemy).Board.Count, Is.Zero,
                "All three should have died to one damage each.");

            int deaths = 0;
            int hits = 0;

            foreach (GameEvent reported in staged)
            {
                if (reported is MinionDiedEvent)
                {
                    deaths++;
                }

                if (reported is DamageDealtEvent)
                {
                    hits++;
                }
            }

            Assert.That(hits, Is.EqualTo(3));
            Assert.That(deaths, Is.EqualTo(3));

            // No view outlived its minion.
            for (int index = 0; index < victims.Count; index++)
            {
                Assert.That(Presenter.TryGetMinionView(victims[index], out MinionView _), Is.False,
                    "A swept minion kept its view.");
            }

            Assert.That(Session.IsBusy, Is.False);
            Assert.That(Session.State.GetPlayer(acting).Board.Count, Is.Zero,
                "A spell put something on the board.");
        }

        // ------------------------------------------------------------------
        //  Recording
        // ------------------------------------------------------------------

        /// <summary>
        /// A match full of effects still replays. The effects are not in the
        /// recording; the catalog is reloaded and the commands are run again.
        /// </summary>
        [UnityTest]
        public IEnumerator A_session_using_effects_verifies_as_deterministic()
        {
            yield return LoadWithScenario(DebugScenarios.BattlecryTargetId);

            CoH.App.MatchDebugTools tools = Object.FindFirstObjectByType<CoH.App.MatchDebugTools>();
            Assert.That(tools, Is.Not.Null);

            PlayerId acting = Session.State.CurrentPlayer;
            PlayerId enemy = acting.Opponent;

            CardView card = FindCardInHand(DebugScenarios.TestBattlecryDamage);
            EntityId victim = Session.State.GetPlayer(enemy).Board[0].Id;

            // A targeted play, recorded with its target.
            Session.Submit(new PlayCardCommand(acting, card.EntityId, 0, victim));
            yield return Settle();
            yield return EndTurn();

            ReplayRecord record = tools.Recording;

            Assert.That(record.InitialSource, Is.EqualTo(ReplayInitialSource.Scenario));
            Assert.That(record.ScenarioId, Is.EqualTo(DebugScenarios.BattlecryTargetId));

            bool sawTheTarget = false;

            foreach (ReplayEntry entry in record.Entries)
            {
                if (entry.Command.Kind == ReplayCommandKind.PlayCard && !entry.Command.TargetId.IsNone)
                {
                    sawTheTarget = true;
                }
            }

            Assert.That(sawTheTarget, Is.True, "The chosen target was not recorded.");

            ReplayVerificationResult result = tools.VerifyCurrentReplay();
            Assert.That(result.Success, Is.True, result.Describe());
        }
    }
}
