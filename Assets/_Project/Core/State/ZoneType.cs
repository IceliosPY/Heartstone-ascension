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

        Graveyard = 4
    }
}
