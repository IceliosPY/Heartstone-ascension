using CoH.Core.Identifiers;

namespace CoH.Core.Effects
{
    /// <summary>
    /// The circumstances one effect is resolving in.
    ///
    /// Values only, captured when the effect is queued. That matters most for a
    /// deathrattle: by the time it resolves, the minion it belongs to has been
    /// off the board for several steps, and anything that went looking for it
    /// would find nothing. What the effect needs to know is written down here at
    /// the moment it still could be.
    ///
    /// Deliberately not a window onto the whole match. An effect that wants the
    /// board asks the state through its selector; what belongs here is only what
    /// the state can no longer answer.
    /// </summary>
    public sealed class EffectContext
    {
        public EffectContext(
            EntityId sourceEntityId,
            EntityId sourceCardInstanceId,
            CardId sourceCardId,
            PlayerId owner,
            PlayerId controller,
            EntityId chosenTargetId = default,
            int sourceBoardPosition = -1)
        {
            SourceEntityId = sourceEntityId;
            SourceCardInstanceId = sourceCardInstanceId;
            SourceCardId = sourceCardId;
            Owner = owner;
            Controller = controller;
            ChosenTargetId = chosenTargetId;
            SourceBoardPosition = sourceBoardPosition;
        }

        /// <summary>
        /// The thing the effect belongs to: the minion for a battlecry or a
        /// deathrattle, the card itself for a spell.
        /// </summary>
        public EntityId SourceEntityId { get; }

        /// <summary>The card that was played, when one was.</summary>
        public EntityId SourceCardInstanceId { get; }

        /// <summary>Which card this is, so damage can be attributed to it.</summary>
        public CardId SourceCardId { get; }

        /// <summary>Whose card it originally was.</summary>
        public PlayerId Owner { get; }

        /// <summary>
        /// Who the effect acts for. Friendly and enemy are always measured from
        /// here, never from a seat number, so a stolen minion's deathrattle
        /// helps whoever was commanding it.
        /// </summary>
        public PlayerId Controller { get; }

        /// <summary>What the player pointed at, or None.</summary>
        public EntityId ChosenTargetId { get; }

        /// <summary>
        /// Where the source stood when the effect was queued, or -1.
        ///
        /// Recorded for a deathrattle because the board position is gone from
        /// the state by the time one resolves, and "summon something where this
        /// died" is the first effect that will want it.
        /// </summary>
        public int SourceBoardPosition { get; }

        /// <summary>The player facing <see cref="Controller"/>.</summary>
        public PlayerId Opponent => Controller.Opponent;

        public override string ToString() =>
            SourceCardId.Value + " (" + SourceEntityId + ", controller " + Controller + ")";
    }
}
