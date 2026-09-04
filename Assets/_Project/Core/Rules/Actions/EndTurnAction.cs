using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

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

            // Spell Damage is only ever "this turn" - it must be gone before
            // the opponent's turn starts, not merely by the time this
            // player's own next turn begins (that would leave it active
            // through the whole of the opponent's intervening turn).
            Player player = context.State.GetPlayer(_playerId);
            SpellDamageSystem.ExpireAtEndOfTurn(context, player);

            // Extension point (Phase 11): end-of-turn triggers are queued here,
            // before the next turn starts.

            context.Enqueue(new StartTurnAction(_playerId.Opponent));
        }
    }
}
