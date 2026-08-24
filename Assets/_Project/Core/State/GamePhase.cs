namespace CoH.Core.State
{
    /// <summary>
    /// Which stage of its life a match is in.
    ///
    /// Made explicit so that impossible states cannot be represented: there is
    /// never a moment where a current player is set but the mulligan is still
    /// pending, and every command can say plainly which phase it belongs to.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>
        /// The state object exists but no deck has been dealt yet. A match
        /// leaves this phase as soon as setup runs.
        /// </summary>
        Setup = 0,

        /// <summary>Both players hold their opening hand and may replace cards.</summary>
        Mulligan = 1,

        /// <summary>Turns are being played.</summary>
        Playing = 2,

        /// <summary>The match is over. No command mutates state any more.</summary>
        Ended = 3
    }
}
