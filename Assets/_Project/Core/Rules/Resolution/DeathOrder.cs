using System.Collections.Generic;
using CoH.Core.State;

namespace CoH.Core.Rules.Resolution
{
    /// <summary>
    /// The order in which characters dying in the same death phase are
    /// processed.
    ///
    /// This has to be a total order that depends only on game state, never on
    /// how objects happen to sit in memory. Iterating a dictionary or a hash set
    /// would be enough to make two identical matches diverge.
    ///
    /// The comparator, in order:
    ///
    ///   1. Timestamp ascending, so the character that entered play first is
    ///      processed first. This is the order Hearthstone uses for simultaneous
    ///      deaths, and it is the order future deathrattles will fire in.
    ///   2. Controller seat ascending, as a guard in case two characters ever
    ///      share a timestamp.
    ///   3. EntityId ascending, which is unique, so the order is always total
    ///      and never falls through to something unspecified.
    ///
    /// Because rule 3 alone already breaks every tie, sorting is deterministic
    /// even with an unstable sort.
    /// </summary>
    internal static class DeathOrder
    {
        public static readonly IComparer<Entity> Comparer = new EntityDeathComparer();

        private sealed class EntityDeathComparer : IComparer<Entity>
        {
            public int Compare(Entity left, Entity right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                int byTimestamp = left.Timestamp.CompareTo(right.Timestamp);
                if (byTimestamp != 0)
                {
                    return byTimestamp;
                }

                int bySeat = left.Controller.Number.CompareTo(right.Controller.Number);
                if (bySeat != 0)
                {
                    return bySeat;
                }

                return left.Id.CompareTo(right.Id);
            }
        }
    }
}
