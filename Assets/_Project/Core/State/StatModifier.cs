namespace CoH.Core.State
{
    /// <summary>
    /// One lasting change to a minion's statistics.
    ///
    /// Kept as a list of individual changes rather than folded into two running
    /// numbers, because almost everything that comes later needs to be able to
    /// find one again: silence removes them all, an expiring buff removes its
    /// own, and an aura will add and remove one as minions move. A pair of
    /// totals can be added to but never unpicked.
    ///
    /// The printed card is never touched. A two for three buffed to a four for
    /// five is still a two for three everywhere else, in this match and every
    /// other, which is the same separation the whole project rests on.
    ///
    /// <see cref="Order"/> is handed out per minion, so a fingerprint has a
    /// stable sequence to describe without anything depending on where objects
    /// happen to sit in memory.
    /// </summary>
    public readonly struct StatModifier
    {
        public StatModifier(int order, int attackDelta, int healthDelta, ModifierSource source)
        {
            Order = order;
            AttackDelta = attackDelta;
            HealthDelta = healthDelta;
            Source = source;
        }

        /// <summary>Position in the order they were applied, from one.</summary>
        public int Order { get; }

        public int AttackDelta { get; }

        /// <summary>
        /// Change to maximum health, never to damage already taken.
        ///
        /// A three health minion with one damage on it, given plus two health,
        /// becomes a five health minion with one damage: four effective, not
        /// three. Storing damage separately is what makes that fall out rather
        /// than needing a rule.
        /// </summary>
        public int HealthDelta { get; }

        public ModifierSource Source { get; }

        public string Describe() =>
            (AttackDelta >= 0 ? "+" : string.Empty) + AttackDelta + "/" +
            (HealthDelta >= 0 ? "+" : string.Empty) + HealthDelta;

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Where a modifier came from.
    ///
    /// One value today. It exists because removing modifiers selectively is the
    /// first thing silence and expiring buffs will need, and adding the field
    /// later would mean revisiting every place one is created.
    /// </summary>
    public enum ModifierSource
    {
        /// <summary>Applied by a card's effect, and lasting until something removes it.</summary>
        Effect = 0
    }
}
