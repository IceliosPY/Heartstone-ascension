using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Data
{
    /// <summary>
    /// The set of cards a match is played with, as authored in Unity.
    ///
    /// Its job is to hand the engine a plain C# catalog and then get out of the
    /// way. The engine holds an ICardCatalog and has no idea an asset was ever
    /// involved, which is what lets the same rules run headless on a server.
    ///
    /// It also stays available on the Unity side as the way to find a card's
    /// artwork from its id, so gameplay data and presentation data can share an
    /// authoring file without the rules ever touching a Sprite.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardCatalog",
        menuName = "Conquest of Hearthstone/Card Catalog",
        order = 1)]
    public sealed class CardCatalogAsset : ScriptableObject
    {
        [Tooltip("Every card definition available to a match.")]
        [SerializeField] private List<CardDefinitionAsset> cards = new List<CardDefinitionAsset>();

        public IReadOnlyList<CardDefinitionAsset> Cards => cards;

        public int Count => cards.Count;

        /// <summary>
        /// Builds the runtime catalog the engine consumes.
        ///
        /// Kept as a method here rather than in a separate builder class: it is
        /// a straight walk of the list, and a class that exists to be called
        /// from exactly one place is ceremony, not architecture.
        /// </summary>
        public CardCatalog BuildRuntimeCatalog()
        {
            List<CardDefinition> definitions = new List<CardDefinition>(cards.Count);

            for (int index = 0; index < cards.Count; index++)
            {
                CardDefinitionAsset asset = cards[index];
                if (asset == null)
                {
                    continue;
                }

                definitions.Add(asset.ToDefinition());
            }

            return new CardCatalog(definitions);
        }

        /// <summary>
        /// Finds the authoring asset behind a card id, which is how the Unity
        /// side will later reach artwork from an event that only carries an id.
        /// </summary>
        public bool TryFindAsset(CardId id, out CardDefinitionAsset asset)
        {
            for (int index = 0; index < cards.Count; index++)
            {
                CardDefinitionAsset candidate = cards[index];
                if (candidate != null && candidate.Id == id)
                {
                    asset = candidate;
                    return true;
                }
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// Appends every problem in this catalog to <paramref name="problems"/>:
        /// each card's own problems, plus the ones only visible from here, which
        /// are empty slots and repeated ids.
        /// </summary>
        public void Validate(List<string> problems)
        {
            if (cards.Count == 0)
            {
                problems.Add(name + ": the catalog is empty.");
            }

            HashSet<string> seenIds = new HashSet<string>();

            for (int index = 0; index < cards.Count; index++)
            {
                CardDefinitionAsset card = cards[index];

                if (card == null)
                {
                    problems.Add(name + ": entry " + index + " is empty.");
                    continue;
                }

                card.Validate(problems);

                string id = card.RawId;
                if (!string.IsNullOrEmpty(id) && !seenIds.Add(id))
                {
                    problems.Add(name + ": the card id \"" + id + "\" appears more than once.");
                }
            }

            ValidateEffectsAcrossCards(problems);
        }

        /// <summary>
        /// The checks a card cannot make on its own.
        ///
        /// An effect that summons something can only be told it names a card
        /// nobody has once every card is known, and a summon that turns out to
        /// name a spell would otherwise fail silently in the middle of a match.
        /// </summary>
        private void ValidateEffectsAcrossCards(List<string> problems)
        {
            Dictionary<string, CardType> known = new Dictionary<string, CardType>(System.StringComparer.Ordinal);

            for (int index = 0; index < cards.Count; index++)
            {
                CardDefinitionAsset card = cards[index];

                if (card != null && !string.IsNullOrEmpty(card.RawId))
                {
                    known[card.RawId] = card.CardType;
                }
            }

            for (int index = 0; index < cards.Count; index++)
            {
                cards[index]?.ValidateAgainstCatalog(known, problems);
            }
        }
    }
}
