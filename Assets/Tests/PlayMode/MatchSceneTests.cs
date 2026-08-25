using System.Collections;
using CoH.App;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;
using CoH.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Smoke tests for the playable scene.
    ///
    /// They deliberately do not retest the rules, which the EditMode suites
    /// already cover far better without a scene. What they check is the wiring:
    /// that pressing play really does build a match, that views appear for what
    /// the engine holds, and that a command sent through the session lands.
    /// </summary>
    public sealed class MatchSceneTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Match.unity";

        private static IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);

            // Durations to zero: these tests are about what the board holds, not
            // about how long the animations that put it there take.
            MatchTestScene.MakeInstant();

            // One frame for Start to run and the opening snapshot to be drawn.
            yield return null;
        }

        [UnityTest]
        public IEnumerator The_scene_loads_and_builds_a_match()
        {
            yield return LoadMatch();

            MatchBootstrap bootstrap = Object.FindFirstObjectByType<MatchBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "No MatchBootstrap in the scene.");

            GameSession session = bootstrap.Session;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.IsReady, Is.True, "The session was never given a server.");
        }

        [UnityTest]
        public IEnumerator The_match_reaches_the_playing_phase_with_a_first_turn()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            GameState state = session.State;

            Assert.That(state.Phase, Is.EqualTo(GamePhase.Playing), "Mulligans should be kept automatically.");
            Assert.That(state.TurnNumber, Is.EqualTo(1));
            Assert.That(state.CurrentPlayer.IsNone, Is.False);
            Assert.That(state.HasEnded, Is.False);
        }

        [UnityTest]
        public IEnumerator The_decks_and_hands_come_from_the_authored_assets()
        {
            yield return LoadMatch();

            GameState state = Object.FindFirstObjectByType<GameSession>().State;
            Player starting = state.GetPlayer(state.StartingPlayer);
            Player second = state.GetPlayer(state.StartingPlayer.Opponent);

            Assert.That(starting.Hand.Count, Is.EqualTo(4), "Three dealt plus the first turn draw.");
            Assert.That(second.Hand.Count, Is.EqualTo(5), "Four dealt plus the extra card.");
            Assert.That(starting.Deck.Count + starting.Hand.Count, Is.EqualTo(30));
        }

        [UnityTest]
        public IEnumerator The_main_views_are_present()
        {
            yield return LoadMatch();

            Assert.That(Object.FindFirstObjectByType<MatchPresenter>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<MatchInputController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PresentationQueue>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<MatchHud>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<BoardAnchors>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);

            HeroView[] heroes = Object.FindObjectsByType<HeroView>(FindObjectsSortMode.None);
            Assert.That(heroes.Length, Is.EqualTo(2), "One hero view per player.");
        }

        [UnityTest]
        public IEnumerator A_card_view_exists_for_every_card_in_hand()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            MatchPresenter presenter = Object.FindFirstObjectByType<MatchPresenter>();
            GameState state = session.State;

            int expected =
                state.GetPlayer(PlayerId.One).Hand.Count +
                state.GetPlayer(PlayerId.Two).Hand.Count;

            Assert.That(presenter.CardViews.Count, Is.EqualTo(expected));

            foreach (CardInstance card in state.GetPlayer(state.CurrentPlayer).Hand)
            {
                Assert.That(presenter.TryGetCardView(card.Id, out CardView view), Is.True,
                    "No view for card " + card.Id);
                Assert.That(view.EntityId, Is.EqualTo(card.Id));
            }
        }

        [UnityTest]
        public IEnumerator Ending_a_turn_through_the_session_hands_play_over()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            PlayerId before = session.State.CurrentPlayer;

            Assert.That(session.Submit(new EndTurnCommand(before)), Is.True);

            // Let the presentation queue replay the batch.
            while (session.IsBusy)
            {
                yield return null;
            }

            Assert.That(session.State.CurrentPlayer, Is.EqualTo(before.Opponent));
            Assert.That(session.State.TurnNumber, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator A_refused_command_leaves_the_match_alone()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            PlayerId idle = session.State.CurrentPlayer.Opponent;
            int turnBefore = session.State.TurnNumber;

            Assert.That(session.Submit(new EndTurnCommand(idle)), Is.False);
            Assert.That(session.Validate(new EndTurnCommand(idle)), Is.EqualTo(RejectionReason.NotYourTurn));
            Assert.That(session.State.TurnNumber, Is.EqualTo(turnBefore));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Playing_a_card_puts_a_minion_view_on_the_board()
        {
            yield return LoadMatch();

            GameSession session = Object.FindFirstObjectByType<GameSession>();
            MatchPresenter presenter = Object.FindFirstObjectByType<MatchPresenter>();

            // Two turns each, so the starting player can afford a 2 mana card.
            for (int turn = 0; turn < 2; turn++)
            {
                session.Submit(new EndTurnCommand(session.State.CurrentPlayer));
                while (session.IsBusy)
                {
                    yield return null;
                }
            }

            PlayerId active = session.State.CurrentPlayer;
            Player player = session.State.GetPlayer(active);
            CardInstance card = player.Hand[0];

            Assert.That(session.Submit(new PlayCardCommand(active, card.Id)), Is.True);

            while (session.IsBusy)
            {
                yield return null;
            }

            Assert.That(player.Board.Count, Is.EqualTo(1));

            Minion summoned = player.Board[0];
            Assert.That(presenter.TryGetMinionView(summoned.Id, out MinionView view), Is.True,
                "The summoned minion has no view.");
            Assert.That(view.EntityId, Is.EqualTo(summoned.Id));
            Assert.That(presenter.TryGetCardView(card.Id, out CardView _), Is.False,
                "The played card should have left the hand.");
        }
    }
}
