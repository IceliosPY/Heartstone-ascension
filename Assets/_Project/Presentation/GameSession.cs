using System;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Server;
using CoH.Core.State;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The one door between the scene and the rules.
    ///
    /// Everything a player does arrives here as a command, goes to the server,
    /// and comes back as an ordered list of events that the presentation queue
    /// replays. No MonoBehaviour anywhere else may touch the game state, and
    /// none can: every mutating member of the state is internal to CoH.Core.
    ///
    /// It also refuses to submit while the queue is still replaying, which is
    /// what stops a player acting on a board that has already moved on.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private PresentationQueue queue;

        private IGameServer _server;

        /// <summary>Raised when the engine refused a command, with the reason why.</summary>
        public event Action<GameCommand, RejectionReason> CommandRejected;

        /// <summary>
        /// Raised for every command that reached the engine, taken or refused.
        ///
        /// This is where a recording comes from. Every player intent already
        /// passes through Submit, so watching it needs nothing added to the
        /// rules: the engine has no idea anyone is listening, which is the only
        /// way a recording can be trusted to describe what actually happened.
        /// </summary>
        public event Action<GameCommand, CommandResult> CommandExecuted;

        public bool IsReady => _server != null;

        /// <summary>Read-only as far as the presentation is concerned.</summary>
        public GameState State => _server.State;

        public PresentationQueue Queue => queue;

        /// <summary>True while events are still being replayed. Input stays locked.</summary>
        public bool IsBusy => queue != null && queue.IsPlaying;

        public void Initialize(IGameServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>
        /// Replaces the server behind this session, for a debug scenario or a
        /// replay. The caller is responsible for rebuilding the presentation
        /// afterwards, because nothing that happened up to now applies any more.
        /// </summary>
        public void Rebind(IGameServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        /// <summary>Asks the engine whether a command would be accepted.</summary>
        public RejectionReason Validate(GameCommand command) =>
            _server == null ? RejectionReason.WrongPhase : _server.CanExecute(command);

        public bool CanSubmit(GameCommand command) => Validate(command) == RejectionReason.None;

        /// <summary>
        /// Sends a command and hands whatever happened to the queue. Returns
        /// whether the engine accepted it.
        /// </summary>
        public bool Submit(GameCommand command)
        {
            if (_server == null || IsBusy)
            {
                return false;
            }

            CommandResult result = _server.Execute(command);

            CommandExecuted?.Invoke(command, result);

            if (!result.IsAccepted)
            {
                CommandRejected?.Invoke(command, result.Reason);
                return false;
            }

            queue.Enqueue(result.Events);
            return true;
        }

        /// <summary>
        /// Everything a minion may attack. The view never works this out for
        /// itself, so a highlighted target is always a legal one.
        /// </summary>
        public IReadOnlyList<EntityId> GetLegalAttackTargets(PlayerId playerId, EntityId attackerId) =>
            _server == null
                ? Array.Empty<EntityId>()
                : _server.GetLegalAttackTargets(playerId, attackerId);

        /// <summary>
        /// Whether playing this card asks the player to point at something.
        ///
        /// Asked of the engine rather than worked out from the card's effects,
        /// so that what the view highlights is exactly what the engine will
        /// accept. The view has no opinion about targeting at all.
        /// </summary>
        public PlayTargetRequirement GetPlayTargetRequirement(PlayerId playerId, EntityId cardInstanceId) =>
            _server == null
                ? PlayTargetRequirement.None
                : _server.GetPlayTargetRequirement(playerId, cardInstanceId);

        /// <summary>Everything this card may legally be aimed at right now.</summary>
        public IReadOnlyList<EntityId> GetLegalPlayTargets(PlayerId playerId, EntityId cardInstanceId) =>
            _server == null
                ? Array.Empty<EntityId>()
                : _server.GetLegalPlayTargets(playerId, cardInstanceId);

        /// <summary>
        /// Whether this card could be played at all, target or no target.
        ///
        /// What a hand should be lit by. Judging a card by a command with no
        /// target in it would dim every targeted card the moment it became
        /// playable, which is exactly when it should light up.
        /// </summary>
        public RejectionReason CanPlayCard(PlayerId playerId, EntityId cardInstanceId) =>
            _server == null
                ? RejectionReason.WrongPhase
                : _server.CanPlayCard(playerId, cardInstanceId);

        /// <summary>
        /// Whether a minion could attack at all, and why not when it could not.
        /// Asked before an attack is aimed, so picking up a minion that has
        /// already swung says so instead of doing nothing.
        /// </summary>
        public RejectionReason CanAttack(PlayerId playerId, EntityId attackerId) =>
            _server == null
                ? RejectionReason.WrongPhase
                : _server.CanAttack(playerId, attackerId);
    }
}
