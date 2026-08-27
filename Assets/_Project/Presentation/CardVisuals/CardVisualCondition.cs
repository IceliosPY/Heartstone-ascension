using System;
using CoH.Core.Cards;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>Something about a card that a layer can be switched on by.</summary>
    public enum CardVisualField
    {
        CardType = 0,
        CardClass = 1,
        Rarity = 2,
        Tribe = 3,

        /// <summary>True when the card prints an attack and a health.</summary>
        ShowsStatistics = 4,

        /// <summary>True when the card prints a cost.</summary>
        ShowsCost = 5,

        HasTribe = 6,
        HasArtwork = 7,
        HasRulesText = 8,

        /// <summary>True for a legendary, which is the one rarity that changes the frame.</summary>
        IsElite = 9
    }

    public enum CardVisualComparison
    {
        Equals = 0,
        NotEquals = 1,
        AtLeast = 2,
        AtMost = 3
    }

    /// <summary>
    /// One reason a layer appears, written as data.
    ///
    /// The alternative is the chain every card renderer grows: if minion, else
    /// if spell, else if weapon, and inside each of those a second chain for
    /// class and a third for rarity. That chain is unreadable at twenty cards
    /// and unmaintainable at two hundred, and every new frame means editing it.
    /// Here a layer carries its own reasons, an artist adds a layer without
    /// touching code, and the composer never learns what any of them mean.
    ///
    /// Flat rather than polymorphic, for the same reasons the effect system is:
    /// it serialises without custom drawers, it compares cleanly, and the price
    /// is one unused integer.
    /// </summary>
    [Serializable]
    public struct CardVisualCondition
    {
        [Tooltip("What about the card is being looked at.")]
        public CardVisualField field;

        public CardVisualComparison comparison;

        [Tooltip("The value to compare against. For a yes/no field, zero is false and anything else is true.")]
        public int value;

        public CardVisualCondition(CardVisualField field, CardVisualComparison comparison, int value)
        {
            this.field = field;
            this.comparison = comparison;
            this.value = value;
        }

        /// <summary>A field that is simply true, which is most of what gets written.</summary>
        public static CardVisualCondition True(CardVisualField field) =>
            new CardVisualCondition(field, CardVisualComparison.Equals, 1);

        public static CardVisualCondition False(CardVisualField field) =>
            new CardVisualCondition(field, CardVisualComparison.Equals, 0);

        public static CardVisualCondition Is(CardType type) =>
            new CardVisualCondition(CardVisualField.CardType, CardVisualComparison.Equals, (int)type);

        public static CardVisualCondition Is(CardClass cardClass) =>
            new CardVisualCondition(CardVisualField.CardClass, CardVisualComparison.Equals, (int)cardClass);

        public static CardVisualCondition Is(Rarity rarity) =>
            new CardVisualCondition(CardVisualField.Rarity, CardVisualComparison.Equals, (int)rarity);

        public bool Matches(in CardVisualDescriptor card)
        {
            int actual = Read(card);

            switch (comparison)
            {
                case CardVisualComparison.Equals: return actual == value;
                case CardVisualComparison.NotEquals: return actual != value;
                case CardVisualComparison.AtLeast: return actual >= value;
                case CardVisualComparison.AtMost: return actual <= value;
                default: return false;
            }
        }

        private int Read(in CardVisualDescriptor card)
        {
            switch (field)
            {
                case CardVisualField.CardType: return (int)card.Type;
                case CardVisualField.CardClass: return (int)card.Class;
                case CardVisualField.Rarity: return (int)card.Rarity;
                case CardVisualField.Tribe: return (int)card.Tribe;
                case CardVisualField.ShowsStatistics: return card.ShowsStatistics ? 1 : 0;
                case CardVisualField.ShowsCost: return card.ShowsCost ? 1 : 0;
                case CardVisualField.HasTribe: return card.HasTribe ? 1 : 0;
                case CardVisualField.HasArtwork: return card.HasArtwork ? 1 : 0;
                case CardVisualField.HasRulesText: return card.HasRulesText ? 1 : 0;
                case CardVisualField.IsElite: return card.IsElite ? 1 : 0;
                default: return 0;
            }
        }

        /// <summary>True when every condition holds. An empty list always holds.</summary>
        public static bool AllMatch(CardVisualCondition[] conditions, in CardVisualDescriptor card)
        {
            if (conditions == null)
            {
                return true;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                if (!conditions[index].Matches(card))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether these two can never hold at once.
        ///
        /// Two cases, both certain, both about the same field:
        ///
        ///     is a minion   /  is a spell        two values, one field
        ///     is a spell    /  is not a spell    a value and its negation
        ///
        /// A card is one type, so layers carrying either pair never meet and
        /// their order never comes up. Anything subtler is reported as "no",
        /// because a wrong yes here would hide a real ambiguity — and a false
        /// warning costs a moment while a missed one costs a card that looks
        /// different depending on the order a list happens to be in.
        /// </summary>
        public bool ExcludesTheSameCardAs(in CardVisualCondition other)
        {
            if (field != other.field)
            {
                return false;
            }

            bool bothPin =
                comparison == CardVisualComparison.Equals &&
                other.comparison == CardVisualComparison.Equals;

            if (bothPin)
            {
                return value != other.value;
            }

            bool onePinsAndOneRejects =
                (comparison == CardVisualComparison.Equals &&
                 other.comparison == CardVisualComparison.NotEquals) ||
                (comparison == CardVisualComparison.NotEquals &&
                 other.comparison == CardVisualComparison.Equals);

            return onePinsAndOneRejects && value == other.value;
        }

        /// <summary>True when no card could satisfy both lists.</summary>
        public static bool MutuallyExclusive(CardVisualCondition[] left, CardVisualCondition[] right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            for (int a = 0; a < left.Length; a++)
            {
                for (int b = 0; b < right.Length; b++)
                {
                    if (left[a].ExcludesTheSameCardAs(right[b]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public string Describe() => field + " " + comparison + " " + value;

        public override string ToString() => Describe();
    }
}
