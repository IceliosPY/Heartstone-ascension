using System;
using System.Collections;
using System.Collections.Generic;
using CoH.Core.Random;

namespace CoH.Core.State
{
    /// <summary>
    /// An ordered collection of entities with an optional capacity, used for
    /// the deck, the hand, the board and the graveyard.
    ///
    /// Order is part of the game state, not a presentation detail: board
    /// position decides deathrattle order, adjacency effects and where a
    /// summoned minion appears, and deck order decides what is drawn next.
    /// So this type never sorts or reorders on its own.
    ///
    /// Membership is by reference identity, never by value equality: two
    /// distinct copies of the same card are two distinct entities that must
    /// both be able to sit in the same zone.
    /// </summary>
    public sealed class Zone<T> : IReadOnlyList<T>
        where T : class
    {
        /// <summary>Capacity value meaning "no limit", used by the deck.</summary>
        public const int Unlimited = int.MaxValue;

        private readonly List<T> _items;

        public Zone(ZoneType type, int capacity = Unlimited)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity), capacity, "A zone must be able to hold at least one item.");
            }

            Type = type;
            Capacity = capacity;
            _items = new List<T>(capacity == Unlimited ? 4 : capacity);
        }

        public ZoneType Type { get; }

        public int Capacity { get; }

        public bool HasCapacityLimit => Capacity != Unlimited;

        public int Count => _items.Count;

        public bool IsFull => _items.Count >= Capacity;

        public T this[int index] => _items[index];

        /// <summary>Index of an item, or -1 when absent. Compared by reference.</summary>
        public int IndexOf(T item)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (ReferenceEquals(_items[index], item))
                {
                    return index;
                }
            }

            return -1;
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <summary>Appends at the end. Fails when the zone is full or already holds the item.</summary>
        public bool TryAdd(T item) => TryInsert(_items.Count, item);

        /// <summary>
        /// Inserts at an explicit position, which is how a player chooses where
        /// a minion lands on the board.
        /// </summary>
        public bool TryInsert(int index, T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (index < 0 || index > _items.Count)
            {
                return false;
            }

            if (IsFull || Contains(item))
            {
                return false;
            }

            _items.Insert(index, item);
            return true;
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            _items.RemoveAt(index);
            return true;
        }

        public T RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "No item at that position.");
            }

            T removed = _items[index];
            _items.RemoveAt(index);
            return removed;
        }

        /// <summary>Moves an item to the end of another zone.</summary>
        public bool TryMoveTo(T item, Zone<T> destination) =>
            TryMoveTo(item, destination, destination?.Count ?? 0);

        /// <summary>
        /// Moves an item to an explicit position in another zone. The move is
        /// all-or-nothing: if the destination cannot take the item, the source
        /// is left untouched, so a full hand can never make a card vanish.
        /// </summary>
        public bool TryMoveTo(T item, Zone<T> destination, int index)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (ReferenceEquals(destination, this))
            {
                // Repositioning inside a zone is a different operation and no
                // rule needs it yet.
                return false;
            }

            if (!Contains(item))
            {
                return false;
            }

            if (destination.IsFull || destination.Contains(item))
            {
                return false;
            }

            if (index < 0 || index > destination.Count)
            {
                return false;
            }

            Remove(item);
            destination.TryInsert(index, item);
            return true;
        }

        /// <summary>
        /// Shuffles in place through the engine's random source. Used once at
        /// setup for the deck; draws are then simply taken from the top, which
        /// keeps the whole match reproducible from its seed.
        /// </summary>
        public void Shuffle(IRandomSource random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            random.Shuffle(_items);
        }

        public List<T>.Enumerator GetEnumerator() => _items.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public override string ToString() =>
            Type + "[" + _items.Count + (HasCapacityLimit ? "/" + Capacity : string.Empty) + "]";
    }
}
