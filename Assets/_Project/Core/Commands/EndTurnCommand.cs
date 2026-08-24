using CoH.Core.Identifiers;

namespace CoH.Core.Commands
{
    /// <summary>
    /// The active player declares their turn over. The engine ends the turn and
    /// immediately begins the opponent's, as Hearthstone does.
    /// </summary>
    public sealed class EndTurnCommand : GameCommand
    {
        public EndTurnCommand(PlayerId playerId)
            : base(playerId)
        {
        }

        public override string ToString() => "EndTurn(" + PlayerId + ")";
    }
}
