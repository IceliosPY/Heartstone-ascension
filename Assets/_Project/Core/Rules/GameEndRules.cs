using System.Collections.Generic;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// The win condition check.
    ///
    /// Seam note: today this is called straight after fatigue, which is the
    /// only thing that can currently kill anyone. When the death phase arrives,
    /// it will call this very method at the end of each phase instead. The call
    /// site moves; the rule itself does not get rewritten or duplicated.
    /// </summary>
    internal static class GameEndRules
    {
        /// <summary>
        /// Ends the match if a hero is down. Returns true when the match is
        /// over, whether it just ended or had already ended.
        /// </summary>
        public static bool CheckForGameEnd(GameState state, List<GameEvent> events)
        {
            if (state.Phase == GamePhase.Ended)
            {
                return true;
            }

            bool oneIsDown = state.GetPlayer(PlayerId.One).Hero.CurrentHealth <= 0;
            bool twoIsDown = state.GetPlayer(PlayerId.Two).Hero.CurrentHealth <= 0;

            if (!oneIsDown && !twoIsDown)
            {
                return false;
            }

            state.Phase = GamePhase.Ended;
            state.CurrentPlayer = PlayerId.None;

            if (oneIsDown && twoIsDown)
            {
                // Both heroes going down in the same step is a draw, as in
                // Hearthstone. Fatigue alone cannot cause it, but the rule
                // costs nothing now and would be easy to forget later.
                state.Winner = PlayerId.None;
                events.Add(new GameEndedEvent(PlayerId.None, true));
                return true;
            }

            PlayerId winner = oneIsDown ? PlayerId.Two : PlayerId.One;
            state.Winner = winner;
            events.Add(new GameEndedEvent(winner, false));
            return true;
        }
    }
}
