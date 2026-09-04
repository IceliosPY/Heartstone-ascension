using CoH.Core.Identifiers;

namespace CoH.Core.Commands
{
    /// <summary>
    /// A player is using their hero power, having already decided which of its
    /// options they want.
    ///
    /// One command rather than two - activate, then choose - and that is a
    /// deliberate reading of what "committed" means. Nothing is spent and
    /// nothing is remembered until this arrives, so a player who opens the
    /// choice interface and changes their mind has cancelled by not sending
    /// anything. There is no half-used hero power to represent in state, no
    /// pending interaction to clean up when a match is rebuilt from a replay,
    /// and no way for a disconnect to leave a player owing a decision.
    ///
    /// The presentation still asks the engine whether the power could be used
    /// before it offers the menu, so a full board or an empty mana bar is
    /// refused before the player is asked to pick anything.
    /// </summary>
    public sealed class UseHeroPowerCommand : GameCommand
    {
        public UseHeroPowerCommand(PlayerId playerId, int optionIndex = 0)
            : base(playerId)
        {
            OptionIndex = optionIndex;
        }

        /// <summary>
        /// Which of the power's fixed options was chosen, by position in the
        /// authored list.
        ///
        /// An index rather than the chosen card's id, because the list is the
        /// authoritative thing: an index is checked against it in one
        /// comparison, and a client cannot name something that was never on the
        /// menu by inventing an id. Zero for a power that offers only one.
        /// </summary>
        public int OptionIndex { get; }

        public override string ToString() =>
            "UseHeroPower(" + PlayerId + ", option " + OptionIndex + ")";
    }
}
