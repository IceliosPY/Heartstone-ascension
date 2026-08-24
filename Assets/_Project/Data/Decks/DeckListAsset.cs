using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;
using CoH.Core.Setup;
using UnityEngine;

namespace CoH.Data
{
    /// <summary>
    /// A deck, as authored in Unity: a list of cards and how many of each.
    ///
    /// Deck-building rules are deliberately absent. Nothing here checks two
    /// copies per card, one legendary, or a total of thirty, because none of
    /// that is decided yet and a prototype deck of thirty identical Test
    /// Soldiers is exactly what we want to be able to author.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Deck_NewDeck",
        menuName = "Conquest of Hearthstone/Deck List",
        order = 2)]
    public sealed class DeckListAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private CardDefinitionAsset card;
            [Min(1)]
            [SerializeField] private int count = 1;

            public CardDefinitionAsset Card => card;

            public int Count => count;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>How many cards this deck holds once the counts are expanded.</summary>
        public int TotalCards
        {
            get
            {
                int total = 0;
                for (int index = 0; index < entries.Count; index++)
                {
                    Entry entry = entries[index];
                    if (entry != null && entry.Card != null)
                    {
                        total += Mathf.Max(0, entry.Count);
                    }
                }

                return total;
            }
        }

        /// <summary>
        /// Expands the entries into the flat list of card ids the engine
        /// shuffles. Order carries no meaning: setup shuffles it anyway.
        /// </summary>
        public DeckList BuildRuntimeDeckList()
        {
            List<CardId> cardIds = new List<CardId>(TotalCards);

            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry == null || entry.Card == null)
                {
                    continue;
                }

                for (int copy = 0; copy < entry.Count; copy++)
                {
                    cardIds.Add(entry.Card.Id);
                }
            }

            return new DeckList(cardIds);
        }

        public void Validate(List<string> problems)
        {
            if (entries.Count == 0)
            {
                problems.Add(name + ": the deck is empty.");
            }

            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];

                if (entry == null || entry.Card == null)
                {
                    problems.Add(name + ": entry " + index + " has no card.");
                    continue;
                }

                if (entry.Count < 1)
                {
                    problems.Add(name + ": entry " + index + " (" + entry.Card.RawId + ") has a count below one.");
                }

                if (!entry.Card.Collectible)
                {
                    problems.Add(
                        name + ": \"" + entry.Card.RawId + "\" is not collectible and should not be in a deck.");
                }
            }
        }
    }
}
