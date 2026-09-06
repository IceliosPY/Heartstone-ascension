using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>A character was Frozen, or had its Freeze extended.</summary>
    public sealed class FrozenEvent : GameEvent
    {
        public FrozenEvent(EntityId sourceId, EntityId targetId, PlayerId controller, int frozenUntilTurn)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Controller = controller;
            FrozenUntilTurn = frozenUntilTurn;
        }

        /// <summary>What applied the freeze, or None.</summary>
        public EntityId SourceId { get; }

        public EntityId TargetId { get; }

        public PlayerId Controller { get; }

        /// <summary>The controller's turn this character will thaw at the end of.</summary>
        public int FrozenUntilTurn { get; }

        public override string ToString() =>
            "Frozen(" + TargetId + ", until turn " + FrozenUntilTurn + ")";
    }

    /// <summary>A character's Freeze was explicitly removed at the end of its expiring turn.</summary>
    public sealed class ThawedEvent : GameEvent
    {
        public ThawedEvent(EntityId targetId, PlayerId controller)
        {
            TargetId = targetId;
            Controller = controller;
        }

        public EntityId TargetId { get; }

        public PlayerId Controller { get; }

        public override string ToString() => "Thawed(" + TargetId + ")";
    }
}
