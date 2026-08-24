using System;
using System.Collections.Generic;
using CoH.Core.Commands;
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
    }
}
