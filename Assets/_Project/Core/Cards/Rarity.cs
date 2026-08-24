namespace CoH.Core.Cards
{
    /// <summary>
    /// How rare a card is.
    ///
    /// Unlike the class roster, these tiers are a fixed, well-known set rather
    /// than something specific to our setting, so all of them are declared now.
    ///
    /// Rarity is gameplay data, not decoration: deck-building limits and, later,
    /// the pools that Discover draws from will read it.
    /// </summary>
    public enum Rarity
    {
        /// <summary>Basic cards and anything the game generates, such as tokens.</summary>
        Free = 0,

        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }
}
