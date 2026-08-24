using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Putting a minion into play.
    ///
    /// A helper rather than an action, for the same reason damage is: several
    /// minions summoned by one effect must all arrive within a single action,
    /// before any death phase runs. Playing a card calls this, and so will
    /// token generation and resurrection.
    /// </summary>
    internal static class SummonRules
    {
        /// <summary>Board position meaning "at the right end".</summary>
        public const int Rightmost = -1;

        /// <summary>
        /// Creates the minion, stamps it, and places it on the board.
        ///
        /// Returns null and does nothing when the board is full, which is
        /// checked before anything is created so a refused summon never burns an
        /// entity id or leaves an orphan entity behind.
        /// </summary>
        public static Minion Summon(
            ResolutionContext context,
            Player controller,
            CardId cardId,
            int boardPosition)
        {
            if (controller.Board.IsFull)
            {
                return null;
            }

            GameState state = context.State;

            int position = boardPosition;
            if (position < 0 || position > controller.Board.Count)
            {
                position = controller.Board.Count;
            }

            Minion minion = state.CreateMinion(cardId, controller.Id);

            minion.Zone = ZoneType.Play;

            // Order of entry into play, which decides how simultaneous deaths
            // and, later, deathrattles are sequenced.
            minion.Timestamp = state.NextTimestamp();

            // Recorded so the combat rules can tell it apart from a minion that
            // has been around since an earlier turn.
            minion.SummonedOnTurn = state.TurnNumber;

            controller.Board.TryInsert(position, minion);

            context.Emit(new MinionSummonedEvent(controller.Id, minion.Id, minion.CardId, position));

            return minion;
        }
    }
}
