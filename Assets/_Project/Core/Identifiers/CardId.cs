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

        /// <summary>
        /// Whether a string is a well-formed card id: lower_snake_case, opening
        /// with a letter, made only of lowercase letters, digits and single
        /// underscores, and not ending on one.
        ///
        /// A card id is a permanent gameplay identity, not a display name and
        /// not an asset file name. Keeping the shape strict means an id can be
        /// typed into a deck list, dropped in a log or put in save data without
        /// anyone wondering about capitalisation or spaces.
        ///
        /// Enforced by the authoring layer's validation rather than by this
        /// type's constructor, so that reading unexpected data never throws:
        /// bad data should be reported, not crash a match.
        /// </summary>
        public static bool IsWellFormed(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            if (value[value.Length - 1] == '_')
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];

                bool isLowerLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isUnderscore = character == '_';

                if (!isLowerLetter && !isDigit && !isUnderscore)
                {
                    return false;
                }

                if (isUnderscore && index > 0 && value[index - 1] == '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether this id is well formed. False for <see cref="None"/>.</summary>
        public bool IsWellFormedId() => IsWellFormed(_value);

        public bool Equals(CardId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CardId other && Equals(other);

        public override int GetHashCode() =>
            _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

        public static bool operator ==(CardId left, CardId right) => left.Equals(right);

        public static bool operator !=(CardId left, CardId right) => !left.Equals(right);

        public override string ToString() => IsNone ? "CardId.None" : _value;
    }
}
