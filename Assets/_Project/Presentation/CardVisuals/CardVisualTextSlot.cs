namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Which of a card's words or numbers a layer prints.
    ///
    /// Separate from <see cref="CardVisualSlot"/> because a text layer never
    /// asks the catalog for anything: its content comes from the card in front
    /// of it, and changes during a match while the pictures do not.
    /// </summary>
    public enum CardVisualTextSlot
    {
        None = 0,
        Name = 1,
        RulesText = 2,
        ManaCost = 3,
        Attack = 4,
        Health = 5,
        Tribe = 6
    }
}
