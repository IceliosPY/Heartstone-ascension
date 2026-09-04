using System;
using CoH.Core.Identifiers;

namespace CoH.Core.Setup
{
    /// <summary>
    /// The numeric constants a match is built on.
    ///
    /// Kept as an injected object rather than as constants scattered through
    /// the rules so tests can build a small board or a tiny hand without
    /// fighting the real values, and so a future format could change them
    /// without touching engine code.
    /// </summary>
    public sealed class GameConfig
    {
        /// <summary>
        /// Default id of the extra card given to the player going second. The
        /// catalog must contain a definition for it; the engine only ever sees
        /// an id.
        /// </summary>
        public static readonly CardId DefaultSecondPlayerExtraCard = new CardId("the_coin");

        /// <summary>
        /// Standard values. Immutable, so sharing a single instance is safe and
        /// carries no global mutable state.
        /// </summary>
        public static readonly GameConfig Default = new GameConfig();

        public GameConfig(
            int startingHeroHealth = 30,
            int maxHandSize = 10,
            int maxBoardSize = 7,
            int maxManaCrystals = 10,
            int deckSize = 30,
            int startingPlayerHandSize = 3,
            int secondPlayerHandSize = 4,
            CardId secondPlayerExtraCard = default,
            CardId heroPowerForSeatOne = default,
            CardId heroPowerForSeatTwo = default)
        {
            if (startingHeroHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingHeroHealth), startingHeroHealth, "Must be positive.");
            }

            if (maxHandSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHandSize), maxHandSize, "Must be positive.");
            }

            if (maxBoardSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBoardSize), maxBoardSize, "Must be positive.");
            }

            if (maxManaCrystals <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxManaCrystals), maxManaCrystals, "Must be positive.");
            }

            if (deckSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deckSize), deckSize, "Must be positive.");
            }

            if (startingPlayerHandSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingPlayerHandSize), startingPlayerHandSize, "Cannot be negative.");
            }

            if (secondPlayerHandSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(secondPlayerHandSize), secondPlayerHandSize, "Cannot be negative.");
            }

            StartingHeroHealth = startingHeroHealth;
            MaxHandSize = maxHandSize;
            MaxBoardSize = maxBoardSize;
            MaxManaCrystals = maxManaCrystals;
            DeckSize = deckSize;
            StartingPlayerHandSize = startingPlayerHandSize;
            SecondPlayerHandSize = secondPlayerHandSize;
            SecondPlayerExtraCard = secondPlayerExtraCard.IsNone ? DefaultSecondPlayerExtraCard : secondPlayerExtraCard;
            HeroPowerForSeatOne = heroPowerForSeatOne;
            HeroPowerForSeatTwo = heroPowerForSeatTwo;
        }

        /// <summary>
        /// The hero power each seat brings, or none.
        ///
        /// Configuration rather than a rule, for exactly the reason
        /// <see cref="SecondPlayerExtraCard"/> is: the engine must never need
        /// to ask which class a seat is. Both default to nothing, so a match
        /// set up without them behaves precisely as every match did before hero
        /// powers existed - which is what keeps the existing tests honest.
        ///
        /// Who is a Necromancer is therefore decided by whoever builds the
        /// match, not here.
        /// </summary>
        public CardId HeroPowerForSeatOne { get; }

        public CardId HeroPowerForSeatTwo { get; }

        /// <summary>The hero power configured for a seat, or none.</summary>
        public CardId HeroPowerFor(PlayerId seat) =>
            seat == PlayerId.Two ? HeroPowerForSeatTwo : HeroPowerForSeatOne;

        /// <summary>This configuration with different hero powers.</summary>
        public GameConfig WithHeroPowers(CardId seatOne, CardId seatTwo) =>
            new GameConfig(
                StartingHeroHealth, MaxHandSize, MaxBoardSize, MaxManaCrystals, DeckSize,
                StartingPlayerHandSize, SecondPlayerHandSize, SecondPlayerExtraCard,
                seatOne, seatTwo);

        public int StartingHeroHealth { get; }

        public int MaxHandSize { get; }

        public int MaxBoardSize { get; }

        public int MaxManaCrystals { get; }

        /// <summary>Expected deck size. Enforcing it is deck-building's job, not the engine's.</summary>
        public int DeckSize { get; }

        /// <summary>Cards dealt to the player who takes the first turn.</summary>
        public int StartingPlayerHandSize { get; }

        /// <summary>Cards dealt to the other player, before the extra card below.</summary>
        public int SecondPlayerHandSize { get; }

        /// <summary>
        /// Card handed to the player who does not start, once the mulligan is
        /// over. In Hearthstone terms this is The Coin.
        ///
        /// Held as configuration rather than hardcoded so the engine never
        /// needs to ask "is this card The Coin?". As far as the rules are
        /// concerned it is an ordinary non-collectible card, and its effect
        /// will come from the data-driven effect system like any other.
        /// </summary>
        public CardId SecondPlayerExtraCard { get; }
    }
}
