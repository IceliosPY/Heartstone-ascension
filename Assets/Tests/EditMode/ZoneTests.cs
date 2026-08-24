using System;
using System.Linq;
using CoH.Core.State;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Zone order is game state, not presentation: board position decides
    /// deathrattle order and where summons appear, and deck order decides what
    /// is drawn next. These tests pin that order down, plus the capacities
    /// that make a hand cap at ten and a board cap at seven.
    /// </summary>
    public sealed class ZoneTests
    {
        private static TestItem Item(string name) => new TestItem(name);

        [Test]
        public void Insertion_order_is_preserved()
        {
            Zone<TestItem> zone = new Zone<TestItem>(ZoneType.Deck);
            TestItem a = Item("a");
            TestItem b = Item("b");
            TestItem c = Item("c");

            zone.TryAdd(a);
            zone.TryAdd(b);
            zone.TryAdd(c);

            Assert.That(zone.Count, Is.EqualTo(3));
            Assert.That(zone[0], Is.SameAs(a));
            Assert.That(zone[1], Is.SameAs(b));
            Assert.That(zone[2], Is.SameAs(c));
            Assert.That(zone.Select(item => item.Name), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void Inserting_at_a_position_shifts_the_rest_right()
        {
            Zone<TestItem> board = new Zone<TestItem>(ZoneType.Play, capacity: 7);
            TestItem left = Item("left");
            TestItem right = Item("right");
            TestItem middle = Item("middle");

            board.TryAdd(left);
            board.TryAdd(right);

            Assert.That(board.TryInsert(1, middle), Is.True);
            Assert.That(board.Select(item => item.Name), Is.EqualTo(new[] { "left", "middle", "right" }));
        }

        [Test]
        public void Inserting_outside_the_valid_range_fails_without_changing_anything()
        {
            Zone<TestItem> zone = new Zone<TestItem>(ZoneType.Play, capacity: 7);
            zone.TryAdd(Item("a"));

            Assert.That(zone.TryInsert(-1, Item("bad")), Is.False);
            Assert.That(zone.TryInsert(5, Item("bad")), Is.False);
            Assert.That(zone.Count, Is.EqualTo(1));
        }

        [Test]
        public void A_zone_with_a_capacity_refuses_extra_items()
        {
            Zone<TestItem> board = new Zone<TestItem>(ZoneType.Play, capacity: 7);

            for (int index = 0; index < 7; index++)
            {
                Assert.That(board.TryAdd(Item("minion" + index)), Is.True);
            }

            Assert.That(board.IsFull, Is.True);
            Assert.That(board.TryAdd(Item("eighth")), Is.False);
            Assert.That(board.Count, Is.EqualTo(7));
        }

        [Test]
        public void A_zone_without_a_capacity_keeps_accepting_items()
        {
            Zone<TestItem> deck = new Zone<TestItem>(ZoneType.Deck);

            for (int index = 0; index < 200; index++)
            {
                Assert.That(deck.TryAdd(Item("card" + index)), Is.True);
            }

            Assert.That(deck.HasCapacityLimit, Is.False);
            Assert.That(deck.IsFull, Is.False);
            Assert.That(deck.Count, Is.EqualTo(200));
        }

        [Test]
        public void The_same_item_cannot_sit_twice_in_a_zone()
        {
            Zone<TestItem> zone = new Zone<TestItem>(ZoneType.Hand, capacity: 10);
            TestItem card = Item("card");

            Assert.That(zone.TryAdd(card), Is.True);
            Assert.That(zone.TryAdd(card), Is.False);
            Assert.That(zone.Count, Is.EqualTo(1));
        }

        [Test]
        public void Distinct_copies_of_the_same_card_can_coexist()
        {
            Zone<TestItem> zone = new Zone<TestItem>(ZoneType.Deck);

            // Same name, two distinct objects: membership is by reference.
            Assert.That(zone.TryAdd(Item("copy")), Is.True);
            Assert.That(zone.TryAdd(Item("copy")), Is.True);
            Assert.That(zone.Count, Is.EqualTo(2));
        }

        [Test]
        public void Removing_closes_the_gap_and_keeps_order()
        {
            Zone<TestItem> zone = new Zone<TestItem>(ZoneType.Play, capacity: 7);
            TestItem a = Item("a");
            TestItem b = Item("b");
            TestItem c = Item("c");
            zone.TryAdd(a);
            zone.TryAdd(b);
            zone.TryAdd(c);

            Assert.That(zone.Remove(b), Is.True);

            Assert.That(zone.Select(item => item.Name), Is.EqualTo(new[] { "a", "c" }));
            Assert.That(zone.IndexOf(c), Is.EqualTo(1));
            Assert.That(zone.Remove(b), Is.False, "Removing twice must not succeed.");
        }

        [Test]
        public void Removing_by_index_returns_the_removed_item()
        {
            Zone<TestItem> zone = new Zone<TestItem>(ZoneType.Deck);
            TestItem top = Item("top");
            zone.TryAdd(top);
            zone.TryAdd(Item("next"));

            Assert.That(zone.RemoveAt(0), Is.SameAs(top));
            Assert.That(zone.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => zone.RemoveAt(5));
        }

        [Test]
        public void Moving_transfers_an_item_between_zones()
        {
            Zone<TestItem> deck = new Zone<TestItem>(ZoneType.Deck);
            Zone<TestItem> hand = new Zone<TestItem>(ZoneType.Hand, capacity: 10);
            TestItem card = Item("card");
            deck.TryAdd(card);

            Assert.That(deck.TryMoveTo(card, hand), Is.True);

            Assert.That(deck.Count, Is.EqualTo(0));
            Assert.That(hand.Count, Is.EqualTo(1));
            Assert.That(hand[0], Is.SameAs(card));
        }

        [Test]
        public void Moving_can_target_an_explicit_position()
        {
            Zone<TestItem> hand = new Zone<TestItem>(ZoneType.Hand, capacity: 10);
            Zone<TestItem> board = new Zone<TestItem>(ZoneType.Play, capacity: 7);
            TestItem played = Item("played");
            hand.TryAdd(played);
            board.TryAdd(Item("left"));
            board.TryAdd(Item("right"));

            Assert.That(hand.TryMoveTo(played, board, 1), Is.True);

            Assert.That(board.Select(item => item.Name), Is.EqualTo(new[] { "left", "played", "right" }));
        }

        [Test]
        public void A_move_into_a_full_zone_leaves_the_source_untouched()
        {
            Zone<TestItem> deck = new Zone<TestItem>(ZoneType.Deck);
            Zone<TestItem> hand = new Zone<TestItem>(ZoneType.Hand, capacity: 2);
            TestItem card = Item("card");
            deck.TryAdd(card);
            hand.TryAdd(Item("a"));
            hand.TryAdd(Item("b"));

            Assert.That(deck.TryMoveTo(card, hand), Is.False);

            // The card must not vanish just because the hand was full.
            Assert.That(deck.Count, Is.EqualTo(1));
            Assert.That(deck[0], Is.SameAs(card));
            Assert.That(hand.Count, Is.EqualTo(2));
        }

        [Test]
        public void Moving_an_item_the_source_does_not_hold_fails()
        {
            Zone<TestItem> deck = new Zone<TestItem>(ZoneType.Deck);
            Zone<TestItem> hand = new Zone<TestItem>(ZoneType.Hand, capacity: 10);

            Assert.That(deck.TryMoveTo(Item("stranger"), hand), Is.False);
            Assert.That(hand.Count, Is.EqualTo(0));
        }

        [Test]
        public void A_zone_reports_where_it_belongs_and_rejects_a_nonsense_capacity()
        {
            Zone<TestItem> hand = new Zone<TestItem>(ZoneType.Hand, capacity: 10);

            Assert.That(hand.Type, Is.EqualTo(ZoneType.Hand));
            Assert.That(hand.Capacity, Is.EqualTo(10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Zone<TestItem>(ZoneType.Hand, capacity: 0));
        }
    }
}
