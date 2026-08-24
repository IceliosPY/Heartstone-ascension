using CoH.Core.Cards;
using CoH.Core.Identifiers;

namespace CoH.Core.State
{
    /// <summary>
    /// A minion in play.
    ///
    /// Deliberately a different entity from the <see cref="CardInstance"/> it
    /// came from: a card in hand and the minion it summons have different
    /// lifetimes, different buffs and different identities, and conflating
    /// them is a classic source of bugs.
    ///
    /// Board position is not stored here. It is the index inside the owning
    /// player's board zone, so there is exactly one source of truth for it.
    /// </summary>
    public sealed class Minion : Entity
    {
        internal Minion(EntityId id, PlayerId owner, CardDefinition definition)
            : base(id, owner)
        {
            CardId = definition.Id;
            BaseAttack = definition.Attack;
            BaseHealth = definition.Health;
            MaxAttacksPerTurn = 1;
            Zone = ZoneType.None;
        }

        public CardId CardId { get; }

        /// <summary>
        /// Where this minion currently is. Play while it is on the board,
        /// Graveyard once a death phase has removed it.
        /// </summary>
        public ZoneType Zone { get; internal set; }

        /// <summary>
        /// Set by effects that destroy a minion outright, whatever its health.
        /// Kept separate from health so that "destroy" and "reduce to zero" stay
        /// distinguishable, which they are in Hearthstone.
        /// </summary>
        public bool IsMarkedForDestruction { get; internal set; }

        /// <summary>Attack copied from the definition at summon time, then possibly overwritten by effects.</summary>
        public int BaseAttack { get; internal set; }

        /// <summary>Health copied from the definition at summon time, then possibly overwritten by effects.</summary>
        public int BaseHealth { get; internal set; }

        public int AttackModifier { get; internal set; }

        public int HealthModifier { get; internal set; }

        /// <summary>
        /// Damage taken so far, stored instead of a "current health" field.
        ///
        /// This is how Hearthstone itself models it, and it is the only way to
        /// get healing and expiring health buffs right: a 2/3 buffed to 2/5,
        /// hurt for 4, then losing the buff has 3 max health and 4 damage, not
        /// some remembered current-health number.
        /// </summary>
        public int Damage { get; internal set; }

        /// <summary>Attacks already made this turn.</summary>
        public int AttacksThisTurn { get; internal set; }

        /// <summary>
        /// How many attacks are allowed per turn. An integer rather than a
        /// "has attacked" flag, so Windfury is a value change instead of a
        /// special case in the combat code.
        /// </summary>
        public int MaxAttacksPerTurn { get; internal set; }

        /// <summary>
        /// Turn number this minion was summoned on, which summoning sickness
        /// will later compare against. Zero means not summoned yet.
        /// </summary>
        public int SummonedOnTurn { get; internal set; }

        /// <summary>
        /// Effective attack. Not clamped: whether a debuffed minion floors at
        /// zero is a rule, and rules do not live in state objects.
        /// </summary>
        public int Attack => BaseAttack + AttackModifier;

        public int MaxHealth => BaseHealth + HealthModifier;

        public int CurrentHealth => MaxHealth - Damage;

        public bool IsDamaged => Damage > 0;

        /// <summary>Still on the board.</summary>
        public bool IsInPlay => Zone == ZoneType.Play;

        /// <summary>
        /// This minion is doomed but has not been removed yet.
        ///
        /// It stays on the board, keeping its position, until the next death
        /// phase. That delay is what makes two minions killing each other work:
        /// both are marked, and neither disappears in the middle of the action
        /// that killed them.
        ///
        /// Health is read here rather than latched at damage time on purpose:
        /// a minion healed back above zero before the death phase runs is not
        /// doomed any more.
        /// </summary>
        public bool IsPendingDeath => IsInPlay && (IsMarkedForDestruction || CurrentHealth <= 0);

        public override string ToString() =>
            "Minion " + CardId + " (" + Id + ", " + Attack + "/" + CurrentHealth + ")";
    }
}
