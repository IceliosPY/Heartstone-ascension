using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Begins a player's turn.
    ///
    /// Event order is part of the engine's contract, because the presentation
    /// layer replays it as an animation sequence: TurnStarted, then
    /// ManaCrystalGained when a crystal is actually gained, then ManaRefilled,
    /// then the draw. Anything lethal that the draw causes is reported by the
    /// death phase that follows this action, not from inside it.
    /// </summary>
    internal sealed class StartTurnAction : ResolutionAction
    {
        private readonly PlayerId _playerId;

        public StartTurnAction(PlayerId playerId)
        {
            _playerId = playerId;
        }

        public override void Resolve(ResolutionContext context)
        {
            GameState state = context.State;
            Player player = state.GetPlayer(_playerId);

            state.TurnNumber++;
            state.CurrentPlayer = _playerId;
            player.TurnsTaken++;

            ResetPerTurnCounters(player);

            context.Emit(new TurnStartedEvent(_playerId, state.TurnNumber, player.TurnsTaken));

            ManaSystem.StartTurn(player, state.Config, context);
            DrawSystem.Draw(context, player);
        }

        private static void ResetPerTurnCounters(Player player)
        {
            player.HasUsedHeroPowerThisTurn = false;
            player.Hero.AttacksThisTurn = 0;

            for (int index = 0; index < player.Board.Count; index++)
            {
                player.Board[index].AttacksThisTurn = 0;
            }
        }
    }
}
