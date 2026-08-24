using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;

namespace CoH.Core.Commands
{
    /// <summary>
    /// A player confirms which opening-hand cards they want replaced. An empty
    /// selection means "keep everything", which is a normal, valid choice.
    ///
    /// Both players submit independently. Nothing is resolved until both have
    /// confirmed, and resolution then runs in a fixed seat order so that the
    /// order in which the two confirmations arrived cannot change the outcome.
    /// </summary>
    public sealed class MulliganCommand : GameCommand
    {
        private readonly EntityId[] _cardsToReplace;

        public MulliganCommand(PlayerId playerId, params EntityId[] cardsToReplace)
            : base(playerId)
        {
            _cardsToReplace = cardsToReplace ?? Array.Empty<EntityId>();
        }

        public MulliganCommand(PlayerId playerId, IEnumerable<EntityId> cardsToReplace)
            : base(playerId)
        {
            _cardsToReplace = cardsToReplace == null
                ? Array.Empty<EntityId>()
                : new List<EntityId>(cardsToReplace).ToArray();
        }

        /// <summary>Cards the player wants to put back, in no particular order.</summary>
        public IReadOnlyList<EntityId> CardsToReplace => _cardsToReplace;

        public override string ToString() =>
            "Mulligan(" + PlayerId + ", " + _cardsToReplace.Length + " replaced)";
    }
}
