using System;

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
        /// Standard values. Immutable, so sharing a single instance is safe and
        /// carries no global mutable state.
        /// </summary>
        public static readonly GameConfig Default = new GameConfig();

        public GameConfig(
            int startingHeroHealth = 30,
            int maxHandSize = 10,
            int maxBoardSize = 7,
            int maxManaCrystals = 10,
            int deckSize = 30)
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

            StartingHeroHealth = startingHeroHealth;
            MaxHandSize = maxHandSize;
            MaxBoardSize = maxBoardSize;
            MaxManaCrystals = maxManaCrystals;
            DeckSize = deckSize;
        }

        public int StartingHeroHealth { get; }

        public int MaxHandSize { get; }

        public int MaxBoardSize { get; }

        public int MaxManaCrystals { get; }

        /// <summary>Expected deck size. Enforcing it is deck-building's job, not the engine's.</summary>
        public int DeckSize { get; }
    }
}
