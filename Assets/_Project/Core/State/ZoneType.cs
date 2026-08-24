namespace CoH.Core.State
{
    /// <summary>
    /// Where something currently sits during a match.
    ///
    /// Modelling zones explicitly, rather than as loose lists, is what will
    /// later make bounce, mill, resurrect and discover uniform: they are all
    /// just a move from one zone to another.
    /// </summary>
    public enum ZoneType
    {
        None = 0,
        Deck = 1,
        Hand = 2,

        /// <summary>On the board, in play.</summary>
        Play = 3,

        Graveyard = 4,

        /// <summary>
        /// Held outside every other zone for the duration of one operation.
        /// Used by the mulligan, where the cards a player replaces must leave
        /// the hand before the replacements are drawn, so that a replaced card
        /// cannot come back as its own replacement.
        /// </summary>
        SetAside = 5
    }
}
