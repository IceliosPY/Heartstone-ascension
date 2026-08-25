using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Rules.Effects;
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

        private GameEngine(GameState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Wraps a state that was prepared rather than played into existence.
        ///
        /// For debug scenarios, and for replaying one. A normal match is always
        /// built by the constructor above and reaches its position through
        /// MatchSetup and the rules; this is the seam that lets a developer
        /// start from a position instead of playing fifteen turns to reach it.
        /// Nothing in the rules uses it.
        /// </summary>
        public static GameEngine FromState(GameState state) => new GameEngine(state);

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

            bool isMinion = definition.Type == CardType.Minion;

            if (!isMinion && definition.Type != CardType.Spell)
            {
                return RejectionReason.CardTypeNotPlayable;
            }

            if (!ManaSystem.CanPay(player, ManaSystem.GetPlayCost(State, card)))
            {
                return RejectionReason.NotEnoughMana;
            }

            if (isMinion && player.Board.IsFull)
            {
                return RejectionReason.BoardFull;
            }

            // Rightmost is the one negative value that means something.
            if (isMinion &&
                command.BoardPosition != PlayCardCommand.Rightmost &&
                (command.BoardPosition < 0 || command.BoardPosition > player.Board.Count))
            {
                return RejectionReason.InvalidBoardPosition;
            }

            return ValidateTarget(definition, command.PlayerId, command.TargetId);
        }

        /// <summary>
        /// Checks what the player pointed at, if anything.
        ///
        /// The rule differs between a spell and a minion, and that difference is
        /// Hearthstone's rather than ours. A spell is only its effect, so with
        /// nothing legal to aim at there is nothing to buy and it cannot be
        /// cast. A minion is also a body, so it goes down and the battlecry
        /// simply does not happen. Where a target does exist, both must point at
        /// one: Hearthstone gives no option to decline.
        /// </summary>
        private RejectionReason ValidateTarget(
            CardDefinition definition, PlayerId controller, EntityId targetId)
        {
            PlayTargetRequirement requirement = TargetRequirementOf(definition);

            if (requirement == PlayTargetRequirement.None)
            {
                return targetId.IsNone ? RejectionReason.None : RejectionReason.InvalidTarget;
            }

            SelectorDefinition selector = EffectQueries.FindPlayTargetSelector(definition.Effects);

            List<EntityId> legal = new List<EntityId>();
            SelectorResolver.CollectLegalTargets(State, selector, controller, legal);

            if (legal.Count == 0)
            {
                // Nothing to aim at. A spell is unplayable; a minion is played
                // and its battlecry finds nobody.
                if (requirement == PlayTargetRequirement.Required)
                {
                    return RejectionReason.InvalidTarget;
                }

                return targetId.IsNone ? RejectionReason.None : RejectionReason.InvalidTarget;
            }

            if (targetId.IsNone || !legal.Contains(targetId))
            {
                return RejectionReason.InvalidTarget;
            }

            return RejectionReason.None;
        }

        private static PlayTargetRequirement TargetRequirementOf(CardDefinition definition)
        {
            if (EffectQueries.FindPlayTargetSelector(definition.Effects) == null)
            {
                return PlayTargetRequirement.None;
            }

            return definition.Type == CardType.Spell
                ? PlayTargetRequirement.Required
                : PlayTargetRequirement.Optional;
        }

        /// <summary>
        /// Whether playing this card asks the player to point at something.
        ///
        /// Read only, and the same answer the validation will give. A client
        /// asks rather than inspecting the card's effects and inventing a rule
        /// of its own, which is the only way the highlighted list a player sees
        /// can be the list the engine checks their answer against.
        /// </summary>
        public PlayTargetRequirement GetPlayTargetRequirement(PlayerId playerId, EntityId cardInstanceId)
        {
            if (playerId.IsNone)
            {
                return PlayTargetRequirement.None;
            }

            CardInstance card = FindInHand(State.GetPlayer(playerId), cardInstanceId);

            if (card == null || !State.Catalog.TryGet(card.CardId, out CardDefinition definition))
            {
                return PlayTargetRequirement.None;
            }

            return TargetRequirementOf(definition);
        }

        /// <summary>
        /// Everything this card may legally be aimed at right now, in a fixed
        /// order. Empty when it takes no target, or when nothing qualifies.
        /// </summary>
        public IReadOnlyList<EntityId> GetLegalPlayTargets(PlayerId playerId, EntityId cardInstanceId)
        {
            List<EntityId> targets = new List<EntityId>();

            if (playerId.IsNone)
            {
                return targets;
            }

            CardInstance card = FindInHand(State.GetPlayer(playerId), cardInstanceId);

            if (card == null || !State.Catalog.TryGet(card.CardId, out CardDefinition definition))
            {
                return targets;
            }

            SelectorResolver.CollectLegalTargets(
                State, EffectQueries.FindPlayTargetSelector(definition.Effects), playerId, targets);

            return targets;
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
