using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// A player tried to draw from an empty deck and took fatigue.
    ///
    /// Emitted alongside the <see cref="HeroDamagedEvent"/> it causes: this one
    /// says why, that one says what happened to the hero. Keeping cause and
    /// effect apart lets the presentation show a fatigue visual and a damage
    /// number without either having to infer the other.
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

    /// <summary>A hero took damage.</summary>
    public sealed class HeroDamagedEvent : GameEvent
    {
        public HeroDamagedEvent(
            PlayerId playerId,
            EntityId heroId,
            int amount,
            int absorbedByArmor,
            int remainingHealth)
        {
            PlayerId = playerId;
            HeroId = heroId;
            Amount = amount;
            AbsorbedByArmor = absorbedByArmor;
            RemainingHealth = remainingHealth;
        }

        public PlayerId PlayerId { get; }

        public EntityId HeroId { get; }

        /// <summary>Damage before armour.</summary>
        public int Amount { get; }

        public int AbsorbedByArmor { get; }

        public int RemainingHealth { get; }

        public override string ToString() =>
            "HeroDamaged(" + PlayerId + ", " + Amount + " -> " + RemainingHealth + " hp)";
    }
}
