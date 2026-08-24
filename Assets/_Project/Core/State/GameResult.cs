namespace CoH.Core.State
{
    /// <summary>
    /// How a match ended, or that it has not.
    ///
    /// An explicit value rather than a nullable winner, because "no winner" is
    /// ambiguous: it could mean the match is still running or that both heroes
    /// went down together. Those are different outcomes and the engine must
    /// never confuse them.
    /// </summary>
    public enum GameResult
    {
        InProgress = 0,
        PlayerOneWins = 1,
        PlayerTwoWins = 2,

        /// <summary>Both heroes died in the same death phase.</summary>
        Draw = 3
    }
}
