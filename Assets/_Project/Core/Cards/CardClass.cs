namespace CoH.Core.Cards
{
    /// <summary>
    /// Which class a card belongs to.
    ///
    /// Only Neutral exists for now, and that is deliberate. Values are numbered
    /// explicitly and Unity stores the number, so adding a class later leaves
    /// every already-authored asset exactly as it was. There is therefore
    /// nothing to gain from inventing the whole roster in advance, and plenty
    /// to lose: names guessed today would have to be corrected later, and a
    /// renumbering would silently change what existing cards belong to.
    ///
    /// Rule for extending: append new values with new numbers. Never renumber,
    /// never reuse a number, never remove one.
    /// </summary>
    public enum CardClass
    {
        Neutral = 0
    }
}
