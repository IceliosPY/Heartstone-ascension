using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// The conditions under which one entry in the catalog applies.
    ///
    /// Every constraint is opt-in. An entry that constrains nothing is the
    /// default for its slot; an entry that constrains type and class is the
    /// picture for that pair. That is the whole override mechanism: you author
    /// the entries that differ and nothing else, and there is no combination
    /// anywhere that has to be enumerated.
    /// </summary>
    [Serializable]
    public struct CardVisualMatch
    {
        public bool constrainType;
        public CardType type;

        public bool constrainClass;
        public CardClass cardClass;

        public bool constrainRarity;
        public Rarity rarity;

        public bool constrainTribe;
        public Tribe tribe;

        [Tooltip("Leave empty to apply to every style.")]
        public CardVisualStyle style;

        /// <summary>
        /// How specific this entry is.
        ///
        /// The weights are the fallback policy, written as numbers. They are
        /// ordered by how much of the card each constraint decides: the type
        /// decides the whole shape of a card, a style decides which set of
        /// pictures it is drawn from, a class recolours it, a rarity changes a
        /// gem and a border, and a tribe changes one plaque. So an entry for
        /// this type outranks an entry for this class, which outranks an entry
        /// for this rarity.
        ///
        /// Every weight is larger than the sum of the weights below it, which is
        /// what makes the order a strict priority rather than a vote: no number
        /// of small constraints can ever outrank one big one.
        /// </summary>
        public int Specificity =>
            (constrainType ? 16 : 0) +
            (style.IsNone ? 0 : 8) +
            (constrainClass ? 4 : 0) +
            (constrainRarity ? 2 : 0) +
            (constrainTribe ? 1 : 0);

        public bool Matches(in CardVisualDescriptor card)
        {
            if (constrainType && card.Type != type)
            {
                return false;
            }

            if (constrainClass && card.Class != cardClass && card.SecondaryClass != cardClass)
            {
                return false;
            }

            if (constrainRarity && card.Rarity != rarity)
            {
                return false;
            }

            if (constrainTribe && card.Tribe != tribe)
            {
                return false;
            }

            return style.IsNone || style.Equals(card.Style);
        }

        public string Describe()
        {
            List<string> parts = new List<string>();

            if (constrainType)
            {
                parts.Add(type.ToString());
            }

            if (constrainClass)
            {
                parts.Add(cardClass.ToString());
            }

            if (constrainRarity)
            {
                parts.Add(rarity.ToString());
            }

            if (constrainTribe)
            {
                parts.Add(tribe.ToString());
            }

            if (!style.IsNone)
            {
                parts.Add("style " + style);
            }

            return parts.Count == 0 ? "any card" : string.Join(" + ", parts);
        }
    }

    /// <summary>One picture, and the cards it applies to.</summary>
    [Serializable]
    public sealed class CardVisualEntry
    {
        public CardVisualSlot slot = CardVisualSlot.Frame;

        public CardVisualMatch match;

        public Sprite sprite;

        [Tooltip("For the inspector and the reports. Nothing reads it.")]
        public string notes = string.Empty;

        public string Describe() => slot + " for " + match.Describe();
    }

    /// <summary>How a lookup ended, which the reports and the preview tool read.</summary>
    public readonly struct CardVisualResolution
    {
        public CardVisualResolution(Sprite sprite, CardVisualEntry entry, bool exact)
        {
            Sprite = sprite;
            Entry = entry;
            IsExact = exact;
        }

        public Sprite Sprite { get; }

        public CardVisualEntry Entry { get; }

        /// <summary>
        /// True when the entry constrained everything the card could be asked
        /// about. False means a fallback was taken, which is fine and worth
        /// being able to see.
        /// </summary>
        public bool IsExact { get; }

        public bool Found => Sprite != null;

        public static CardVisualResolution Missing => default;
    }

    /// <summary>
    /// Every picture a card can be built from, and the rules for choosing
    /// between them.
    ///
    /// A flat table rather than a tree. Asking for a legendary mage minion frame
    /// does not walk a hierarchy; it scores every entry for that slot and takes
    /// the most specific one that applies. Which means:
    ///
    ///   - overriding is authoring a more specific entry, and nothing else;
    ///   - falling back is the more specific entry simply not existing;
    ///   - there is no combination to enumerate, so nothing explodes.
    ///
    /// Two rules keep it honest. Nothing is ever chosen arbitrarily: when no
    /// entry applies the answer is "missing", the layer is skipped, and the
    /// validator names the gap. And when two entries are equally specific the
    /// card's appearance would depend on list order, so that is reported as an
    /// authoring mistake rather than resolved by a coin toss.
    ///
    /// There is no gameplay here. The catalog cannot tell a spell from a minion
    /// except as a value to compare.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardVisualCatalog",
        menuName = "Conquest of Hearthstone/Card Visual Catalog",
        order = 31)]
    public sealed class CardVisualCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<CardVisualEntry> entries = new List<CardVisualEntry>();

        public IReadOnlyList<CardVisualEntry> Entries => entries;

        /// <summary>
        /// The picture for this slot on this card, or nothing.
        ///
        /// Deterministic: the highest specificity wins, and among equals the one
        /// authored first, so the same card always composes to the same picture
        /// whatever order Unity happened to load anything in.
        /// </summary>
        public CardVisualResolution Resolve(CardVisualSlot slot, in CardVisualDescriptor card)
        {
            CardVisualEntry best = null;
            int bestScore = -1;

            for (int index = 0; index < entries.Count; index++)
            {
                CardVisualEntry entry = entries[index];

                if (entry == null || entry.slot != slot || entry.sprite == null)
                {
                    continue;
                }

                if (!entry.match.Matches(card))
                {
                    continue;
                }

                int score = entry.match.Specificity;

                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                return CardVisualResolution.Missing;
            }

            // "Exact" means the entry pinned down everything that could have
            // been asked about this card, rather than being a default that
            // happened to apply.
            bool exact =
                best.match.constrainType &&
                best.match.constrainClass &&
                !best.match.style.IsNone;

            return new CardVisualResolution(best.sprite, best, exact);
        }

        /// <summary>Every slot this catalog has at least one picture for.</summary>
        public void CollectFilledSlots(HashSet<CardVisualSlot> destination)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null && entries[index].sprite != null)
                {
                    destination.Add(entries[index].slot);
                }
            }
        }

        public void Validate(List<string> problems)
        {
            if (problems == null)
            {
                return;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                CardVisualEntry entry = entries[index];

                if (entry == null)
                {
                    problems.Add(name + ": entry " + index + " is empty.");
                    continue;
                }

                if (entry.sprite == null)
                {
                    problems.Add(
                        name + ": '" + entry.Describe() + "' has no sprite, so it will never be chosen. " +
                        "Fill it in or delete the entry.");
                    continue;
                }

                if (entry.slot == CardVisualSlot.None)
                {
                    problems.Add(name + ": entry " + index + " has no slot.");
                }

                if (entry.slot == CardVisualSlot.Artwork)
                {
                    problems.Add(
                        name + ": '" + entry.Describe() + "' puts artwork in the catalog. " +
                        "Artwork belongs to a card, not to a kind of card, and comes from the artwork library.");
                }

                // Ambiguity: same slot, same specificity, and both apply to some
                // card at once. The cheap and sufficient test is an identical
                // match, which is what an accidental duplicate looks like.
                for (int other = index + 1; other < entries.Count; other++)
                {
                    CardVisualEntry rival = entries[other];

                    if (rival == null || rival.slot != entry.slot || rival.sprite == null)
                    {
                        continue;
                    }

                    if (SameConditions(entry.match, rival.match) && rival.sprite != entry.sprite)
                    {
                        problems.Add(
                            name + ": '" + entry.Describe() + "' is authored twice with different " +
                            "sprites, so which one a card gets depends on list order.");
                    }
                }
            }
        }

        private static bool SameConditions(in CardVisualMatch left, in CardVisualMatch right) =>
            left.constrainType == right.constrainType && (!left.constrainType || left.type == right.type) &&
            left.constrainClass == right.constrainClass && (!left.constrainClass || left.cardClass == right.cardClass) &&
            left.constrainRarity == right.constrainRarity && (!left.constrainRarity || left.rarity == right.rarity) &&
            left.constrainTribe == right.constrainTribe && (!left.constrainTribe || left.tribe == right.tribe) &&
            left.style.Equals(right.style);

#if UNITY_EDITOR
        /// <summary>Adds an entry. Editor tooling only; there is no runtime authoring.</summary>
        internal void AddEntry(CardVisualEntry entry)
        {
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        internal void ClearEntries() => entries.Clear();
#endif
    }
}
