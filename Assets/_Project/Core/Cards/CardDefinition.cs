using System;
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
            string text = "")
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
        }

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
