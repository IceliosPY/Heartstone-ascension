using System.Collections.Generic;
using System.Linq;
using CoH.Core.Random;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Shuffling is the first place randomness touches a match. Because decks
    /// are shuffled once at setup and then drawn from the top, getting a
    /// reproducible shuffle is what makes an entire match reproducible.
    /// </summary>
    public sealed class ShuffleTests
    {
        private static List<int> Sequence(int count) => Enumerable.Range(0, count).ToList();

        [Test]
        public void Same_seed_produces_the_same_order()
        {
            List<int> left = Sequence(30);
            List<int> right = Sequence(30);

            new Pcg32Random(2024UL).Shuffle(left);
            new Pcg32Random(2024UL).Shuffle(right);

            Assert.That(right, Is.EqualTo(left));
        }

        [Test]
        public void Different_seeds_produce_different_orders()
        {
            List<int> left = Sequence(30);
            List<int> right = Sequence(30);

            new Pcg32Random(1UL).Shuffle(left);
            new Pcg32Random(2UL).Shuffle(right);

            Assert.That(right, Is.Not.EqualTo(left));
        }

        [Test]
        public void Shuffling_is_a_permutation_and_loses_nothing()
        {
            List<int> deck = Sequence(30);

            new Pcg32Random(77UL).Shuffle(deck);

            Assert.That(deck.Count, Is.EqualTo(30));
            Assert.That(deck.OrderBy(value => value), Is.EqualTo(Sequence(30)));
        }

        [Test]
        public void Shuffling_actually_reorders()
        {
            List<int> deck = Sequence(30);

            new Pcg32Random(3UL).Shuffle(deck);

            Assert.That(deck, Is.Not.EqualTo(Sequence(30)));
        }

        [Test]
        public void Shuffling_a_tiny_collection_is_harmless()
        {
            List<int> empty = new List<int>();
            List<int> single = new List<int> { 42 };

            new Pcg32Random(1UL).Shuffle(empty);
            new Pcg32Random(1UL).Shuffle(single);

            Assert.That(empty, Is.Empty);
            Assert.That(single, Is.EqualTo(new List<int> { 42 }));
        }

        [Test]
        public void Shuffling_a_zone_keeps_its_contents_and_stays_reproducible()
        {
            Zone<TestItem> left = new Zone<TestItem>(ZoneType.Deck);
            Zone<TestItem> right = new Zone<TestItem>(ZoneType.Deck);

            TestItem[] cards = Enumerable.Range(0, 30).Select(i => new TestItem("card" + i)).ToArray();
            foreach (TestItem card in cards)
            {
                left.TryAdd(card);
                right.TryAdd(card);
            }

            left.Shuffle(new Pcg32Random(555UL));
            right.Shuffle(new Pcg32Random(555UL));

            Assert.That(left.Count, Is.EqualTo(30));
            Assert.That(left.Select(item => item.Name), Is.EqualTo(right.Select(item => item.Name)));
            Assert.That(left.OrderBy(item => item.Name), Is.EqualTo(cards.OrderBy(item => item.Name)));
        }
    }
}
