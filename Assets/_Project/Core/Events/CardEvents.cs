using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// A card moved from a deck into a hand.
    ///
    /// <see cref="CardId"/> may be redacted to None by a future view layer when
    /// the event is sent to the player who must not see the card.
    /// </summary>
    public sealed class CardDrawnEvent : GameEvent
    {
        public CardDrawnEvent(PlayerId playerId, EntityId cardInstanceId, CardId cardId, int cardsLeftInDeck)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            CardsLeftInDeck = cardsLeftInDeck;
        }

        public PlayerId PlayerId { get; }

        public EntityId CardInstanceId { get; }

        /// <summary>Which card it is, or None when hidden from the recipient.</summary>
        public CardId CardId { get; }

        public int CardsLeftInDeck { get; }

        public override string ToString() => "CardDrawn(" + PlayerId + ", " + CardId + ")";
    }

    /// <summary>
    /// A card was drawn into a full hand and destroyed instead of being held.
    ///
    /// No CardDrawnEvent is emitted alongside: the card never reached the hand.
    /// If the trigger system later needs "whenever you draw a card" to fire on
    /// an overdrawn card, this is the decision to revisit.
    /// </summary>
    public sealed class CardBurnedEvent : GameEvent
    {
        public CardBurnedEvent(PlayerId playerId, EntityId cardInstanceId, CardId cardId, int cardsLeftInDeck)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
            CardsLeftInDeck = cardsLeftInDeck;
        }

        public PlayerId PlayerId { get; }

        public EntityId CardInstanceId { get; }

        public CardId CardId { get; }

        public int CardsLeftInDeck { get; }

        public override string ToString() => "CardBurned(" + PlayerId + ", " + CardId + ")";
    }

    /// <summary>
    /// A card was created straight into a hand rather than drawn from a deck.
    ///
    /// Today this only happens for the extra card the second player receives,
    /// but the event is deliberately generic: token generation and Discover
    /// will produce exactly the same thing, and the presentation layer decides
    /// what to show from the CardId.
    /// </summary>
    public sealed class CardGeneratedEvent : GameEvent
    {
        public CardGeneratedEvent(PlayerId playerId, EntityId cardInstanceId, CardId cardId)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            CardId = cardId;
        }

        public PlayerId PlayerId { get; }

        public EntityId CardInstanceId { get; }

        public CardId CardId { get; }

        public override string ToString() => "CardGenerated(" + PlayerId + ", " + CardId + ")";
    }
}
