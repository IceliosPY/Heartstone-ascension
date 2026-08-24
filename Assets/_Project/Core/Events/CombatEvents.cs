using CoH.Core.Identifiers;

namespace CoH.Core.Events
{
    /// <summary>
    /// An attack is happening. Emitted before any damage, so the presentation
    /// can play the lunge and only then show the impacts.
    ///
    /// Nothing here says what the attack will do. The numbers arrive in the
    /// damage events that follow, and whether anything died arrives later still,
    /// from the death phase. Keeping the three apart is what lets the
    /// presentation animate a combat without knowing a single rule.
    /// </summary>
    public sealed class AttackDeclaredEvent : GameEvent
    {
        public AttackDeclaredEvent(PlayerId attackingPlayer, EntityId attackerId, EntityId targetId)
        {
            AttackingPlayer = attackingPlayer;
            AttackerId = attackerId;
            TargetId = targetId;
        }

        public PlayerId AttackingPlayer { get; }

        public EntityId AttackerId { get; }

        public EntityId TargetId { get; }

        public override string ToString() =>
            "AttackDeclared(" + AttackerId + " -> " + TargetId + ")";
    }
}
