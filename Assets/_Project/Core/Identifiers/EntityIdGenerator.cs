using System;

namespace CoH.Core.Identifiers
{
    /// <summary>
    /// Hands out <see cref="EntityId"/> values as a plain incrementing counter.
    ///
    /// This is instance state on purpose: there is no static counter anywhere
    /// in the engine. Two matches running side by side, or a match replayed
    /// from a command log, must each produce exactly the same identifiers.
    /// </summary>
    public sealed class EntityIdGenerator
    {
        /// <summary>First value handed out. 0 is reserved for <see cref="EntityId.None"/>.</summary>
        public const int FirstValue = 1;

        private int _nextValue;

        public EntityIdGenerator()
            : this(FirstValue)
        {
        }

        public EntityIdGenerator(int firstValue)
        {
            if (firstValue < FirstValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstValue), firstValue, "0 is reserved for EntityId.None.");
            }

            _nextValue = firstValue;
        }

        /// <summary>Value the next call to <see cref="Next"/> will use, without consuming it.</summary>
        public int NextValue => _nextValue;

        /// <summary>Number of identifiers handed out so far.</summary>
        public int IssuedCount => _nextValue - FirstValue;

        public EntityId Next()
        {
            EntityId id = new EntityId(_nextValue);
            _nextValue++;
            return id;
        }
    }
}
