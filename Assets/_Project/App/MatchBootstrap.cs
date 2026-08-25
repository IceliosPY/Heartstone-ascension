using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Diagnostics;
using CoH.Core.Rules;
using CoH.Core.Identifiers;
using CoH.Core.Server;
using CoH.Core.Setup;
using CoH.Data;
using CoH.Presentation;
using UnityEngine;

namespace CoH.App
{
    /// <summary>
    /// Starts a match when the scene plays.
    ///
    /// This is the composition root, and the only place that knows about both
    /// authored Unity assets and the rules engine. It converts the assets into
    /// plain runtime data, builds a local server, hands it to the session and
    /// steps aside.
    ///
    /// There is not a single rule in here. It does not decide who starts, what
    /// a card costs or whether a move is legal; it wires objects together and
    /// lets the engine do the rest.
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [Header("Authored data")]
        [SerializeField] private CardCatalogAsset catalog;
        [SerializeField] private DeckListAsset playerOneDeck;
        [SerializeField] private DeckListAsset playerTwoDeck;

        [Tooltip("The same seed always rebuilds the same match, shuffles included.")]
        [SerializeField] private ulong matchSeed = 1UL;

        [Tooltip("Draw a fresh seed on every play, for varying test matches.")]
        [SerializeField] private bool randomizeSeed;

        [Header("Scene")]
        [SerializeField] private GameSession session;
        [SerializeField] private MatchPresenter presenter;

        [Header("Mulligan")]
        [Tooltip("Phase 7 keeps every opening card. A mulligan screen comes later.")]
        [SerializeField] private bool autoKeepOpeningHands = true;

        private CardCatalog _runtimeCatalog;
        private DeckList _deckOne;
        private DeckList _deckTwo;

        private readonly List<ReplayMulligan> _hostMulligans = new List<ReplayMulligan>();

        public GameSession Session => session;

        public MatchPresenter Presenter => presenter;

        public ulong Seed => matchSeed;

        /// <summary>The catalog this match is running on. Needed to verify a replay of it.</summary>
        public CardCatalog RuntimeCatalog => _runtimeCatalog;

        public DeckList DeckOne => _deckOne;

        public DeckList DeckTwo => _deckTwo;

        public GameConfig Config => GameConfig.Default;

        /// <summary>
        /// The mulligans this host settled itself, before anybody could act.
        ///
        /// A recording has to carry them, because they never reach the session
        /// and so never become recorded commands. The day a real mulligan screen
        /// exists the player will submit them and this will be empty, which is
        /// correct in both cases.
        /// </summary>
        public IReadOnlyList<ReplayMulligan> HostMulligans => _hostMulligans;

        /// <summary>
        /// Raised whenever the match behind the session has been replaced, by a
        /// restart, a debug scenario or a replay. Whatever was recorded or
        /// displayed up to that point no longer describes anything.
        /// </summary>
        public event System.Action MatchReplaced;

        private void Start()
        {
            StartMatch();
        }

        /// <summary>Builds and starts the match. Safe to call once.</summary>
        public void StartMatch()
        {
            if (!ValidateWiring())
            {
                return;
            }

            if (randomizeSeed)
            {
                matchSeed = (ulong)System.DateTime.UtcNow.Ticks;
            }

            // Unity authoring data becomes plain runtime data here, and nothing
            // belonging to Unity travels any further.
            _runtimeCatalog = catalog.BuildRuntimeCatalog();
            _deckOne = playerOneDeck.BuildRuntimeDeckList();
            _deckTwo = playerTwoDeck.BuildRuntimeDeckList();

            LocalGameServer server = new LocalGameServer(GameConfig.Default, _runtimeCatalog, matchSeed);

            session.Initialize(server);
            server.StartMatch(_deckOne, _deckTwo);

            _hostMulligans.Clear();

            if (autoKeepOpeningHands)
            {
                KeepEverything(server);

                _hostMulligans.Add(new ReplayMulligan(PlayerId.One, System.Array.Empty<EntityId>()));
                _hostMulligans.Add(new ReplayMulligan(PlayerId.Two, System.Array.Empty<EntityId>()));
            }

            // The opening snapshot is drawn in one go; nothing to replay yet.
            presenter.Rebuild();
            MatchReplaced?.Invoke();
        }

        /// <summary>
        /// Throws the current match away and deals a fresh one.
        ///
        /// A development convenience. It is the same path a match always takes,
        /// only starting again.
        /// </summary>
        public void RestartMatch()
        {
            if (randomizeSeed)
            {
                matchSeed = (ulong)System.DateTime.UtcNow.Ticks;
            }

            StartMatch();
        }

        /// <summary>
        /// Drops the match into a prepared position.
        ///
        /// This is one of the few places a full rebuild from state is the right
        /// answer: nothing that is on screen relates to what is about to be
        /// there, and there are no events to arrive at it through. Every action
        /// taken afterwards goes back to being driven by events.
        /// </summary>
        public bool LoadScenario(string scenarioId)
        {
            if (!ValidateWiring() || _runtimeCatalog == null)
            {
                return false;
            }

            if (!DebugScenarios.TryFind(scenarioId, out DebugScenario scenario))
            {
                Debug.LogError("MatchBootstrap: there is no debug scenario called '" + scenarioId + "'.", this);
                return false;
            }

            GameEngine engine = GameEngine.FromState(
                DebugScenarioBuilder.Build(scenario, _runtimeCatalog, GameConfig.Default));

            AdoptMatch(LocalGameServer.Wrapping(engine));
            return true;
        }

        /// <summary>
        /// Rebuilds the position a replay started from, ready for its commands
        /// to be fed through the ordinary session path.
        /// </summary>
        public bool LoadReplayStart(ReplayRecord record)
        {
            if (record == null || !ValidateWiring() || _runtimeCatalog == null)
            {
                return false;
            }

            try
            {
                AdoptMatch(LocalGameServer.Wrapping(ReplayVerifier.BuildEngine(record, _runtimeCatalog)));
                return true;
            }
            catch (System.Exception error)
            {
                Debug.LogError("MatchBootstrap: this replay cannot be started. " + error.Message, this);
                return false;
            }
        }

        private void AdoptMatch(LocalGameServer server)
        {
            session.Rebind(server);

            if (session.Queue != null)
            {
                session.Queue.FlushImmediately();
            }

            presenter.Rebuild();
            MatchReplaced?.Invoke();
        }

        /// <summary>
        /// Confirms both mulligans with nothing replaced, which is what starts
        /// the first turn. A real mulligan screen replaces this later without
        /// the engine changing at all.
        /// </summary>
        private static void KeepEverything(IGameServer server)
        {
            server.Execute(new MulliganCommand(PlayerId.One));
            server.Execute(new MulliganCommand(PlayerId.Two));
        }

        private bool ValidateWiring()
        {
            if (catalog == null)
            {
                Debug.LogError("MatchBootstrap: no card catalog assigned.", this);
                return false;
            }

            if (playerOneDeck == null || playerTwoDeck == null)
            {
                Debug.LogError("MatchBootstrap: both deck lists must be assigned.", this);
                return false;
            }

            if (session == null || presenter == null)
            {
                Debug.LogError("MatchBootstrap: the session and presenter must be assigned.", this);
                return false;
            }

            return true;
        }
    }
}
