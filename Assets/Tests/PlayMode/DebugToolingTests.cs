using System.Collections;
using System.Collections.Generic;
using CoH.App;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
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
    /// The developer tools, running inside a real match.
    ///
    /// What is checked here is that they are wired to the real thing: that a
    /// scenario really lands on the board the interaction can be used on, that
    /// a replay really travels the same road the player does, and that nothing
    /// they do leaves the game stuck. How the panel looks is not tested and
    /// should not be.
    /// </summary>
    public sealed class DebugToolingTests : InteractionTestBase
    {
        private MatchDebugTools _tools;
        private DebugOverlay _overlay;
        private MatchBootstrap _bootstrap;

        private IEnumerator LoadWithTools()
        {
            yield return LoadMatch();

            _tools = Object.FindFirstObjectByType<MatchDebugTools>();
            _overlay = Object.FindFirstObjectByType<DebugOverlay>();
            _bootstrap = Object.FindFirstObjectByType<MatchBootstrap>();

            Assert.That(_tools, Is.Not.Null, "The scene has no MatchDebugTools.");
            Assert.That(_overlay, Is.Not.Null, "The scene has no DebugOverlay.");
            Assert.That(_bootstrap, Is.Not.Null, "The scene has no MatchBootstrap.");
        }

        // ------------------------------------------------------------------
        //  The panel
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator The_debug_panel_opens_and_closes_and_starts_closed()
        {
            yield return LoadWithTools();

            Assert.That(_overlay.IsOpen, Is.False, "The developer panel must not be in the way by default.");

            _overlay.SetOpen(true);
            yield return null;
            Assert.That(_overlay.IsOpen, Is.True);

            _overlay.Toggle();
            yield return null;
            Assert.That(_overlay.IsOpen, Is.False);
        }

        /// <summary>
        /// The panel must survive being shown at any moment, including a match
        /// mid sequence, without throwing on something that is not there.
        /// </summary>
        [UnityTest]
        public IEnumerator The_panel_can_be_refreshed_at_any_point_of_a_match()
        {
            yield return LoadWithTools();

            _overlay.SetOpen(true);
            _overlay.Refresh();

            yield return AdvanceUntilSomethingIsPlayable();

            _overlay.Refresh();

            CardView card = FirstPlayableCard();
            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            _overlay.Refresh();

            Assert.That(_overlay.IsOpen, Is.True);
        }

        // ------------------------------------------------------------------
        //  Recording
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator A_normal_session_records_itself_without_being_asked()
        {
            yield return LoadWithTools();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            ReplayRecord record = _tools.Recording;

            Assert.That(record, Is.Not.Null, "Nothing was recorded.");
            Assert.That(record.CommandCount, Is.GreaterThan(0));
            Assert.That(record.Seed, Is.EqualTo(_bootstrap.Seed));
            Assert.That(record.CatalogFingerprint, Is.Not.Empty);

            bool sawAPlay = false;

            foreach (ReplayEntry entry in record.Entries)
            {
                if (entry.Command.Kind == ReplayCommandKind.PlayCard)
                {
                    sawAPlay = true;
                }
            }

            Assert.That(sawAPlay, Is.True, "The card that was dragged onto the board was not recorded.");
        }

        [UnityTest]
        public IEnumerator The_current_session_verifies_as_deterministic()
        {
            yield return LoadWithTools();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            Drag(card.transform.position, NearBoardRight);
            yield return Settle();
            yield return EndTurn();

            string before = StateFingerprint.Of(Session.State);

            ReplayVerificationResult result = _tools.VerifyCurrentReplay();

            Assert.That(result.Success, Is.True, result.Describe());
            Assert.That(result.Kind, Is.EqualTo(DivergenceKind.None));

            // And checking must never disturb the match being played.
            Assert.That(StateFingerprint.Of(Session.State), Is.EqualTo(before),
                "Verifying a replay changed the match on screen.");
        }

        [UnityTest]
        public IEnumerator Exporting_a_replay_writes_a_file_that_reads_back()
        {
            yield return LoadWithTools();
            yield return AdvanceUntilSomethingIsPlayable();

            CardView card = FirstPlayableCard();
            Drag(card.transform.position, NearBoardRight);
            yield return Settle();

            string path = _tools.ExportCurrentReplay();

            Assert.That(path, Is.Not.Empty, "Nothing was exported.");
            Assert.That(System.IO.File.Exists(path), Is.True, "The export named a file that is not there.");

            try
            {
                ReplayRecord reloaded = ReplayFiles.Load(path);

                Assert.That(reloaded.FormatVersion, Is.EqualTo(ReplayFormat.CurrentVersion));
                Assert.That(reloaded.CommandCount, Is.EqualTo(_tools.Recording.CommandCount));
                Assert.That(reloaded.Seed, Is.EqualTo(_tools.Recording.Seed));

                // And it is worth something: it verifies.
                Assert.That(_tools.Verify(reloaded).Success, Is.True);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        // ------------------------------------------------------------------
        //  Scenarios
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Loading_a_scenario_puts_it_on_the_board_and_leaves_the_game_playable()
        {
            yield return LoadWithTools();

            Assert.That(_tools.LoadScenario(DebugScenarios.ReadyCombatId), Is.True);
            yield return null;

            GameState state = Session.State;

            Assert.That(state.CurrentPlayer, Is.EqualTo(PlayerId.One));
            Assert.That(state.GetPlayer(PlayerId.One).Board.Count, Is.EqualTo(1));
            Assert.That(state.GetPlayer(PlayerId.Two).Board.Count, Is.EqualTo(1));

            // The presentation has to actually show it.
            EntityId mine = state.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId theirs = state.GetPlayer(PlayerId.Two).Board[0].Id;

            Assert.That(Presenter.TryGetMinionView(mine, out MinionView attacker), Is.True,
                "The scenario's minion has no view.");
            Assert.That(Presenter.TryGetMinionView(theirs, out MinionView defender), Is.True);
            Assert.That(Presenter.Viewpoint, Is.EqualTo(PlayerId.One));

            Assert.That(Session.IsBusy, Is.False, "Loading a scenario left the input locked.");

            // And it can be played immediately, through the ordinary pointer.
            Drag(attacker.transform.position, defender.transform.position);
            yield return Settle();

            Assert.That(state.GetPlayer(PlayerId.One).Board[0].CurrentHealth, Is.EqualTo(1));
            Assert.That(state.GetPlayer(PlayerId.Two).Board[0].CurrentHealth, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator The_double_death_scenario_really_kills_both_on_screen()
        {
            yield return LoadWithTools();

            Assert.That(_tools.LoadScenario(DebugScenarios.DoubleDeathId), Is.True);
            yield return null;

            EntityId mine = Session.State.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId theirs = Session.State.GetPlayer(PlayerId.Two).Board[0].Id;

            Assert.That(Presenter.TryGetMinionView(mine, out MinionView attacker), Is.True);
            Assert.That(Presenter.TryGetMinionView(theirs, out MinionView defender), Is.True);

            Drag(attacker.transform.position, defender.transform.position);
            yield return Settle();

            Assert.That(Session.State.GetPlayer(PlayerId.One).Board.Count, Is.Zero);
            Assert.That(Session.State.GetPlayer(PlayerId.Two).Board.Count, Is.Zero);
            Assert.That(Presenter.TryGetMinionView(mine, out MinionView _), Is.False);
            Assert.That(Presenter.TryGetMinionView(theirs, out MinionView _), Is.False);
        }

        [UnityTest]
        public IEnumerator A_session_started_from_a_scenario_records_which_one()
        {
            yield return LoadWithTools();

            _tools.LoadScenario(DebugScenarios.HeroLethalId);
            yield return null;

            EntityId attacker = Session.State.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId enemyHero = Session.State.GetPlayer(PlayerId.Two).Hero.Id;

            Session.Submit(new AttackCommand(PlayerId.One, attacker, enemyHero));
            yield return Settle();

            ReplayRecord record = _tools.Recording;

            Assert.That(record.InitialSource, Is.EqualTo(ReplayInitialSource.Scenario));
            Assert.That(record.ScenarioId, Is.EqualTo(DebugScenarios.HeroLethalId));
            Assert.That(record.CommandCount, Is.GreaterThan(0));

            Assert.That(Session.State.HasEnded, Is.True, "This scenario is one swing from over.");

            // And that recording rebuilds the very same position.
            ReplayVerificationResult result = _tools.VerifyCurrentReplay();
            Assert.That(result.Success, Is.True, result.Describe());
        }

        // ------------------------------------------------------------------
        //  Visual replay
        // ------------------------------------------------------------------

        /// <summary>
        /// A replay on screen has to travel the same road the player does:
        /// through the session, into the engine, out as events, and staged by
        /// the presentation queue. A second renderer for replays would be a
        /// second thing to keep correct.
        /// </summary>
        [UnityTest]
        public IEnumerator A_visual_replay_goes_through_the_session_and_the_queue()
        {
            yield return LoadWithTools();

            _tools.LoadScenario(DebugScenarios.ReadyCombatId);
            yield return null;

            EntityId attacker = Session.State.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId defender = Session.State.GetPlayer(PlayerId.Two).Board[0].Id;

            Session.Submit(new AttackCommand(PlayerId.One, attacker, defender));
            yield return Settle();
            yield return EndTurn();

            ReplayRecord record = _tools.Recording;
            Assert.That(record.CommandCount, Is.EqualTo(2));

            string expected = StateFingerprint.Of(Session.State);

            // Watch the queue while the replay runs. The delegate is kept so it
            // can actually be removed again afterwards.
            List<GameEvent> staged = new List<GameEvent>();
            System.Action<GameEvent> watcher = staged.Add;

            Session.Queue.Staging += watcher;

            _tools.PlayReplay(record);

            yield return WaitUntil(() => !_tools.IsReplaying, seconds: 20f);
            yield return Settle();

            Session.Queue.Staging -= watcher;

            Assert.That(_tools.IsReplaying, Is.False, "The replay never finished.");
            Assert.That(staged, Is.Not.Empty,
                "The replay produced no staged events, so it did not use the normal path.");

            bool sawAnAttack = false;

            foreach (GameEvent staged_event in staged)
            {
                if (staged_event is AttackDeclaredEvent)
                {
                    sawAnAttack = true;
                }
            }

            Assert.That(sawAnAttack, Is.True, "The replayed attack was never staged for animation.");

            // And it arrived exactly where the recording did.
            Assert.That(StateFingerprint.Of(Session.State), Is.EqualTo(expected),
                "The replay ended somewhere else than the session it came from.");
        }

        [UnityTest]
        public IEnumerator A_visual_replay_hands_the_input_back_when_it_finishes()
        {
            yield return LoadWithTools();

            _tools.LoadScenario(DebugScenarios.ReadyCombatId);
            yield return null;

            EntityId attacker = Session.State.GetPlayer(PlayerId.One).Board[0].Id;
            EntityId defender = Session.State.GetPlayer(PlayerId.Two).Board[0].Id;

            Session.Submit(new AttackCommand(PlayerId.One, attacker, defender));
            yield return Settle();

            ReplayRecord record = _tools.Recording;

            _tools.PlayReplay(record);
            yield return WaitUntil(() => !_tools.IsReplaying, seconds: 20f);
            yield return Settle();

            Assert.That(Session.IsBusy, Is.False, "The replay left the queue running.");
            Assert.That(Input.State, Is.Not.EqualTo(InteractionState.Resolving),
                "The replay left the input locked.");

            // The board is playable again straight away.
            yield return EndTurn();
            Assert.That(Session.State.CurrentPlayer, Is.EqualTo(PlayerId.Two));
        }
    }
}
