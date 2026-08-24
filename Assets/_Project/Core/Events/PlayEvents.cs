using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// Mana was paid. Only emitted when something was actually spent, so a free
    /// card produces no mana event at all.
    /// </summary>
    public sealed class ManaSpentEvent : GameEvent
    {
        public ManaSpentEvent(PlayerId playerId, int amount, int remainingMana)
        {
            PlayerId = playerId;
            Amount = amount;
            RemainingMana = remainingMana;
        }

        public PlayerId PlayerId { get; }

        public int Amount { get; }

        public int RemainingMana { get; }

        public override string ToString() =>
            "ManaSpent(" + PlayerId + ", " + Amount + ", " + RemainingMana + " left)";
    }

    /// <summary>
    /// A card left a hand because its owner played it.
    ///
    /// Separate from whatever the card then does. A minion card produces this
    /// and then a <see cref="MinionSummonedEvent"/>; a spell will produce this
    /// and then its effects. The presentation animates the card leaving the
    /// hand from here, whatever kind of card it was.
    /// </summary>
    public sealed class CardPlayedEvent : GameEvent
    {
        public CardPlayedEvent(PlayerId playerId, EntityId cardInstanceId, CardId cardId, EntityId targetId)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            TargetId = targetId;
        }

        public PlayerId PlayerId { get; }

        public EntityId CardInstanceId { get; }

        public CardId CardId { get; }

        /// <summary>What the card was aimed at, or None.</summary>
        public EntityId TargetId { get; }

        public override string ToString() => "CardPlayed(" + PlayerId + ", " + CardId + ")";
    }

    /// <summary>
    /// A minion entered play.
    ///
    /// Deliberately distinct from <see cref="CardPlayedEvent"/>, because the two
    /// do not always come together: a token summoned by an effect is summoned
    /// without any card being played, and a spell is played without summoning
    /// anything.
    /// </summary>
    public sealed class MinionSummonedEvent : GameEvent
    {
        public MinionSummonedEvent(PlayerId controller, EntityId minionId, CardId cardId, int boardPosition)
        {
            Controller = controller;
            MinionId = minionId;
            CardId = cardId;
            BoardPosition = boardPosition;
        }

        public PlayerId Controller { get; }

        public EntityId MinionId { get; }

        public CardId CardId { get; }

        /// <summary>Slot it landed on, left to right.</summary>
        public int BoardPosition { get; }

        public override string ToString() =>
            "MinionSummoned(" + Controller + ", " + CardId + ", slot " + BoardPosition + ")";
    }
}
