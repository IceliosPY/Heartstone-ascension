using CoH.Core.State;

namespace CoH.Core.Rules.Resolution
{
    /// <summary>
    /// Extension point for auras and other continuous modifiers.
    ///
    /// Nothing recalculates anything yet, and there is deliberately no
    /// infrastructure here: auras do not exist. What matters now is that the
    /// pipeline already calls this at the one moment it will need to, right
    /// after a death phase has changed what is on the board.
    ///
    /// When auras arrive they must be recomputed from scratch rather than added
    /// and subtracted as things move, otherwise a minion leaving play at the
    /// wrong moment leaves a permanent buff behind. That is why this is a
    /// "recalculate" hook and not an "apply" one.
    /// </summary>
    internal static class ContinuousEffects
    {
        public static void Recalculate(GameState state)
        {
            // Intentionally empty until Phase 11 introduces auras.
        }
    }
}
