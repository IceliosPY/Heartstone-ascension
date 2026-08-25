using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// A player tried to draw from an empty deck and took fatigue.
    ///
    /// Emitted just before the damage it causes: this one says why, the
    /// following <see cref="DamageDealtEvent"/> says what happened. Keeping
    /// cause and effect apart lets the presentation show a fatigue visual and a
    /// damage number without either having to infer the other.
    /// </summary>
    public sealed class FatigueDamageEvent : GameEvent
    {
        public FatigueDamageEvent(PlayerId playerId, int amount)
        {
            PlayerId = playerId;
            Amount = amount;
        }

        public PlayerId PlayerId { get; }

        /// <summary>Damage dealt, which is the player's fatigue counter after it increased.</summary>
        public int Amount { get; }

        public override string ToString() => "FatigueDamage(" + PlayerId + ", " + Amount + ")";
    }

    /// <summary>
    /// A character took damage.
    ///
    /// One event for heroes and minions alike, because they are the same thing
    /// as far as damage is concerned. Nothing here says whether the target
    /// died: that is decided later, by the death phase, and reported separately.
    /// </summary>
    public sealed class DamageDealtEvent : GameEvent
    {
        public DamageDealtEvent(
            EntityId sourceId,
            EntityId targetId,
            PlayerId targetController,
            int amount,
            int absorbedByArmor,
            int remainingHealth,
            int remainingArmor)
        {
            SourceId = sourceId;
            TargetId = targetId;
            TargetController = targetController;
            Amount = amount;
            AbsorbedByArmor = absorbedByArmor;
            RemainingHealth = remainingHealth;
            RemainingArmor = remainingArmor;
        }

        /// <summary>What dealt the damage, or None when nothing did, as with fatigue.</summary>
        public EntityId SourceId { get; }

        public EntityId TargetId { get; }

        public PlayerId TargetController { get; }

        /// <summary>Damage before armour.</summary>
        public int Amount { get; }

        public int AbsorbedByArmor { get; }

        /// <summary>Health after the hit. May be zero or below; the target is not removed yet.</summary>
        public int RemainingHealth { get; }

        /// <summary>
        /// Armour after the hit, always zero for a minion.
        ///
        /// Here for the same reason as <see cref="RemainingHealth"/>: a reader
        /// showing the hit at the moment it lands needs the numbers as they are
        /// then, and the state has long since moved on. Without it a client
        /// would have to keep a running subtraction of its own, which is a copy
        /// of something the engine already knows.
        /// </summary>
        public int RemainingArmor { get; }

        public override string ToString() =>
            "DamageDealt(" + TargetId + ", " + Amount + " -> " + RemainingHealth + " hp)";
    }
}
