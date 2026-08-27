using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>What one particular card looks like, beyond what its rules say.</summary>
    [Serializable]
    public sealed class CardVisualBinding
    {
        [Tooltip("The card this describes, by the id the engine knows it by.")]
        public string cardId = string.Empty;

        [Tooltip("The painting. Everything else about the card's appearance comes from its rules.")]
        public Sprite artwork;

        [Tooltip("Leave empty for the standard style.")]
        public CardVisualStyle style;

        [Tooltip("Set symbol identifier, or empty for none.")]
        public string expansion = string.Empty;

        [Tooltip(
            "What this one card wants done differently from its recipe. Every field of it " +
            "is optional, and a card with none set composes exactly as the recipe says.")]
        public CardVisualOverrides overrides = new CardVisualOverrides();
    }

    /// <summary>
    /// The artwork, and the handful of purely visual choices that belong to one
    /// card rather than to a kind of card.
    ///
    /// This exists so that <c>CoH.Core</c> never learns what a sprite is, and so
    /// that the gameplay authoring in <c>CoH.Data</c> does not either. A card's
    /// rules and a card's painting change for entirely different reasons and by
    /// entirely different people; keeping them in separate assets means
    /// retuning a battlecry cannot touch a picture and repainting a card cannot
    /// touch a rule.
    ///
    /// It maps an id to a painting and nothing else. Type, class, rarity and
    /// tribe are gameplay facts and are read from the card itself, so this file
    /// can never disagree with the engine about what a card is.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardVisualLibrary",
        menuName = "Conquest of Hearthstone/Card Visual Library",
        order = 32)]
    public sealed class CardVisualLibraryAsset : ScriptableObject
    {
        [Tooltip("Used for any card with no entry of its own.")]
        [SerializeField] private Sprite fallbackArtwork;

        [SerializeField] private List<CardVisualBinding> cards = new List<CardVisualBinding>();

        private Dictionary<string, CardVisualBinding> _byId;

        public IReadOnlyList<CardVisualBinding> Cards => cards;

        /// <summary>
        /// The painting for a card, or the fallback.
        ///
        /// A card with no artwork yet is a real situation rather than a mistake,
        /// and it produces a card with a placeholder picture rather than a hole
        /// or an exception.
        /// </summary>
        public Sprite ArtworkFor(CardId id)
        {
            CardVisualBinding binding = Find(id);
            return binding != null && binding.artwork != null ? binding.artwork : fallbackArtwork;
        }

        public CardVisualStyle StyleFor(CardId id)
        {
            CardVisualBinding binding = Find(id);

            return binding == null || binding.style.IsNone
                ? CardVisualStyle.Default
                : binding.style;
        }

        /// <summary>
        /// What one card wants done differently, or null.
        ///
        /// Null rather than an empty object, so that the composer can tell at a
        /// glance that there is nothing to apply — and so that the overwhelming
        /// majority of cards, which want nothing, cost nothing.
        /// </summary>
        public CardVisualOverrides OverridesFor(CardId id)
        {
            CardVisualBinding binding = Find(id);

            return binding == null || binding.overrides == null || binding.overrides.IsEmpty
                ? null
                : binding.overrides;
        }

        public string ExpansionFor(CardId id)
        {
            CardVisualBinding binding = Find(id);
            return binding == null ? string.Empty : binding.expansion ?? string.Empty;
        }

        private CardVisualBinding Find(CardId id)
        {
            if (id.IsNone)
            {
                return null;
            }

            if (_byId == null || _byId.Count != cards.Count)
            {
                Rebuild();
            }

            return _byId.TryGetValue(id.Value, out CardVisualBinding binding) ? binding : null;
        }

        private void Rebuild()
        {
            _byId = new Dictionary<string, CardVisualBinding>(StringComparer.Ordinal);

            for (int index = 0; index < cards.Count; index++)
            {
                CardVisualBinding binding = cards[index];

                if (binding != null && !string.IsNullOrEmpty(binding.cardId))
                {
                    _byId[binding.cardId] = binding;
                }
            }
        }

        private void OnValidate() => _byId = null;

        /// <summary>
        /// Checks the library against a list of the cards that actually exist.
        ///
        /// The interesting failure is an entry for a card nobody has, which is
        /// almost always a renamed card whose artwork silently stopped being
        /// used. A card with no entry is only worth mentioning, not a problem.
        /// </summary>
        public void Validate(ICollection<string> knownCardIds, List<string> problems)
        {
            if (problems == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < cards.Count; index++)
            {
                CardVisualBinding binding = cards[index];

                if (binding == null || string.IsNullOrEmpty(binding.cardId))
                {
                    problems.Add(name + ": entry " + index + " names no card.");
                    continue;
                }

                if (!seen.Add(binding.cardId))
                {
                    problems.Add(name + ": '" + binding.cardId + "' is listed twice.");
                }

                if (knownCardIds != null && !knownCardIds.Contains(binding.cardId))
                {
                    problems.Add(
                        name + ": '" + binding.cardId + "' is not a card in the catalog. " +
                        "It was probably renamed, and its artwork is no longer reaching anything.");
                }
            }
        }

#if UNITY_EDITOR
        internal void Set(string cardId, Sprite artwork)
        {
            for (int index = 0; index < cards.Count; index++)
            {
                if (cards[index] != null && string.Equals(cards[index].cardId, cardId, StringComparison.Ordinal))
                {
                    cards[index].artwork = artwork;
                    _byId = null;
                    return;
                }
            }

            cards.Add(new CardVisualBinding { cardId = cardId, artwork = artwork });
            _byId = null;
        }

        internal void SetFallbackArtwork(Sprite artwork) => fallbackArtwork = artwork;

        /// <summary>
        /// The overrides for a card, made if the card has no entry yet.
        ///
        /// Editor only, and deliberately the only way to get a writable set: a
        /// card acquires an entry here the moment somebody polishes it, and not
        /// before.
        /// </summary>
        internal CardVisualOverrides EstablishOverrides(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            for (int index = 0; index < cards.Count; index++)
            {
                if (cards[index] != null &&
                    string.Equals(cards[index].cardId, cardId, StringComparison.Ordinal))
                {
                    cards[index].overrides ??= new CardVisualOverrides();
                    return cards[index].overrides;
                }
            }

            CardVisualBinding fresh = new CardVisualBinding
            {
                cardId = cardId,
                overrides = new CardVisualOverrides()
            };

            cards.Add(fresh);
            _byId = null;

            return fresh.overrides;
        }
#endif
    }
}
