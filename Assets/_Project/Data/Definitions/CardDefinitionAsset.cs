using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Data
{
    /// <summary>
    /// A card, as authored in the Unity inspector.
    ///
    /// This is the only place a card is written by hand. The engine never sees
    /// this object: <see cref="ToDefinition"/> converts it into the plain C#
    /// CardDefinition that CoH.Core works with, and the conversion is written
    /// out field by field on purpose. No reflection, no runtime serialisation,
    /// nothing that would break under IL2CPP stripping or hide a mistake.
    ///
    /// Artwork lives here and stops here. The engine knows a card by its id and
    /// nothing else, so a Sprite can never reach the rules.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Card_NewCard",
        menuName = "Conquest of Hearthstone/Card Definition",
        order = 0)]
    public sealed class CardDefinitionAsset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Permanent gameplay id, lower_snake_case. Never change it once cards exist in decks.")]
        [SerializeField] private string cardId = string.Empty;

        [Tooltip("Shown to the player. Safe to change at any time.")]
        [SerializeField] private string displayName = string.Empty;

        [Header("Classification")]
        [SerializeField] private CardType cardType = CardType.Minion;
        [SerializeField] private CardClass cardClass = CardClass.Neutral;
        [SerializeField] private Rarity rarity = Rarity.Free;
        [SerializeField] private Tribe tribe = Tribe.None;

        [Tooltip("Whether a player may put this card in a deck. False for The Coin and tokens.")]
        [SerializeField] private bool collectible = true;

        [Header("Statistics")]
        [SerializeField] private int manaCost;
        [SerializeField] private int attack;

        [Tooltip("Health for a minion, durability for a weapon, unused otherwise.")]
        [SerializeField] private int health;

        [Header("Keywords")]
        [Tooltip(
            "Standing abilities printed on the card. Shown to the player as Rush, " +
            "Provocation (Taunt) and Camouflage (Stealth).")]
        [SerializeField] private CardKeywords keywords = CardKeywords.None;

        [Header("Effects")]
        [Tooltip("What this card does, in order. A card with none is a plain body.")]
        [SerializeField] private List<AuthoredEffect> effects = new List<AuthoredEffect>();

        [Header("Presentation")]
        [Tooltip("Shown to the player. Never read by the engine to work out what the card does.")]
        [TextArea(2, 4)]
        [SerializeField] private string rulesText = string.Empty;

        [Tooltip("Never reaches CoH.Core. Only the Unity side reads it.")]
        [SerializeField] private Sprite artwork;

        /// <summary>The gameplay identity of this card.</summary>
        public CardId Id => new CardId(cardId);

        public string RawId => cardId;

        public string DisplayName => displayName;

        public CardType CardType => cardType;

        /// <summary>The standing abilities printed on this card.</summary>
        public CardKeywords Keywords => keywords;

        /// <summary>What this card does, as authored.</summary>
        public IReadOnlyList<AuthoredEffect> Effects => effects;

        public bool Collectible => collectible;

        /// <summary>
        /// The illustration. Deliberately readable only from the Unity side; the
        /// rules layer has no way to reach it.
        /// </summary>
        public Sprite Artwork => artwork;

        /// <summary>
        /// Produces the immutable, engine-facing definition. Everything the
        /// rules need, and nothing that belongs to Unity.
        /// </summary>
        public CardDefinition ToDefinition() =>
            new CardDefinition(
                new CardId(cardId),
                displayName,
                cardType,
                manaCost,
                attack,
                health,
                collectible,
                cardClass,
                rarity,
                tribe,
                rulesText,
                ConvertEffects(),
                keywords);

        /// <summary>
        /// Converts the authored effects in order, and keeps that order. A card
        /// that damages and then draws must do so in that order, so nothing here
        /// sorts, groups or filters.
        /// </summary>
        private EffectDefinition[] ConvertEffects()
        {
            if (effects == null || effects.Count == 0)
            {
                return System.Array.Empty<EffectDefinition>();
            }

            EffectDefinition[] converted = new EffectDefinition[effects.Count];

            for (int index = 0; index < effects.Count; index++)
            {
                converted[index] = effects[index].ToDefinition();
            }

            return converted;
        }

        /// <summary>
        /// Appends everything wrong with this card to <paramref name="problems"/>.
        ///
        /// Deliberately a plain list of readable sentences rather than a
        /// framework of typed diagnostics: a handful of solid checks that print
        /// clearly in the console beats an abstraction nobody reads.
        /// </summary>
        public void Validate(List<string> problems)
        {
            string label = string.IsNullOrEmpty(cardId) ? name : cardId;

            if (string.IsNullOrEmpty(cardId))
            {
                problems.Add(label + ": the card id is empty.");
            }
            else if (!CardId.IsWellFormed(cardId))
            {
                problems.Add(label + ": the card id must be lower_snake_case, for example \"test_soldier\".");
            }

            if (string.IsNullOrEmpty(displayName))
            {
                problems.Add(label + ": the display name is empty.");
            }

            if (cardType == CardType.None)
            {
                problems.Add(label + ": the card has no type.");
            }

            if (manaCost < 0)
            {
                problems.Add(label + ": mana cost cannot be negative (" + manaCost + ").");
            }

            if (attack < 0)
            {
                problems.Add(label + ": attack cannot be negative (" + attack + ").");
            }

            if (cardType == CardType.Minion && health <= 0)
            {
                problems.Add(label + ": a minion needs at least 1 health (" + health + ").");
            }

            if (cardType == CardType.Spell && (attack != 0 || health != 0))
            {
                problems.Add(label + ": a spell should have no attack and no health.");
            }

            if (cardType == CardType.Weapon && health <= 0)
            {
                problems.Add(label + ": a weapon needs at least 1 durability.");
            }

            ValidateEffects(label, problems);
        }

        private void ValidateEffects(string label, List<string> problems)
        {
            if (effects == null)
            {
                return;
            }

            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index] == null)
                {
                    problems.Add(label + " effect [" + index + "]: the entry is empty.");
                    continue;
                }

                effects[index].Validate(label, index, cardType, problems);
            }
        }

        /// <summary>
        /// Checks the effects against the rest of the catalog, which a card
        /// cannot do alone: a summon naming a card nobody has is only visible
        /// once every card is known.
        /// </summary>
        public void ValidateAgainstCatalog(
            IReadOnlyDictionary<string, CardType> knownCards, List<string> problems)
        {
            if (effects == null)
            {
                return;
            }

            string label = string.IsNullOrEmpty(cardId) ? name : cardId;

            for (int index = 0; index < effects.Count; index++)
            {
                effects[index]?.ValidateAgainstCatalog(label, index, knownCards, problems);
            }
        }
    }
}
