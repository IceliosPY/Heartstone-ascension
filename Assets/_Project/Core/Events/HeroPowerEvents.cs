using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// A player used their hero power, and which of its options they took.
    ///
    /// Emitted before whatever the option does, in the same way a card being
    /// played is reported before the minion it summons: the presentation shows
    /// the power firing and then shows the result of it, and the two are
    /// separate moments on screen.
    /// </summary>
    public sealed class HeroPowerUsedEvent : GameEvent
    {
        public HeroPowerUsedEvent(
            PlayerId playerId, CardId heroPowerCardId, int optionIndex, int remainingMana)
        {
            PlayerId = playerId;
            HeroPowerCardId = heroPowerCardId;
            OptionIndex = optionIndex;
            RemainingMana = remainingMana;
        }

        public PlayerId PlayerId { get; }

        public CardId HeroPowerCardId { get; }

        /// <summary>Which fixed option was taken, by position in the authored list.</summary>
        public int OptionIndex { get; }

        public int RemainingMana { get; }

        public override string ToString() =>
            "HeroPowerUsed(" + PlayerId + ", " + HeroPowerCardId +
            ", option " + OptionIndex + ", " + RemainingMana + " mana left)";
    }
}
