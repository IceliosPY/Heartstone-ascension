using System;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Which family of components a card is composed from.
    ///
    /// A card generator usually offers several complete looks — the modern
    /// frames, the original ones, a diamond treatment — and each is a different
    /// set of pictures for the same slots rather than a different kind of card.
    /// That is exactly what this is: a key the catalog matches on, alongside
    /// type, class and rarity.
    ///
    /// A string rather than an enum, because a style is authored data. Adding
    /// one is dropping sprites into the catalog under a new name, and nothing
    /// in the code needs to learn it exists. The project uses one style today
    /// and the composer neither knows nor cares.
    /// </summary>
    [Serializable]
    public struct CardVisualStyle : IEquatable<CardVisualStyle>
    {
        [SerializeField] private string value;

        public CardVisualStyle(string value) => this.value = value ?? string.Empty;

        /// <summary>The style every card uses unless it says otherwise.</summary>
        public static CardVisualStyle Default => new CardVisualStyle("standard");

        public string Value => string.IsNullOrEmpty(value) ? string.Empty : value;

        public bool IsNone => string.IsNullOrEmpty(value);

        public bool Equals(CardVisualStyle other) =>
            string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => obj is CardVisualStyle other && Equals(other);

        public override int GetHashCode() =>
            Value.ToLowerInvariant().GetHashCode();

        public override string ToString() => IsNone ? "(none)" : Value;
    }
}
