namespace CoH.Core.Cards
{
    /// <summary>
    /// The family a minion belongs to, which tribal synergies will read.
    ///
    /// Empty for now on purpose, for the same reason as
    /// <see cref="CardClass"/>: the tribes of our setting are not decided, and
    /// appending values later costs nothing because Unity stores the number.
    ///
    /// Rule for extending: append new values with new numbers. Never renumber.
    /// </summary>
    public enum Tribe
    {
        None = 0
    }
}
