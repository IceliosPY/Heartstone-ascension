namespace CoH.Core.Commands
{
    /// <summary>
    /// Why a command was refused.
    ///
    /// A typed reason rather than a bare boolean, so the presentation layer can
    /// explain the refusal to the player and a server can log what a client
    /// tried to do. Only the reasons the engine can currently produce are
    /// listed; the list grows with the rules.
    /// </summary>
    public enum RejectionReason
    {
        /// <summary>The command was accepted.</summary>
        None = 0,

        /// <summary>The match is not in a phase where this command means anything.</summary>
        WrongPhase = 1,

        /// <summary>The match is over.</summary>
        GameAlreadyEnded = 2,

        /// <summary>The requesting player is not the active player.</summary>
        NotYourTurn = 3,

        /// <summary>This player already submitted their mulligan choice.</summary>
        AlreadyConfirmedMulligan = 4,

        /// <summary>The mulligan listed a card that is not in the player's hand, or listed one twice.</summary>
        InvalidMulliganSelection = 5,

        /// <summary>
        /// The command names no real player. A malformed command has to be
        /// refused rather than throw: on a server, it arrives from a client.
        /// </summary>
        UnknownPlayer = 6,

        /// <summary>No such card in the requesting player's hand.</summary>
        CardNotInHand = 7,

        /// <summary>
        /// The card is of a type the engine cannot play yet. Spells, weapons and
        /// the rest arrive with the effect system; until then only minions can
        /// be played.
        /// </summary>
        CardTypeNotPlayable = 8,

        /// <summary>The player cannot afford the card.</summary>
        NotEnoughMana = 9,

        /// <summary>The board already holds as many minions as it can.</summary>
        BoardFull = 10,

        /// <summary>The requested board slot does not exist.</summary>
        InvalidBoardPosition = 11
    }
}
