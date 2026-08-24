using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Actions;
using CoH.Core.Rules.Resolution;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// The engine: the one door into the rules.
    ///
    /// Everything follows the same shape. A caller hands over an intent, the
    /// engine validates it against the current state, turns it into internal
    /// work, drains that work through a resolution pipeline, and returns an
    /// ordered description of what happened. The engine never waits for
    /// anything, never knows about animation, and never lets a caller declare
    /// an outcome.
    ///
    /// That shape is already the shape a server needs: the day commands arrive
    /// over a network instead of from a local method call, nothing here changes.
    /// </summary>
    public sealed class GameEngine
    {
        public GameEngine(GameConfig config, ICardCatalog catalog, ulong seed)
        {
            State = new GameState(config, catalog, seed);
        }

        public GameState State { get; }

        /// <summary>
        /// Builds the match from two deck lists and moves it to the mulligan
        /// phase, returning everything that happened.
        ///
        /// Not a command: no player asks for it. On a server this is something
        /// the match host does, which is why it is a method rather than
        /// something a client could send.
        /// </summary>
        public IReadOnlyList<GameEvent> StartMatch(DeckList deckForSeatOne, DeckList deckForSeatTwo)
        {
            if (State.Phase != GamePhase.Setup)
            {
                throw new InvalidOperationException("The match has already been set up.");
            }

            ResolutionContext context = new ResolutionContext(State);
            MatchSetup.Run(context, deckForSeatOne, deckForSeatTwo);
            context.Run();
            return context.Events;
        }

        /// <summary>
        /// Validates and resolves a command. A refused command changes nothing
        /// and produces no events.
        /// </summary>
        public CommandResult Execute(GameCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            RejectionReason reason = Validate(command);
            if (reason != RejectionReason.None)
            {
                return CommandResult.Rejected(reason);
            }

            ResolutionContext context = new ResolutionContext(State);

            switch (command)
            {
                case MulliganCommand mulligan:
                    ApplyMulligan(mulligan, context);
                    break;

                case EndTurnCommand _:
                    context.Enqueue(new EndTurnAction(State.CurrentPlayer));
                    break;

                case PlayCardCommand playCard:
                    context.Enqueue(new PlayCardAction(
                        playCard.PlayerId,
                        playCard.CardInstanceId,
                        playCard.BoardPosition,
                        playCard.TargetId));
                    break;

                case AttackCommand attack:
                    context.Enqueue(new AttackAction(attack.PlayerId, attack.AttackerId, attack.TargetId));
                    break;

                default:
                    throw new NotSupportedException("Unhandled command type: " + command.GetType().Name);
            }

            context.Run();
            return CommandResult.Accepted(context.Events);
        }

        /// <summary>
        /// Asks whether a command would be accepted, without doing anything.
        ///
        /// This is how the presentation layer greys out a card or refuses to
        /// draw a targeting arrow: it asks the engine rather than deciding for
        /// itself. Same code path as Execute, so the two can never disagree.
        /// </summary>
        public RejectionReason CanExecute(GameCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return Validate(command);
        }

        /// <summary>
        /// Whether this minion is in a state to attack anything at all,
        /// ignoring targets. None means it can.
        /// </summary>
        public RejectionReason CanAttack(PlayerId playerId, EntityId attackerId)
        {
            if (State.HasEnded)
            {
                return RejectionReason.GameAlreadyEnded;
            }

            if (State.Phase != GamePhase.Playing)
            {
                return RejectionReason.WrongPhase;
            }

            if (playerId.IsNone)
            {
                return RejectionReason.UnknownPlayer;
            }

            if (playerId != State.CurrentPlayer)
            {
                return RejectionReason.NotYourTurn;
            }

            return CombatRules.ValidateAttacker(State, playerId, attackerId, out Minion _);
        }

        /// <summary>
        /// Everything the given minion may attack right now, or an empty list
        /// when it cannot attack at all.
        ///
        /// The engine is the only thing that decides what is a legal target.
        /// When the presentation highlights a target it will be showing this
        /// list, never its own idea of one.
        /// </summary>
        public IReadOnlyList<EntityId> GetLegalAttackTargets(PlayerId playerId, EntityId attackerId)
        {
            List<EntityId> targets = new List<EntityId>();

            if (CanAttack(playerId, attackerId) != RejectionReason.None)
            {
                return targets;
            }

            CombatRules.ValidateAttacker(State, playerId, attackerId, out Minion attacker);
            CombatRules.CollectLegalTargets(State, attacker, targets);
            return targets;
        }

        /// <summary>
        /// Pushes an internal action through the pipeline.
        ///
        /// Used by tests to build board situations that no command can produce
        /// yet, and by the command handlers above. Internal because a caller
        /// outside the engine must never be able to hand over ready-made work
        /// and bypass validation.
        /// </summary>
        internal IReadOnlyList<GameEvent> Resolve(ResolutionAction rootAction)
        {
            ResolutionContext context = new ResolutionContext(State);

            if (rootAction != null)
            {
                context.Enqueue(rootAction);
            }

            context.Run();
            return context.Events;
        }

        /// <summary>
        /// Runs the pipeline with nothing queued, which still performs a death
        /// phase. Lets a caller change state directly and then have the
        /// consequences processed properly.
        /// </summary>
        internal IReadOnlyList<GameEvent> ResolvePending() => Resolve(null);

        private RejectionReason Validate(GameCommand command)
        {
            if (State.HasEnded)
            {
                return RejectionReason.GameAlreadyEnded;
            }

            if (command.PlayerId.IsNone)
            {
                return RejectionReason.UnknownPlayer;
            }

            switch (command)
            {
                case MulliganCommand mulligan:
                    return ValidateMulligan(mulligan);

                case EndTurnCommand endTurn:
                    return ValidateEndTurn(endTurn);

                case PlayCardCommand playCard:
                    return ValidatePlayCard(playCard);

                case AttackCommand attack:
                    return ValidateAttack(attack);

                default:
                    return RejectionReason.WrongPhase;
            }
        }

        private RejectionReason ValidateAttack(AttackCommand command)
        {
            if (State.Phase != GamePhase.Playing)
            {
                return RejectionReason.WrongPhase;
            }

            if (command.PlayerId != State.CurrentPlayer)
            {
                return RejectionReason.NotYourTurn;
            }

            RejectionReason attackerProblem = CombatRules.ValidateAttacker(
                State, command.PlayerId, command.AttackerId, out Minion attacker);

            if (attackerProblem != RejectionReason.None)
            {
                return attackerProblem;
            }

            return CombatRules.ValidateTarget(State, attacker, command.TargetId);
        }

        /// <summary>
        /// Checks a card can be played, in the order that gives the most useful
        /// answer: what the card is before what it costs, and what it costs
        /// before where it would land.
        /// </summary>
        private RejectionReason ValidatePlayCard(PlayCardCommand command)
        {
            if (State.Phase != GamePhase.Playing)
            {
                return RejectionReason.WrongPhase;
            }

            if (command.PlayerId != State.CurrentPlayer)
            {
                return RejectionReason.NotYourTurn;
            }

            Player player = State.GetPlayer(command.PlayerId);

            CardInstance card = FindInHand(player, command.CardInstanceId);
            if (card == null)
            {
                return RejectionReason.CardNotInHand;
            }

            if (!State.Catalog.TryGet(card.CardId, out CardDefinition definition))
            {
                return RejectionReason.CardNotInHand;
            }

            if (definition.Type != CardType.Minion)
            {
                return RejectionReason.CardTypeNotPlayable;
            }

            if (!ManaSystem.CanPay(player, ManaSystem.GetPlayCost(State, card)))
            {
                return RejectionReason.NotEnoughMana;
            }

            if (player.Board.IsFull)
            {
                return RejectionReason.BoardFull;
            }

            // Rightmost is the one negative value that means something.
            if (command.BoardPosition != PlayCardCommand.Rightmost &&
                (command.BoardPosition < 0 || command.BoardPosition > player.Board.Count))
            {
                return RejectionReason.InvalidBoardPosition;
            }

            return RejectionReason.None;
        }

        private RejectionReason ValidateEndTurn(EndTurnCommand command)
        {
            if (State.Phase != GamePhase.Playing)
            {
                return RejectionReason.WrongPhase;
            }

            if (command.PlayerId != State.CurrentPlayer)
            {
                return RejectionReason.NotYourTurn;
            }

            return RejectionReason.None;
        }

        private RejectionReason ValidateMulligan(MulliganCommand command)
        {
            if (State.Phase != GamePhase.Mulligan)
            {
                return RejectionReason.WrongPhase;
            }

            Player player = State.GetPlayer(command.PlayerId);

            if (player.HasConfirmedMulligan)
            {
                return RejectionReason.AlreadyConfirmedMulligan;
            }

            IReadOnlyList<EntityId> selection = command.CardsToReplace;

            for (int index = 0; index < selection.Count; index++)
            {
                EntityId candidate = selection[index];

                if (!IsInHand(player, candidate))
                {
                    return RejectionReason.InvalidMulliganSelection;
                }

                for (int other = index + 1; other < selection.Count; other++)
                {
                    if (selection[other] == candidate)
                    {
                        return RejectionReason.InvalidMulliganSelection;
                    }
                }
            }

            return RejectionReason.None;
        }

        private void ApplyMulligan(MulliganCommand command, ResolutionContext context)
        {
            Player player = State.GetPlayer(command.PlayerId);
            player.SetMulliganSelection(command.CardsToReplace);
            player.HasConfirmedMulligan = true;

            bool bothConfirmed =
                State.GetPlayer(PlayerId.One).HasConfirmedMulligan &&
                State.GetPlayer(PlayerId.Two).HasConfirmedMulligan;

            if (bothConfirmed)
            {
                context.Enqueue(new ResolveMulligansAction());
            }
        }

        private static bool IsInHand(Player player, EntityId cardInstanceId) =>
            FindInHand(player, cardInstanceId) != null;

        private static CardInstance FindInHand(Player player, EntityId cardInstanceId)
        {
            for (int index = 0; index < player.Hand.Count; index++)
            {
                if (player.Hand[index].Id == cardInstanceId)
                {
                    return player.Hand[index];
                }
            }

            return null;
        }
    }
}
