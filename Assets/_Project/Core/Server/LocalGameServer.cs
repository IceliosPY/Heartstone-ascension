using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
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

        private LocalGameServer(GameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>
        /// Serves a match that was prepared rather than dealt: a debug scenario,
        /// or a replay being run again.
        ///
        /// Separate from the constructor because it is not how a match starts.
        /// A real one is always built from two decks and a seed, and everything
        /// above this interface is unable to tell the difference either way.
        /// </summary>
        public static LocalGameServer Wrapping(GameEngine engine) => new LocalGameServer(engine);

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

        public RejectionReason CanAttack(PlayerId playerId, EntityId attackerId) =>
            _engine.CanAttack(playerId, attackerId);

        public PlayTargetRequirement GetPlayTargetRequirement(PlayerId playerId, EntityId cardInstanceId) =>
            _engine.GetPlayTargetRequirement(playerId, cardInstanceId);

        public IReadOnlyList<EntityId> GetLegalPlayTargets(PlayerId playerId, EntityId cardInstanceId) =>
            _engine.GetLegalPlayTargets(playerId, cardInstanceId);

        public RejectionReason CanUseHeroPower(PlayerId playerId) =>
            _engine.CanUseHeroPower(playerId);

        public IReadOnlyList<EffectDefinition> GetHeroPowerOptions(PlayerId playerId) =>
            _engine.GetHeroPowerOptions(playerId);

        public RejectionReason CanPlayCard(PlayerId playerId, EntityId cardInstanceId) =>
            _engine.CanPlayCard(playerId, cardInstanceId);
    }
}
