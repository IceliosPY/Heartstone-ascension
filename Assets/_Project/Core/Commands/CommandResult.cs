using System;
using System.Collections.Generic;
using CoH.Core.Events;

namespace CoH.Core.Commands
{
    /// <summary>
    /// What came back from handing a command to the engine.
    ///
    /// On success it carries the ordered list of everything that happened, which
    /// the presentation layer replays as animations. The engine itself never
    /// waits for those animations: it resolves immediately and hands back a
    /// description of the result.
    ///
    /// A rejected command carries no events at all, because a rejected command
    /// changes nothing.
    /// </summary>
    public sealed class CommandResult
    {
        private static readonly GameEvent[] NoEvents = Array.Empty<GameEvent>();

        private CommandResult(bool accepted, RejectionReason reason, IReadOnlyList<GameEvent> events)
        {
            IsAccepted = accepted;
            Reason = reason;
            Events = events;
        }

        public bool IsAccepted { get; }

        /// <summary>Why the command was refused, or None when it was accepted.</summary>
        public RejectionReason Reason { get; }

        /// <summary>Everything that happened, in resolution order. Empty on rejection.</summary>
        public IReadOnlyList<GameEvent> Events { get; }

        public static CommandResult Accepted(IReadOnlyList<GameEvent> events) =>
            new CommandResult(true, RejectionReason.None, events ?? NoEvents);

        public static CommandResult Rejected(RejectionReason reason) =>
            new CommandResult(false, reason, NoEvents);

        public override string ToString() =>
            IsAccepted ? "Accepted (" + Events.Count + " events)" : "Rejected (" + Reason + ")";
    }
}
