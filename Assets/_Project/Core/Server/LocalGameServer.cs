using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Core.Server
{
    /// <summary>
    /// An <see cref="IGameServer"/> that runs the engine in this process.
    ///
    /// A thin wrapper on purpose. Its value is not what it does but what it
    /// proves: the presentation already talks to the rules through an interface
    /// it cannot reach behind, so nothing above it will need rewriting the day
    /// the engine moves to a server.
    ///
    /// Starting the match is deliberately not on the interface. Setting up a
    /// match is something a host does, not something a client may ask for.
    /// </summary>
    public sealed class LocalGameServer : IGameServer
    {
        private readonly GameEngine _engine;

        public LocalGameServer(GameConfig config, ICardCatalog catalog, ulong seed)
        {
            _engine = new GameEngine(config, catalog, seed);
        }

        public GameState State => _engine.State;

        /// <summary>Host-side setup. Returns everything that happened while building the match.</summary>
        public IReadOnlyList<GameEvent> StartMatch(DeckList deckForSeatOne, DeckList deckForSeatTwo)
        {
            if (deckForSeatOne == null)
            {
                throw new ArgumentNullException(nameof(deckForSeatOne));
            }

            if (deckForSeatTwo == null)
            {
                throw new ArgumentNullException(nameof(deckForSeatTwo));
            }

            return _engine.StartMatch(deckForSeatOne, deckForSeatTwo);
        }

        public CommandResult Execute(GameCommand command) => _engine.Execute(command);

        public RejectionReason CanExecute(GameCommand command) => _engine.CanExecute(command);

        public IReadOnlyList<EntityId> GetLegalAttackTargets(PlayerId playerId, EntityId attackerId) =>
            _engine.GetLegalAttackTargets(playerId, attackerId);
    }
}
