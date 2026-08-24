using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Ends the active player's turn and queues the opponent's.
    ///
    /// The two are separate actions rather than one method, so that a death
    /// phase runs between them. Nothing can currently die at the end of a turn,
    /// but end-of-turn effects will, and they must be resolved before the next
    /// turn begins rather than during it.
    /// </summary>
    internal sealed class EndTurnAction : ResolutionAction
    {
        private readonly PlayerId _playerId;

        public EndTurnAction(PlayerId playerId)
        {
            _playerId = playerId;
        }

        public override void Resolve(ResolutionContext context)
        {
            context.Emit(new TurnEndedEvent(_playerId, context.State.TurnNumber));

            // Extension point (Phase 11): end-of-turn triggers are queued here,
            // before the next turn starts.

            context.Enqueue(new StartTurnAction(_playerId.Opponent));
        }
    }
}
