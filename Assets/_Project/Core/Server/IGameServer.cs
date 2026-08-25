using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Server
{
    /// <summary>
    /// What the presentation is allowed to ask of the rules.
    ///
    /// This is the seam that makes a networked match possible later without
    /// touching a single view: today <see cref="LocalGameServer"/> answers by
    /// calling the engine in the same process, tomorrow another implementation
    /// answers over a socket. Nothing above this interface can tell the
    /// difference.
    ///
    /// Note what is missing. There is no way to start a match, and no way to
    /// change anything except by submitting a command. A client asks; it never
    /// declares.
    ///
    /// <see cref="State"/> hands back the real state, which is safe because
    /// every mutating member of GameState is internal to CoH.Core: a view can
    /// read the board, and cannot possibly change it.
    /// </summary>
    public interface IGameServer
    {
        GameState State { get; }

        /// <summary>Validates and resolves a command, returning what happened.</summary>
        CommandResult Execute(GameCommand command);

        /// <summary>Asks whether a command would be accepted, changing nothing.</summary>
        RejectionReason CanExecute(GameCommand command);

        /// <summary>Everything a minion may attack right now.</summary>
        IReadOnlyList<EntityId> GetLegalAttackTargets(PlayerId playerId, EntityId attackerId);

        /// <summary>
        /// Whether a minion is in a state to attack at all, ignoring targets.
        /// None means it can.
        ///
        /// Read-only, and answered by the same rules that would judge the
        /// command itself. A client needs it to know whether picking a minion up
        /// should start an attack, and to say why when it should not; working
        /// that out from summoning turns and attack counters on the client would
        /// be a second copy of a rule that already exists here.
        /// </summary>
        RejectionReason CanAttack(PlayerId playerId, EntityId attackerId);

        /// <summary>
        /// Whether playing this card asks the player to point at something.
        ///
        /// Read only. A client asks rather than reading the card's effects and
        /// inventing a rule of its own, so that what it highlights is exactly
        /// what the engine will accept.
        /// </summary>
        PlayTargetRequirement GetPlayTargetRequirement(PlayerId playerId, EntityId cardInstanceId);

        /// <summary>Everything this card may legally be aimed at right now.</summary>
        IReadOnlyList<EntityId> GetLegalPlayTargets(PlayerId playerId, EntityId cardInstanceId);
    }
}
