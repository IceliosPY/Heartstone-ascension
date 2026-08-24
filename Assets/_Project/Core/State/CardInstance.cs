using CoH.Core.Cards;
using CoH.Core.Identifiers;

namespace CoH.Core.State
{
    /// <summary>
    /// One concrete copy of a card during a match, sitting in a deck, a hand or
    /// a graveyard.
    ///
    /// It holds only what makes this copy different from the printed card. The
    /// definition itself is never touched, so a cost reduction on the copy in
    /// hand leaves every other copy, in this match and any other, untouched.
    /// </summary>
    public sealed class CardInstance : Entity
    {
        internal CardInstance(EntityId id, PlayerId owner, CardId cardId)
            : base(id, owner)
        {
            CardId = cardId;
            Zone = ZoneType.None;
        }

        /// <summary>Which definition this is a copy of.</summary>
        public CardId CardId { get; }

        /// <summary>
        /// Which zone currently holds this card. Kept as a field so the engine
        /// does not have to search every zone to answer the question; keeping
        /// it in sync is the job of whatever moves the card.
        /// </summary>
        public ZoneType Zone { get; internal set; }

        /// <summary>Signed change to the printed mana cost. Negative reduces it.</summary>
        public int CostModifier { get; internal set; }

        /// <summary>Signed change to the printed attack, from buffs applied in hand.</summary>
        public int AttackModifier { get; internal set; }

        /// <summary>Signed change to the printed health, from buffs applied in hand.</summary>
        public int HealthModifier { get; internal set; }

        /// <summary>
        /// Effective mana cost: printed cost plus modifiers.
        ///
        /// Not clamped here. Deciding that a cost can never go below zero is a
        /// game rule and belongs to the rules layer, not to a state object.
        /// </summary>
        public int GetCost(ICardCatalog catalog) => catalog.Get(CardId).ManaCost + CostModifier;

        public override string ToString() => "Card " + CardId + " (" + Id + ", " + Zone + ")";
    }
}
