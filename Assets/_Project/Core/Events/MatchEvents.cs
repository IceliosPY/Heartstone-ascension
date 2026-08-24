using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Events
{
    /// <summary>
    /// Setup finished: both decks are built and shuffled and the starting
    /// player is known. The opening hands are dealt right after this.
    /// </summary>
    public sealed class GameStartedEvent : GameEvent
    {
        public GameStartedEvent(PlayerId startingPlayer, ulong seed)
        {
            StartingPlayer = startingPlayer;
            Seed = seed;
        }

        public PlayerId StartingPlayer { get; }

        /// <summary>Seed the whole match can be reproduced from.</summary>
        public ulong Seed { get; }

        public override string ToString() => "GameStarted(first=" + StartingPlayer + ", seed=" + Seed + ")";
    }

    /// <summary>Opening hands are dealt and both players may now replace cards.</summary>
    public sealed class MulliganStartedEvent : GameEvent
    {
        public override string ToString() => "MulliganStarted";
    }

    /// <summary>One player's mulligan has been carried out.</summary>
    public sealed class MulliganResolvedEvent : GameEvent
    {
        public MulliganResolvedEvent(PlayerId playerId, int replacedCount)
        {
            PlayerId = playerId;
            ReplacedCount = replacedCount;
        }

        public PlayerId PlayerId { get; }

        public int ReplacedCount { get; }

        public override string ToString() => "MulliganResolved(" + PlayerId + ", " + ReplacedCount + ")";
    }

    /// <summary>The match is over. Emitted exactly once.</summary>
    public sealed class GameEndedEvent : GameEvent
    {
        public GameEndedEvent(GameResult result)
        {
            Result = result;
        }

        public GameResult Result { get; }

        /// <summary>The winning player, or None on a draw.</summary>
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

        /// <summary>True when both heroes died in the same death phase.</summary>
        public bool IsDraw => Result == GameResult.Draw;

        public override string ToString() => "GameEnded(" + Result + ")";
    }
}
