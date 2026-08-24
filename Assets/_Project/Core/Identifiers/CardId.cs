using System;

namespace CoH.Core.Identifiers
{
    /// <summary>
    /// Identifies a card definition, for example "test_soldier".
    ///
    /// A string is used rather than a number because card ids are authored by
    /// hand, must stay readable in logs and save data, and must remain stable
    /// across builds even when cards are added or removed.
    ///
    /// Comparison is always ordinal: card ids are technical keys, never
    /// user-facing text, so culture must never influence how they match.
    /// </summary>
    public readonly struct CardId : IEquatable<CardId>
    {
        /// <summary>The absence of a card. This is also default(CardId).</summary>
        public static readonly CardId None = default;

        private readonly string _value;

        public CardId(string value)
        {
            _value = string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>Raw string value, or an empty string for <see cref="None"/>.</summary>
        public string Value => _value ?? string.Empty;

        public bool IsNone => _value == null;

        public bool Equals(CardId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CardId other && Equals(other);

        public override int GetHashCode() =>
            _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

        public static bool operator ==(CardId left, CardId right) => left.Equals(right);

        public static bool operator !=(CardId left, CardId right) => !left.Equals(right);

        public override string ToString() => IsNone ? "CardId.None" : _value;
    }
}
