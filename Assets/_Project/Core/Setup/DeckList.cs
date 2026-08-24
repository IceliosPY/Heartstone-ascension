using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;

namespace CoH.Core.Setup
{
    /// <summary>
    /// The cards a player brings to a match, as an ordered list of ids.
    ///
    /// Order carries no meaning: the deck is shuffled at setup. Holding ids
    /// rather than definitions keeps deck lists serialisable and lets the same
    /// list be used against any catalog.
    /// </summary>
    public sealed class DeckList
    {
        private readonly CardId[] _cards;

        public DeckList(IEnumerable<CardId> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            List<CardId> collected = new List<CardId>(cards);

            for (int index = 0; index < collected.Count; index++)
            {
                if (collected[index].IsNone)
                {
                    throw new ArgumentException("A deck list cannot contain an empty card id.", nameof(cards));
                }
            }

            _cards = collected.ToArray();
        }

        public IReadOnlyList<CardId> Cards => _cards;

        public int Count => _cards.Length;

        public override string ToString() => "DeckList(" + _cards.Length + " cards)";
    }
}
