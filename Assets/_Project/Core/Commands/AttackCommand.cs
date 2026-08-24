using CoH.Core.Identifiers;

namespace CoH.Core.Commands
{
    /// <summary>
    /// A minion attacks an enemy character.
    ///
    /// Only minions attack for now. A hero attacking depends on weapons and
    /// buffs that do not exist yet, so the engine deliberately refuses it
    /// rather than pretending a hero with no attack could try.
    /// </summary>
    public sealed class AttackCommand : GameCommand
    {
        public AttackCommand(PlayerId playerId, EntityId attackerId, EntityId targetId)
            : base(playerId)
        {
            AttackerId = attackerId;
            TargetId = targetId;
        }

        /// <summary>The minion doing the attacking.</summary>
        public EntityId AttackerId { get; }

        /// <summary>The enemy minion or hero being attacked.</summary>
        public EntityId TargetId { get; }

        public override string ToString() =>
            "Attack(" + PlayerId + ", " + AttackerId + " -> " + TargetId + ")";
    }
}
