using System;

namespace CoH.Core.Identifiers
{
    /// <summary>
    /// Identifies one of the two players of a match.
    ///
    /// The backing value is 0 for <see cref="None"/>, 1 for <see cref="First"/>
    /// and 2 for <see cref="Second"/>, so that default(PlayerId) is safely
    /// <see cref="None"/> rather than silently meaning "first player".
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        /// <summary>No player. This is also default(PlayerId).</summary>
        public static readonly PlayerId None = default;

        /// <summary>The player seated first. Which one starts the match is a rule, decided later.</summary>
        public static readonly PlayerId First = new PlayerId(1);

        /// <summary>The player seated second.</summary>
        public static readonly PlayerId Second = new PlayerId(2);

        private readonly int _number;

        private PlayerId(int number)
        {
            _number = number;
        }

        /// <summary>1 or 2 for a real player, 0 for <see cref="None"/>.</summary>
        public int Number => _number;

        /// <summary>Zero-based index, suitable for array access. -1 for <see cref="None"/>.</summary>
        public int Index => _number - 1;

        public bool IsNone => _number == 0;

        /// <summary>The other player of the match, or <see cref="None"/> if this is None.</summary>
        public PlayerId Opponent
        {
            get
            {
                if (_number == 1)
                {
                    return Second;
                }

                if (_number == 2)
                {
                    return First;
                }

                return None;
            }
        }

        /// <summary>Builds a player id from a zero-based index (0 or 1).</summary>
        public static PlayerId FromIndex(int index)
        {
            if (index != 0 && index != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, "A match has exactly two players, so index must be 0 or 1.");
            }

            return new PlayerId(index + 1);
        }

        public bool Equals(PlayerId other) => _number == other._number;

        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => _number;

        public static bool operator ==(PlayerId left, PlayerId right) => left._number == right._number;

        public static bool operator !=(PlayerId left, PlayerId right) => left._number != right._number;

        public override string ToString() => IsNone ? "PlayerId.None" : "P" + _number.ToString();
    }
}
