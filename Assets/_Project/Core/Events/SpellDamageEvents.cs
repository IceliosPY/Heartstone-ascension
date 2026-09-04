using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>A player gained temporary Spell Damage, lasting the rest of their current turn.</summary>
    public sealed class SpellDamageGrantedEvent : GameEvent
    {
        public SpellDamageGrantedEvent(PlayerId playerId, int amount, int newTotal)
        {
            PlayerId = playerId;
            Amount = amount;
            NewTotal = newTotal;
        }

        public PlayerId PlayerId { get; }

        /// <summary>How much this grant added.</summary>
        public int Amount { get; }

        /// <summary>The player's Spell Damage after this grant.</summary>
        public int NewTotal { get; }

        public override string ToString() =>
            "SpellDamageGranted(" + PlayerId + ", +" + Amount + ", now " + NewTotal + ")";
    }

    /// <summary>A player's temporary Spell Damage ran out at the end of their turn.</summary>
    public sealed class SpellDamageExpiredEvent : GameEvent
    {
        public SpellDamageExpiredEvent(PlayerId playerId)
        {
            PlayerId = playerId;
        }

        public PlayerId PlayerId { get; }

        public override string ToString() => "SpellDamageExpired(" + PlayerId + ")";
    }
}
