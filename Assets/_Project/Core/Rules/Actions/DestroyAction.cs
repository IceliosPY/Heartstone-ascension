using System;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Destroys one or several characters outright, whatever their health.
    ///
    /// Several targets in a single action rather than one action each, so that
    /// a board clear removes everything in the same death phase instead of one
    /// minion at a time.
    ///
    /// Marking rather than removing, exactly like damage: the characters stay
    /// where they are until the next death phase.
    ///
    /// Kept separate from damage because Hearthstone distinguishes the two:
    /// destruction ignores health, and effects that care about damage do not
    /// fire for it.
    /// </summary>
    internal sealed class DestroyAction : ResolutionAction
    {
        private readonly EntityId[] _targetIds;

        public DestroyAction(params EntityId[] targetIds)
        {
            _targetIds = targetIds ?? Array.Empty<EntityId>();
        }

        public override void Resolve(ResolutionContext context)
        {
            for (int index = 0; index < _targetIds.Length; index++)
            {
                Mark(context, _targetIds[index]);
            }
        }

        private static void Mark(ResolutionContext context, EntityId targetId)
        {
            if (!context.State.TryGetEntity(targetId, out Entity target))
            {
                return;
            }

            if (target is Minion minion && minion.IsInPlay)
            {
                minion.IsMarkedForDestruction = true;
                return;
            }

            if (target is Hero hero && !hero.HasDied)
            {
                hero.IsMarkedForDestruction = true;
            }
        }
    }
}
