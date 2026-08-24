using CoH.Core.Identifiers;

namespace CoH.Core.State
{
    /// <summary>
    /// A player's hero.
    ///
    /// Shares the damage-plus-max-health model with <see cref="Minion"/> for
    /// the same reasons, and adds armour, which absorbs damage before health
    /// and is therefore needed by the damage calculation from the very first
    /// rule that deals damage.
    /// </summary>
    public sealed class Hero : Entity
    {
        internal Hero(EntityId id, PlayerId owner, int baseHealth)
            : base(id, owner)
        {
            BaseHealth = baseHealth;
            MaxAttacksPerTurn = 1;
        }

        public int BaseHealth { get; internal set; }

        public int HealthModifier { get; internal set; }

        public int Damage { get; internal set; }

        /// <summary>Armour absorbs incoming damage before health does.</summary>
        public int Armor { get; internal set; }

        /// <summary>
        /// Attack the hero currently strikes for, granted by a weapon or a
        /// temporary buff. Heroes have no printed attack of their own.
        /// </summary>
        public int AttackModifier { get; internal set; }

        public int AttacksThisTurn { get; internal set; }

        public int MaxAttacksPerTurn { get; internal set; }

        public int MaxHealth => BaseHealth + HealthModifier;

        public int CurrentHealth => MaxHealth - Damage;

        public int Attack => AttackModifier;

        public override string ToString() =>
            "Hero " + Id + " (" + CurrentHealth + " hp, " + Armor + " armor)";
    }
}
