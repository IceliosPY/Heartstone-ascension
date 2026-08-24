using CoH.Core.Cards;
using CoH.Core.Commands;
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

        public GameSession Session => session;

        public ulong Seed => matchSeed;

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
            CardCatalog runtimeCatalog = catalog.BuildRuntimeCatalog();
            DeckList deckOne = playerOneDeck.BuildRuntimeDeckList();
            DeckList deckTwo = playerTwoDeck.BuildRuntimeDeckList();

            LocalGameServer server = new LocalGameServer(GameConfig.Default, runtimeCatalog, matchSeed);

            session.Initialize(server);
            server.StartMatch(deckOne, deckTwo);

            if (autoKeepOpeningHands)
            {
                KeepEverything(server);
            }

            // The opening snapshot is drawn in one go; nothing to replay yet.
            presenter.Rebuild();
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
