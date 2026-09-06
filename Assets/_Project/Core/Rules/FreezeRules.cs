using System;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Applies and removes Frozen, the generic state that makes a character
    /// lose its next attack opportunity.
    ///
    /// Frozen is a fact about a character, not about the card that caused it:
    /// nothing here knows a card id, and any future card freezes exactly the
    /// way Ice Barrage does by using the same action. See
    /// <see cref="Minion.FrozenUntilTurn"/> for the state itself.
    ///
    /// A character is Frozen for as long as <c>FrozenUntilTurn</c> holds a
    /// value. What the value records is which of the controller's turns must
    /// pass, in full, before the character thaws - worked out once, here, at
    /// the moment Freeze is applied, using nothing but
    /// <see cref="GameState.TurnNumber"/> and <see cref="GameState.CurrentPlayer"/>.
    /// Removing it is a separate, explicit step: see
    /// <see cref="ThawAtEndOfTurn"/>, called from <c>EndTurnAction</c>.
    /// </summary>
    internal static class FreezeRules
    {
        /// <param name="sourceId">What applied the freeze, or None.</param>
        public static void Apply(ResolutionContext context, EntityId sourceId, EntityId targetId)
        {
            if (!context.State.TryGetEntity(targetId, out Entity target))
            {
                return;
            }

            if (target is Minion minion)
            {
                ApplyToMinion(context, sourceId, minion);
                return;
            }

            if (target is Hero hero)
            {
                ApplyToHero(context, sourceId, hero);
            }
        }

        private static void ApplyToMinion(ResolutionContext context, EntityId sourceId, Minion minion)
        {
            // A minion already gone cannot be frozen, exactly as it cannot be
            // damaged - see DamageRules.DamageMinion's own guard.
            if (!minion.IsInPlay || minion.IsPendingDeath)
            {
                return;
            }

            bool hasAttackOpportunity = CombatRules.ValidateAttackerIgnoringFreeze(
                context.State, minion.Controller, minion.Id, out _) == RejectionReason.None;

            int expiration = ExpirationTurn(context.State, minion.Controller, hasAttackOpportunity);

            minion.FrozenUntilTurn = Extend(minion.FrozenUntilTurn, expiration);
            context.Emit(new FrozenEvent(sourceId, minion.Id, minion.Controller, minion.FrozenUntilTurn.Value));
        }

        private static void ApplyToHero(ResolutionContext context, EntityId sourceId, Hero hero)
        {
            if (hero.HasDied)
            {
                return;
            }

            // Heroes cannot attack yet, so there is no "attack opportunity"
            // to ask about - Case A never applies to a hero today. This is
            // the inert, ready-for-later half of the mechanic (see
            // Hero.FrozenUntilTurn); nothing currently reads it back except
            // the fingerprint.
            int expiration = ExpirationTurn(context.State, hero.Controller, hasAttackOpportunityIgnoringFreeze: false);

            hero.FrozenUntilTurn = Extend(hero.FrozenUntilTurn, expiration);
            context.Emit(new FrozenEvent(sourceId, hero.Id, hero.Controller, hero.FrozenUntilTurn.Value));
        }

        /// <summary>
        /// Re-freezing never shortens an existing freeze, only ever extends
        /// it - a second Freeze landing before the first has expired must not
        /// give the character back a turn it had already lost.
        /// </summary>
        private static int Extend(int? existing, int candidate) =>
            existing.HasValue ? Math.Max(existing.Value, candidate) : candidate;

        /// <summary>
        /// The controller's turn during which this application must take
        /// away an attack, worked out from three cases:
        ///
        /// Case A - frozen on the controller's own turn, with a real attack
        /// opportunity still available (ignoring Freeze): that opportunity is
        /// the one lost, so the expiry is this very turn.
        ///
        /// Case B - frozen on the controller's own turn, but with no attack
        /// opportunity available even ignoring Freeze (already attacked,
        /// still summoning sick, or any other structural reason): this turn
        /// was never available to lose, so the expiry is the controller's
        /// next turn.
        ///
        /// Case C - frozen on the opponent's turn, which is not the
        /// controller's to lose at all: the expiry is the controller's next
        /// turn, exactly as in Case B. This is Ice Barrage's own case.
        ///
        /// Turns alternate one at a time between two players, which is why
        /// the controller's next turn is two turns away when it is currently
        /// their own turn, and one turn away when it is the opponent's - the
        /// same arithmetic <see cref="Minion.IsSummoningSick"/>'s own doc
        /// comment already relies on.
        /// </summary>
        private static int ExpirationTurn(GameState state, PlayerId controller, bool hasAttackOpportunityIgnoringFreeze)
        {
            bool controllersTurn = state.CurrentPlayer == controller;

            if (controllersTurn && hasAttackOpportunityIgnoringFreeze)
            {
                return state.TurnNumber;
            }

            return controllersTurn ? state.TurnNumber + 2 : state.TurnNumber + 1;
        }

        /// <summary>
        /// Thaws whatever this player controls that was due to thaw at the
        /// end of this exact turn. Called from <c>EndTurnAction</c>, before
        /// the next turn starts - not left to be discovered later by a read
        /// of <c>FrozenUntilTurn</c>, per the design: Frozen is removed, not
        /// merely outlived.
        /// </summary>
        public static void ThawAtEndOfTurn(ResolutionContext context, Player player)
        {
            int endingTurn = context.State.TurnNumber;

            for (int index = 0; index < player.Board.Count; index++)
            {
                Minion minion = player.Board[index];

                if (minion.FrozenUntilTurn == endingTurn)
                {
                    minion.FrozenUntilTurn = null;
                    context.Emit(new ThawedEvent(minion.Id, player.Id));
                }
            }

            if (player.Hero.FrozenUntilTurn == endingTurn)
            {
                player.Hero.FrozenUntilTurn = null;
                context.Emit(new ThawedEvent(player.Hero.Id, player.Id));
            }
        }
    }
}
