using System;

namespace CoH.Core.Cards
{
    /// <summary>
    /// The standing abilities printed on a card, as a set of flags.
    ///
    /// Flags rather than a list of effects because these are not things that
    /// happen: they are things that are true for as long as the minion is in
    /// play, and every one of them is read by a rule asking a yes-or-no
    /// question in the middle of validating something. A list would be a search
    /// on every attack.
    ///
    /// The numbering is serialised by Unity, so it follows the same rule as
    /// <see cref="CardClass"/>: append, never renumber, never reuse.
    ///
    /// Only the three this project actually implements are listed. Divine
    /// Shield, Windfury and the rest are not here, because a flag nothing reads
    /// is a promise the engine has not made.
    /// </summary>
    [Flags]
    public enum CardKeywords
    {
        None = 0,

        /// <summary>
        /// May attack other minions on the turn it arrives, but not the enemy
        /// hero. Not Charge, which allows both.
        /// </summary>
        Rush = 1 << 0,

        /// <summary>
        /// While this is on the board and able to be attacked, the enemy must
        /// attack it rather than anything else its controller owns.
        ///
        /// Shown to the player as "Provocation".
        /// </summary>
        Taunt = 1 << 1,

        /// <summary>
        /// The opponent cannot pick this as a target, whether to attack it or
        /// to aim an effect at it. It is not immunity: anything that does not
        /// choose a target still reaches it. Lost the moment this minion
        /// attacks.
        ///
        /// Shown to the player as "Camouflage".
        /// </summary>
        Stealth = 1 << 2
    }

    /// <summary>Asking about a keyword set without repeating bit arithmetic.</summary>
    public static class CardKeywordQueries
    {
        public static bool Has(this CardKeywords keywords, CardKeywords wanted) =>
            wanted != CardKeywords.None && (keywords & wanted) == wanted;
    }
}
