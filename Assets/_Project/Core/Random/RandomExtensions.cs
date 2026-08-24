using System;
using System.Collections.Generic;

namespace CoH.Core.Random
{
    /// <summary>
    /// Random operations shared across the engine. The single implementation
    /// of shuffling lives here so no other part of the codebase can invent a
    /// second, differently-behaving one.
    /// </summary>
    public static class RandomExtensions
    {
        /// <summary>
        /// Shuffles a list in place with the Fisher-Yates algorithm, drawing
        /// every index from <paramref name="random"/> only.
        ///
        /// The same source seeded the same way always produces the same order,
        /// which is what lets a deck be shuffled once at setup and a whole
        /// match be replayed later from its seed.
        /// </summary>
        public static void Shuffle<T>(this IRandomSource random, IList<T> items)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            for (int index = items.Count - 1; index > 0; index--)
            {
                int swapWith = random.NextInt(index + 1);
                T held = items[index];
                items[index] = items[swapWith];
                items[swapWith] = held;
            }
        }
    }
}
