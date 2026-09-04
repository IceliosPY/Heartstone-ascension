using System;
using System.Collections.Generic;
using CoH.Core.Effects;
using CoH.Core.Identifiers;

namespace CoH.Core.Cards
{
    /// <summary>
    /// The immutable, original data of a card: what it says on the printed
    /// card, before anything that happens during a match.
    ///
    /// A definition is shared by every copy of that card in every match, so it
    /// must never be mutated. Buffs, damage, cost reductions and silences all
    /// live on the runtime instance instead (CardInstance, Minion), never
    /// here. That split is what allows a 2 mana 2/3 to be a 1 mana 4/5 on the
    /// board while the card itself is still a 2 mana 2/3 everywhere else.
    ///
    /// Presentation data (artwork, frame, VFX) is deliberately absent: the
    /// engine only ever knows a card by its <see cref="Id"/>, and the Unity
    /// layer maps that id to visuals on its own side.
    /// </summary>
    public sealed class CardDefinition
    {
        public CardDefinition(
            CardId id,
            string name,
            CardType type,
            int manaCost,
            int attack = 0,
            int health = 0,
            bool collectible = true,
            CardClass cardClass = CardClass.Neutral,
            Rarity rarity = Rarity.Free,
            Tribe tribe = Tribe.None,
            string text = "",
            IReadOnlyList<EffectDefinition> effects = null,
            CardKeywords keywords = CardKeywords.None)
        {
            if (id.IsNone)
            {
                throw new ArgumentException("A card definition needs a non-empty id.", nameof(id));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Id = id;
            Name = name;
            Type = type;
            ManaCost = manaCost;
            Attack = attack;
            Health = health;
            Collectible = collectible;
            Class = cardClass;
            Rarity = rarity;
            Tribe = tribe;
            Text = text ?? string.Empty;
            Keywords = keywords;

            Effects = effects == null || effects.Count == 0
                ? NoEffects
                : new List<EffectDefinition>(effects).ToArray();
        }

        private static readonly EffectDefinition[] NoEffects = Array.Empty<EffectDefinition>();

        /// <summary>
        /// What this card does, in the order it was written.
        ///
        /// A card with none is a plain body, exactly as every card was before
        /// this existed. Order is kept because a card that damages and then
        /// draws must do so in that order, and nothing anywhere sorts or groups
        /// this list.
        /// </summary>
        public IReadOnlyList<EffectDefinition> Effects { get; }

        /// <summary>True when this card does something beyond being a body.</summary>
        public bool HasEffects => Effects.Count > 0;

        /// <summary>
        /// The standing abilities printed on the card.
        ///
        /// Separate from <see cref="Effects"/> because they are a different
        /// kind of thing. An effect is something that happens at a moment; a
        /// keyword is something that is true for as long as the minion is
        /// there, and is read by rules deciding legality rather than by
        /// anything that resolves.
        /// </summary>
        public CardKeywords Keywords { get; }

        public CardId Id { get; }

        public string Name { get; }

        public CardType Type { get; }

        public int ManaCost { get; }

        /// <summary>Printed attack. Meaningful for minions and weapons.</summary>
        public int Attack { get; }

        /// <summary>Printed health. Meaningful for minions; durability for weapons.</summary>
        public int Health { get; }

        /// <summary>
        /// Whether the card can be put in a deck by a player. False for cards
        /// that only ever appear through the game itself, such as The Coin or
        /// summoned tokens.
        /// </summary>
        public bool Collectible { get; }

        public CardClass Class { get; }

        public Rarity Rarity { get; }

        /// <summary>Minion family, for tribal synergies. None for anything else.</summary>
        public Tribe Tribe { get; }

        /// <summary>
        /// The rules text shown to the player.
        ///
        /// Written for a human and never read by the engine. What a card does
        /// will come from structured effect data; nothing anywhere parses this
        /// string to work out behaviour, and nothing ever should.
        /// </summary>
        public string Text { get; }

        public override string ToString() =>
            Name + " (" + Id + ", " + Type + " " + ManaCost + " mana)";
    }
}
