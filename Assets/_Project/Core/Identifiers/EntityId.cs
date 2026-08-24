using System;

namespace CoH.Core.Identifiers
{
    /// <summary>
    /// Identifies a single runtime entity (a minion, a hero, a card instance)
    /// for the whole lifetime of a match.
    ///
    /// Values are produced by <see cref="EntityIdGenerator"/> as a simple
    /// incrementing counter. They are deliberately NOT GUIDs: a match must be
    /// reproducible from a seed and a command log, which rules out any source
    /// of randomness or machine state in identifier generation.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        /// <summary>The absence of an entity. This is also default(EntityId).</summary>
        public static readonly EntityId None = default;

        private readonly int _value;

        public EntityId(int value)
        {
            _value = value;
        }

        /// <summary>Raw numeric value. Zero means <see cref="None"/>.</summary>
        public int Value => _value;

        public bool IsNone => _value == 0;

        public bool Equals(EntityId other) => _value == other._value;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => _value;

        public int CompareTo(EntityId other) => _value.CompareTo(other._value);

        public static bool operator ==(EntityId left, EntityId right) => left._value == right._value;

        public static bool operator !=(EntityId left, EntityId right) => left._value != right._value;

        public override string ToString() => IsNone ? "EntityId.None" : "E" + _value.ToString();
    }
}
