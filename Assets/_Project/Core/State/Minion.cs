using System.Collections.Generic;
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
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        internal Minion(EntityId id, PlayerId owner, CardDefinition definition)
            : base(id, owner)
        {
            CardId = definition.Id;
            BaseAttack = definition.Attack;
            BaseHealth = definition.Health;
            MaxAttacksPerTurn = 1;
            Zone = ZoneType.None;
            Keywords = definition.Keywords;
        }

        public CardId CardId { get; }

        /// <summary>
        /// The standing abilities this minion currently has.
        ///
        /// Copied from the definition when it is created and then owned by the
        /// minion, exactly as its statistics are. Losing stealth by attacking
        /// changes this minion and must not change the card, which every other
        /// copy of it also reads.
        /// </summary>
        public CardKeywords Keywords { get; internal set; }

        public bool HasKeyword(CardKeywords keyword) => Keywords.Has(keyword);

        /// <summary>
        /// Takes a keyword away. Only stealth is ever removed today, when this
        /// minion attacks.
        /// </summary>
        internal void RemoveKeyword(CardKeywords keyword) => Keywords &= ~keyword;

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

        /// <summary>
        /// Everything currently changing this minion's statistics, in the order
        /// it was applied.
        ///
        /// A list rather than two totals, so that what was added can later be
        /// taken away again. Nothing removes one yet; silence and expiring buffs
        /// are what it is here for.
        /// </summary>
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        /// <summary>Total attack change from every modifier.</summary>
        public int AttackModifier { get; private set; }

        /// <summary>Total maximum health change from every modifier.</summary>
        public int HealthModifier { get; private set; }

        /// <summary>
        /// Applies a lasting change and returns it.
        ///
        /// The totals are kept alongside the list because effective attack is
        /// read constantly and the list only ever grows by a handful.
        /// </summary>
        internal StatModifier AddModifier(
            int attackDelta, int healthDelta, ModifierSource source = ModifierSource.Effect)
        {
            StatModifier modifier = new StatModifier(
                _modifiers.Count + 1, attackDelta, healthDelta, source);

            _modifiers.Add(modifier);

            AttackModifier += attackDelta;
            HealthModifier += healthDelta;

            return modifier;
        }

        /// <summary>
        /// Takes one modifier back off, by the order it was applied.
        ///
        /// Nothing calls this yet. It exists because a model that can only ever
        /// add is a model silence and expiring buffs would have to tear down,
        /// and ten lines now is cheaper than that later.
        /// </summary>
        internal bool RemoveModifier(int order)
        {
            for (int index = 0; index < _modifiers.Count; index++)
            {
                if (_modifiers[index].Order != order)
                {
                    continue;
                }

                AttackModifier -= _modifiers[index].AttackDelta;
                HealthModifier -= _modifiers[index].HealthDelta;
                _modifiers.RemoveAt(index);
                return true;
            }

            return false;
        }

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

        /// <summary>True when something has changed this minion's printed statistics.</summary>
        public bool IsModified => _modifiers.Count > 0;

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

        /// <summary>
        /// Whether this minion arrived too recently to act.
        ///
        /// It compares against the match-wide turn counter, not the player's
        /// own: a minion summoned on match turn 5 is free on match turn 7, its
        /// controller's next turn. Turn 6 belongs to the opponent, when the
        /// minion could not act anyway.
        ///
        /// This is the raw state answer only. Whether a sick minion may still
        /// attack, which Charge and Rush change, is a combat rule and is decided
        /// elsewhere.
        /// </summary>
        public bool IsSummoningSick(int currentTurnNumber) => SummonedOnTurn >= currentTurnNumber;

        public override string ToString() =>
            "Minion " + CardId + " (" + Id + ", " + Attack + "/" + CurrentHealth + ")";
    }
}
