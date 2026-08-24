using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// A minion was removed from the board by a death phase.
    ///
    /// Carries the board position it occupied because the presentation needs to
    /// know where to play the death animation, and because the position is gone
    /// from the state by the time anyone reads this.
    /// </summary>
    public sealed class MinionDiedEvent : GameEvent
    {
        public MinionDiedEvent(
            PlayerId controller,
            PlayerId owner,
            EntityId minionId,
            CardId cardId,
            int boardPosition)
        {
            Controller = controller;
            Owner = owner;
            MinionId = minionId;
            CardId = cardId;
            BoardPosition = boardPosition;
        }

        /// <summary>Who commanded the minion when it died.</summary>
        public PlayerId Controller { get; }

        /// <summary>Whose it originally was. A stolen minion still goes home to die.</summary>
        public PlayerId Owner { get; }

        public EntityId MinionId { get; }

        public CardId CardId { get; }

        /// <summary>Index it occupied on its controller's board, left to right.</summary>
        public int BoardPosition { get; }

        public override string ToString() =>
            "MinionDied(" + CardId + ", " + MinionId + ", slot " + BoardPosition + ")";
    }

    /// <summary>
    /// A hero went down. The match result is settled right after, once every
    /// death of the phase has been processed, so that two heroes dying together
    /// produce a draw rather than a win for whichever was handled first.
    /// </summary>
    public sealed class HeroDiedEvent : GameEvent
    {
        public HeroDiedEvent(PlayerId playerId, EntityId heroId)
        {
            PlayerId = playerId;
            HeroId = heroId;
        }

        public PlayerId PlayerId { get; }

        public EntityId HeroId { get; }

        public override string ToString() => "HeroDied(" + PlayerId + ")";
    }
}
