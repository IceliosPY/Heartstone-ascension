using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Core.Random;
using CoH.Core.Setup;

namespace CoH.Core.State
{
    /// <summary>
    /// The complete state of a match, and the only place entities are created.
    ///
    /// Constructing a GameState sets up two players with an empty board, hand,
    /// deck and graveyard. It deliberately does not shuffle, deal, pick a
    /// starting player or grant mana: those are rules and belong to the game
    /// loop, not to state construction.
    ///
    /// Determinism: the random source is built from the seed inside the
    /// constructor rather than injected, so a match cannot accidentally be
    /// given a non-reproducible generator. Identifiers and timestamps come
    /// from per-match counters, never from statics, GUIDs or the clock.
    /// </summary>
    public sealed class GameState
    {
        private readonly Player[] _players;
        private readonly Dictionary<EntityId, Entity> _entitiesById;
        private readonly EntityIdGenerator _entityIds;
        private int _lastTimestamp;

        public GameState(GameConfig config, ICardCatalog catalog, ulong seed)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Seed = seed;
            RandomSource = new Pcg32Random(seed);

            _entityIds = new EntityIdGenerator();
            _entitiesById = new Dictionary<EntityId, Entity>();
            _players = new Player[2];

            for (int index = 0; index < _players.Length; index++)
            {
                PlayerId playerId = PlayerId.FromIndex(index);
                Hero hero = new Hero(_entityIds.Next(), playerId, config.StartingHeroHealth);

                // Heroes are in play from the moment the match state exists, so
                // they get the first timestamps. Without one they would sort
                // ahead of every minion in a death phase purely because zero is
                // the smallest number.
                hero.Timestamp = NextTimestamp();

                RegisterEntity(hero);
                _players[index] = new Player(playerId, hero, config);
            }

            CurrentPlayer = PlayerId.None;
            StartingPlayer = PlayerId.None;
            Phase = GamePhase.Setup;
            Result = GameResult.InProgress;
        }

        public GameConfig Config { get; }

        public ICardCatalog Catalog { get; }

        /// <summary>Seed this match was built from. Replaying it reproduces the match exactly.</summary>
        public ulong Seed { get; }

        /// <summary>
        /// The only randomness allowed during this match. Named RandomSource
        /// rather than Random so the member never collides with the
        /// CoH.Core.Random namespace at the call site.
        /// </summary>
        public IRandomSource RandomSource { get; }

        public IReadOnlyList<Player> Players => _players;

        /// <summary>
        /// Turns started since the match began, counted across the whole match
        /// and not per player: the very first turn is 1, the reply is 2, and so
        /// on. Zero while the match has not reached the playing phase.
        ///
        /// For "how many turns has this player had", read Player.TurnsTaken.
        /// The two are deliberately separate values rather than one integer
        /// with two meanings.
        /// </summary>
        public int TurnNumber { get; internal set; }

        /// <summary>Whose turn it is. None outside the playing phase.</summary>
        public PlayerId CurrentPlayer { get; internal set; }

        /// <summary>Which stage the match is in.</summary>
        public GamePhase Phase { get; internal set; }

        /// <summary>
        /// The player who takes the first turn, drawn from the match random
        /// source at setup. Unrelated to PlayerId.One, which is only a seat.
        /// </summary>
        public PlayerId StartingPlayer { get; internal set; }

        /// <summary>
        /// The outcome of the match. The single source of truth: nothing else
        /// decides whether the match is over.
        /// </summary>
        public GameResult Result { get; internal set; }

        /// <summary>
        /// Convenience view of <see cref="Result"/>. None while the match runs
        /// and on a draw, so never read it without checking the result first.
        /// </summary>
        public PlayerId Winner
        {
            get
            {
                if (Result == GameResult.PlayerOneWins)
                {
                    return PlayerId.One;
                }

                if (Result == GameResult.PlayerTwoWins)
                {
                    return PlayerId.Two;
                }

                return PlayerId.None;
            }
        }

        /// <summary>
        /// Derived from <see cref="Result"/> rather than from the phase, so
        /// there is exactly one thing to look at to know whether the match is
        /// over.
        /// </summary>
        public bool HasEnded => Result != GameResult.InProgress;

        public Player GetPlayer(PlayerId id)
        {
            if (id.IsNone)
            {
                throw new ArgumentException("PlayerId.None does not designate a player.", nameof(id));
            }

            return _players[id.Index];
        }

        /// <summary>Convenience accessor for the player facing <paramref name="id"/>.</summary>
        public Player GetOpponentOf(PlayerId id) => GetPlayer(id.Opponent);

        public bool TryGetEntity(EntityId id, out Entity entity) =>
            _entitiesById.TryGetValue(id, out entity);

        public Entity GetEntity(EntityId id)
        {
            if (!_entitiesById.TryGetValue(id, out Entity entity))
            {
                throw new KeyNotFoundException("Unknown entity id: " + id);
            }

            return entity;
        }

        /// <summary>Number of entities created in this match so far.</summary>
        public int EntityCount => _entitiesById.Count;

        /// <summary>
        /// Next order-of-entry stamp. Handed out on entering play, not on
        /// creation, so the sequence reflects the order things actually reached
        /// the board.
        /// </summary>
        internal int NextTimestamp()
        {
            _lastTimestamp++;
            return _lastTimestamp;
        }

        /// <summary>
        /// Creates a card instance for a definition that must exist in the
        /// catalog. Not placed in any zone: putting it somewhere is a rule.
        /// </summary>
        internal CardInstance CreateCardInstance(CardId cardId, PlayerId owner)
        {
            RequireKnownCard(cardId);

            CardInstance instance = new CardInstance(_entityIds.Next(), owner, cardId);
            RegisterEntity(instance);
            return instance;
        }

        /// <summary>
        /// Creates a minion whose base statistics are copied from the card
        /// definition. The definition itself is only read, never modified.
        /// </summary>
        internal Minion CreateMinion(CardId cardId, PlayerId owner)
        {
            CardDefinition definition = RequireKnownCard(cardId);

            Minion minion = new Minion(_entityIds.Next(), owner, definition);
            RegisterEntity(minion);
            return minion;
        }

        private CardDefinition RequireKnownCard(CardId cardId)
        {
            if (!Catalog.TryGet(cardId, out CardDefinition definition))
            {
                throw new ArgumentException("Card id is not in the catalog: " + cardId, nameof(cardId));
            }

            return definition;
        }

        private void RegisterEntity(Entity entity)
        {
            _entitiesById.Add(entity.Id, entity);
        }
    }
}
