using CoH.Core.Identifiers;

namespace CoH.Core.Commands
{
    /// <summary>
    /// A player plays a card from their hand.
    ///
    /// The board position is part of the command, not something the engine
    /// decides afterwards: where a minion lands is a choice the player makes,
    /// and it goes on to decide adjacency effects, death order display and
    /// where future summons appear.
    /// </summary>
    public sealed class PlayCardCommand : GameCommand
    {
        /// <summary>Board position meaning "at the right end of the board".</summary>
        public const int Rightmost = -1;

        public PlayCardCommand(
            PlayerId playerId,
            EntityId cardInstanceId,
            int boardPosition = Rightmost,
            EntityId targetId = default)
            : base(playerId)
        {
            CardInstanceId = cardInstanceId;
            BoardPosition = boardPosition;
            TargetId = targetId;
        }

        /// <summary>Which copy in hand is being played.</summary>
        public EntityId CardInstanceId { get; }

        /// <summary>
        /// Slot the minion should occupy, from 0 for the leftmost. Use
        /// <see cref="Rightmost"/> to append. Ignored for card types that do
        /// not put anything on the board.
        /// </summary>
        public int BoardPosition { get; }

        /// <summary>
        /// What the card is aimed at, or None.
        ///
        /// Carried but not yet used: no card needs a target until the effect
        /// system exists. The field is here so that adding targeted cards later
        /// does not change the shape of a command that clients will already be
        /// sending over a network.
        /// </summary>
        public EntityId TargetId { get; }

        public override string ToString() =>
            "PlayCard(" + PlayerId + ", " + CardInstanceId + ", slot " + BoardPosition + ")";
    }
}
