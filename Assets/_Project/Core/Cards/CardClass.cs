namespace CoH.Core.Cards
{
    /// <summary>
    /// Which class a card belongs to.
    ///
    /// Values are numbered explicitly and Unity stores the number, so adding a
    /// class later leaves every already-authored asset exactly as it was. There
    /// is therefore nothing to gain from inventing the whole roster in advance,
    /// and plenty to lose: names guessed today would have to be corrected
    /// later, and a renumbering would silently change what existing cards
    /// belong to.
    ///
    /// Rule for extending: append new values with new numbers. Never renumber,
    /// never reuse a number, never remove one.
    /// </summary>
    public enum CardClass
    {
        Neutral = 0,

        /// <summary>
        /// The first real class. Added on its own rather than alongside the
        /// rest of the planned roster, for the reason above: a name guessed
        /// today is a serialised mistake tomorrow.
        /// </summary>
        Necromancer = 1,

        /// <summary>The second class. Its hero power, Lunar Phase, is the first to grant a temporary player-level modifier rather than summon or draw.</summary>
        Starcaller = 2
    }
}
