using System;

namespace CoH.Core.Random
{
    /// <summary>
    /// PCG-XSH-RR 32-bit generator (O'Neill, 2014).
    ///
    /// Written by hand rather than reusing System.Random because the engine
    /// needs bit-for-bit identical output everywhere it runs: Mono in the
    /// editor, IL2CPP in a desktop build, IL2CPP/WebAssembly in a browser,
    /// and .NET on a future authoritative server. System.Random makes no such
    /// guarantee across runtimes and versions.
    ///
    /// The whole implementation uses only ulong and uint arithmetic, which is
    /// exactly specified by the C# language, so every platform produces the
    /// same sequence for the same seed.
    /// </summary>
    public sealed class Pcg32Random : IRandomSource
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong DefaultStream = 1442695040888963407UL;

        private readonly ulong _increment;
        private ulong _state;

        /// <param name="seed">Match seed. The same seed always yields the same sequence.</param>
        /// <param name="stream">
        /// Selects one of the generator's independent streams. Two generators
        /// sharing a seed but using different streams produce unrelated
        /// sequences, which is useful if we ever need separate streams for
        /// shuffling and for in-game random effects.
        /// </param>
        public Pcg32Random(ulong seed, ulong stream = DefaultStream)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt32();
            _state = unchecked(_state + seed);
            NextUInt32();
        }

        /// <summary>
        /// Current internal state. Exposed so a match can later be snapshotted
        /// and resumed, or compared between a client and a server.
        /// </summary>
        public ulong State => _state;

        /// <summary>
        /// How many raw values have been drawn. Diagnostics only.
        ///
        /// Counted here rather than derived from the state, because a step
        /// count is what a divergence report can talk about without knowing
        /// anything about how this generator works.
        /// </summary>
        public long DrawCount { get; private set; }

        /// <summary>Raw 32-bit output. Advances the generator by one step.</summary>
        public uint NextUInt32()
        {
            DrawCount++;

            ulong previousState = _state;
            _state = unchecked(previousState * Multiplier + _increment);

            uint xorShifted = (uint)(((previousState >> 18) ^ previousState) >> 27);
            int rotation = (int)(previousState >> 59);

            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <inheritdoc />
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveMax), exclusiveMax, "Upper bound must be strictly positive.");
            }

            uint bound = (uint)exclusiveMax;

            // Rejection sampling. Simply taking NextUInt32() % bound would make
            // the low values slightly more likely whenever bound is not a power
            // of two; discarding the unbalanced tail keeps the distribution
            // uniform, which matters for anything a player could notice such as
            // Discover or random targeting.
            uint threshold = unchecked(0u - bound) % bound;

            while (true)
            {
                uint value = NextUInt32();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }
    }
}
