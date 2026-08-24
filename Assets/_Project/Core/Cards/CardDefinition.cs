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
            int health = 0)
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
        }

        public CardId Id { get; }

        public string Name { get; }

        public CardType Type { get; }

        public int ManaCost { get; }

        /// <summary>Printed attack. Meaningful for minions and weapons.</summary>
        public int Attack { get; }

        /// <summary>Printed health. Meaningful for minions; durability for weapons.</summary>
        public int Health { get; }

        public override string ToString() =>
            Name + " (" + Id + ", " + Type + " " + ManaCost + " mana)";
    }
}
