using System;
using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;

namespace CoH.Core.Diagnostics
{
    /// <summary>Which command a replay entry holds.</summary>
    public enum ReplayCommandKind
    {
        Unknown = 0,
        Mulligan = 1,
        EndTurn = 2,
        PlayCard = 3,
        Attack = 4
    }

    /// <summary>
    /// A submitted command, as values.
    ///
    /// Never a reference to the command object, and never a reference to
    /// anything in the match it came from. A replay has to survive the session
    /// that produced it being torn down, written to a file and read back a week
    /// later, so everything here is an id or a number.
    ///
    /// Converting both ways is written out by hand. Reflection would turn every
    /// future field of a command into a silent change of replay format, and a
    /// replay format that changes without anyone deciding to is worse than no
    /// replay at all.
    /// </summary>
    public sealed class ReplayCommand
    {
        private static readonly EntityId[] NoCards = Array.Empty<EntityId>();

        public ReplayCommand(
            ReplayCommandKind kind,
            PlayerId playerId,
            EntityId cardInstanceId = default,
            int boardPosition = 0,
            EntityId targetId = default,
            EntityId attackerId = default,
            IReadOnlyList<EntityId> mulliganSelection = null)
        {
            Kind = kind;
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            BoardPosition = boardPosition;
            TargetId = targetId;
            AttackerId = attackerId;

            MulliganSelection = mulliganSelection == null
                ? NoCards
                : new List<EntityId>(mulliganSelection).ToArray();
        }

        public ReplayCommandKind Kind { get; }

        public PlayerId PlayerId { get; }

        public EntityId CardInstanceId { get; }

        public int BoardPosition { get; }

        public EntityId TargetId { get; }

        public EntityId AttackerId { get; }

        public IReadOnlyList<EntityId> MulliganSelection { get; }

        /// <summary>Captures a command that was actually submitted.</summary>
        public static ReplayCommand From(GameCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            switch (command)
            {
                case MulliganCommand mulligan:
                    return new ReplayCommand(
                        ReplayCommandKind.Mulligan, mulligan.PlayerId,
                        mulliganSelection: mulligan.CardsToReplace);

                case EndTurnCommand endTurn:
                    return new ReplayCommand(ReplayCommandKind.EndTurn, endTurn.PlayerId);

                case PlayCardCommand play:
                    return new ReplayCommand(
                        ReplayCommandKind.PlayCard, play.PlayerId,
                        play.CardInstanceId, play.BoardPosition, play.TargetId);

                case AttackCommand attack:
                    return new ReplayCommand(
                        ReplayCommandKind.Attack, attack.PlayerId,
                        targetId: attack.TargetId, attackerId: attack.AttackerId);

                default:
                    throw new NotSupportedException(
                        "A replay cannot record a " + command.GetType().Name +
                        ". Teach ReplayCommand about it before submitting it.");
            }
        }

        /// <summary>Rebuilds the very command that was submitted.</summary>
        public GameCommand ToCommand()
        {
            switch (Kind)
            {
                case ReplayCommandKind.Mulligan:
                    return new MulliganCommand(PlayerId, MulliganSelection);

                case ReplayCommandKind.EndTurn:
                    return new EndTurnCommand(PlayerId);

                case ReplayCommandKind.PlayCard:
                    return new PlayCardCommand(PlayerId, CardInstanceId, BoardPosition, TargetId);

                case ReplayCommandKind.Attack:
                    return new AttackCommand(PlayerId, AttackerId, TargetId);

                default:
                    throw new InvalidOperationException("Replay command kind is not set.");
            }
        }

        /// <summary>A single readable line, for a command history.</summary>
        public string Describe() => Kind switch
        {
            ReplayCommandKind.Mulligan =>
                "P" + PlayerId.Number + " Mulligan replace=" + MulliganSelection.Count,
            ReplayCommandKind.EndTurn =>
                "P" + PlayerId.Number + " EndTurn",
            ReplayCommandKind.PlayCard =>
                "P" + PlayerId.Number + " PlayCard card=#" + CardInstanceId.Value +
                " position=" + BoardPosition,
            ReplayCommandKind.Attack =>
                "P" + PlayerId.Number + " Attack attacker=#" + AttackerId.Value +
                " target=#" + TargetId.Value,
            _ => "unknown command"
        };

        public override string ToString() => Describe();
    }
}
