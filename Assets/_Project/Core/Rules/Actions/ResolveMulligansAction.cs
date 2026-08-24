using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Carries out both mulligans once both players have confirmed, hands the
    /// second player their extra card, and queues the first turn.
    ///
    /// Queued as an action rather than run inline so that the first turn starts
    /// through the same pipeline as every other turn.
    /// </summary>
    internal sealed class ResolveMulligansAction : ResolutionAction
    {
        public override void Resolve(ResolutionContext context)
        {
            MulliganSystem.ResolveAll(context);

            context.State.Phase = GamePhase.Playing;
            context.Enqueue(new StartTurnAction(context.State.StartingPlayer));
        }
    }
}
