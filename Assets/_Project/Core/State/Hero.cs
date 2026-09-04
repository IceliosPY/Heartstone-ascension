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

        /// <summary>
        /// The hero power this hero brings to the match, or none.
        ///
        /// A card id, like everything else the engine knows about a card: the
        /// definition behind it lives in the catalog and carries the cost, the
        /// name and the fixed options. Nothing about a particular class is
        /// written here - a hero with no power is simply a hero whose id is
        /// empty, which is every hero the project had before this existed.
        /// </summary>
        public CardId HeroPowerCardId { get; internal set; }

        /// <summary>Whether this hero has a power at all.</summary>
        public bool HasHeroPower => !HeroPowerCardId.IsNone;

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

        /// <summary>
        /// Set by effects that destroy a hero outright. Present for symmetry
        /// with <see cref="Minion"/> so the death phase treats every character
        /// the same way.
        /// </summary>
        public bool IsMarkedForDestruction { get; internal set; }

        /// <summary>
        /// Whether a death phase has already processed this hero's death. Stops
        /// the same hero being reported dead twice.
        /// </summary>
        public bool HasDied { get; internal set; }

        public int MaxHealth => BaseHealth + HealthModifier;

        public int CurrentHealth => MaxHealth - Damage;

        public int Attack => AttackModifier;

        /// <summary>Down but not yet processed by a death phase.</summary>
        public bool IsPendingDeath => !HasDied && (IsMarkedForDestruction || CurrentHealth <= 0);

        public override string ToString() =>
            "Hero " + Id + " (" + CurrentHealth + " hp, " + Armor + " armor)";
    }
}
