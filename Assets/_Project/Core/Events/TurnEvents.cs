using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>A player's turn has begun. Mana and the turn draw follow.</summary>
    public sealed class TurnStartedEvent : GameEvent
    {
        public TurnStartedEvent(PlayerId playerId, int turnNumber, int turnsTakenByPlayer)
        {
            PlayerId = playerId;
            TurnNumber = turnNumber;
            TurnsTakenByPlayer = turnsTakenByPlayer;
        }

        public PlayerId PlayerId { get; }

        /// <summary>Turn count across the whole match.</summary>
        public int TurnNumber { get; }

        /// <summary>How many turns this particular player has now begun.</summary>
        public int TurnsTakenByPlayer { get; }

        public override string ToString() =>
            "TurnStarted(" + PlayerId + ", match turn " + TurnNumber + ")";
    }

    /// <summary>A player's turn is over. The opponent's turn starts immediately after.</summary>
    public sealed class TurnEndedEvent : GameEvent
    {
        public TurnEndedEvent(PlayerId playerId, int turnNumber)
        {
            PlayerId = playerId;
            TurnNumber = turnNumber;
        }

        public PlayerId PlayerId { get; }

        public int TurnNumber { get; }

        public override string ToString() => "TurnEnded(" + PlayerId + ", match turn " + TurnNumber + ")";
    }

    /// <summary>A player gained a mana crystal. Not emitted once the cap is reached.</summary>
    public sealed class ManaCrystalGainedEvent : GameEvent
    {
        public ManaCrystalGainedEvent(PlayerId playerId, int maxMana)
        {
            PlayerId = playerId;
            MaxMana = maxMana;
        }

        public PlayerId PlayerId { get; }

        /// <summary>Crystals owned after the gain.</summary>
        public int MaxMana { get; }

        public override string ToString() => "ManaCrystalGained(" + PlayerId + ", " + MaxMana + ")";
    }

    /// <summary>A player's mana was refilled for the turn.</summary>
    public sealed class ManaRefilledEvent : GameEvent
    {
        public ManaRefilledEvent(PlayerId playerId, int availableMana, int maxMana)
        {
            PlayerId = playerId;
            AvailableMana = availableMana;
            MaxMana = maxMana;
        }

        public PlayerId PlayerId { get; }

        /// <summary>Mana usable this turn, already reduced by any locked crystals.</summary>
        public int AvailableMana { get; }

        public int MaxMana { get; }

        public override string ToString() =>
            "ManaRefilled(" + PlayerId + ", " + AvailableMana + "/" + MaxMana + ")";
    }
}
