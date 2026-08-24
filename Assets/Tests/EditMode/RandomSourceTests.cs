using System;
using System.Collections.Generic;
using CoH.Core.Random;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The random source is the foundation of reproducibility. If two runs of
    /// the same seed ever diverge, reproducible tests, bug replay and future
    /// client/server synchronisation all break at once.
    /// </summary>
    public sealed class RandomSourceTests
    {
        private const int SampleSize = 500;

        private static List<int> Sample(IRandomSource random, int count = SampleSize, int exclusiveMax = 1000)
        {
            List<int> values = new List<int>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(random.NextInt(exclusiveMax));
            }

            return values;
        }

        [Test]
        public void Same_seed_produces_the_same_sequence()
        {
            List<int> left = Sample(new Pcg32Random(42UL));
            List<int> right = Sample(new Pcg32Random(42UL));

            Assert.That(right, Is.EqualTo(left));
        }

        [Test]
        public void Different_seeds_produce_different_sequences()
        {
            List<int> left = Sample(new Pcg32Random(42UL));
            List<int> right = Sample(new Pcg32Random(43UL));

            Assert.That(right, Is.Not.EqualTo(left));
        }

        [Test]
        public void Replaying_a_generator_from_its_seed_reproduces_it_mid_sequence()
        {
            Pcg32Random original = new Pcg32Random(7UL);
            List<int> full = Sample(original, count: 100);

            // A replay starts fresh from the seed and must catch up exactly.
            Pcg32Random replay = new Pcg32Random(7UL);
            List<int> replayed = Sample(replay, count: 100);

            Assert.That(replayed, Is.EqualTo(full));
            Assert.That(replay.State, Is.EqualTo(original.State));
        }

        [Test]
        public void Values_stay_inside_the_requested_range()
        {
            Pcg32Random random = new Pcg32Random(99UL);

            for (int index = 0; index < 5000; index++)
            {
                int value = random.NextInt(7);
                Assert.That(value, Is.InRange(0, 6));
            }
        }

        [Test]
        public void A_bound_of_one_always_returns_zero()
        {
            Pcg32Random random = new Pcg32Random(1UL);

            for (int index = 0; index < 50; index++)
            {
                Assert.That(random.NextInt(1), Is.EqualTo(0));
            }
        }

        [Test]
        public void Every_value_of_a_small_range_eventually_appears()
        {
            Pcg32Random random = new Pcg32Random(2024UL);
            bool[] seen = new bool[6];

            for (int index = 0; index < 2000; index++)
            {
                seen[random.NextInt(6)] = true;
            }

            Assert.That(seen, Is.All.True, "A fair die should show all six faces over 2000 rolls.");
        }

        [Test]
        public void A_non_positive_bound_is_rejected()
        {
            Pcg32Random random = new Pcg32Random(1UL);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-3));
        }

        [Test]
        public void Separate_streams_of_the_same_seed_do_not_correlate()
        {
            List<int> streamOne = Sample(new Pcg32Random(5UL, stream: 1UL));
            List<int> streamTwo = Sample(new Pcg32Random(5UL, stream: 2UL));

            Assert.That(streamTwo, Is.Not.EqualTo(streamOne));
        }
    }
}
