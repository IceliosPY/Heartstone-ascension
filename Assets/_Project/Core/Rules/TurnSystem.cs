using System.Collections.Generic;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Starting and ending turns.
    /// </summary>
    internal static class TurnSystem
    {
        /// <summary>
        /// Begins a player's turn.
        ///
        /// Event order is fixed and part of the engine's contract, because the
        /// presentation layer replays it as an animation sequence:
        ///   TurnStarted, then ManaCrystalGained (when a crystal is actually
        ///   gained), then ManaRefilled, then the draw, and finally GameEnded
        ///   if the draw proved lethal.
        /// </summary>
        public static void StartTurn(GameState state, PlayerId playerId, List<GameEvent> events)
        {
            Player player = state.GetPlayer(playerId);

            state.TurnNumber++;
            state.CurrentPlayer = playerId;
            player.TurnsTaken++;

            ResetPerTurnCounters(player);

            events.Add(new TurnStartedEvent(playerId, state.TurnNumber, player.TurnsTaken));

            ManaSystem.StartTurn(player, state.Config, events);

            DrawSystem.Draw(player, events);

            // Fatigue is currently the only thing that can end a match. When
            // the death phase arrives it takes over this responsibility.
            GameEndRules.CheckForGameEnd(state, events);
        }

        /// <summary>
        /// Ends the active player's turn and immediately begins the opponent's,
        /// as Hearthstone does. Nothing happens between the two.
        /// </summary>
        public static void EndTurn(GameState state, List<GameEvent> events)
        {
            PlayerId ending = state.CurrentPlayer;
            events.Add(new TurnEndedEvent(ending, state.TurnNumber));

            if (state.HasEnded)
            {
                return;
            }

            StartTurn(state, ending.Opponent, events);
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
