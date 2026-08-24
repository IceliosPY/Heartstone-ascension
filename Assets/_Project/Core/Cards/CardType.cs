namespace CoH.Core.Cards
{
    /// <summary>
    /// What a card fundamentally is. This drives where it goes when played
    /// and which runtime entity, if any, it produces.
    /// </summary>
    public enum CardType
    {
        None = 0,
        Minion = 1,
        Spell = 2,
        Weapon = 3,
        Hero = 4,
        HeroPower = 5,
        Location = 6
    }
}
