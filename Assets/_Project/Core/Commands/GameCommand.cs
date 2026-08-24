using CoH.Core.Identifiers;

namespace CoH.Core.Commands
{
    /// <summary>
    /// A player's intent, handed to the engine for validation and resolution.
    ///
    /// Commands are plain data referring to entities by id, never by object
    /// reference. That is what will let the exact same object be serialised and
    /// sent to an authoritative server later: a client says what it wants to
    /// do, and the server alone decides whether it happens.
    /// </summary>
    public abstract class GameCommand
    {
        protected GameCommand(PlayerId playerId)
        {
            PlayerId = playerId;
        }

        /// <summary>The player making the request.</summary>
        public PlayerId PlayerId { get; }
    }
}
