using System.Collections.Generic;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Who may attack, and what they may attack.
    ///
    /// The one place those questions are answered. The presentation will ask it
    /// the same questions the command validator does, so a highlighted target
    /// and a legal target can never disagree.
    /// </summary>
    internal static class CombatRules
    {
        /// <summary>
        /// Checks a minion is in a state to attack anything at all, ignoring
        /// the target.
        ///
        /// Freeze would slot in here as one more check. There is no frozen flag
        /// on Minion today and none was added: a field nothing can ever set
        /// would be dead weight, and the mechanic belongs to a later phase.
        /// </summary>
        public static RejectionReason ValidateAttacker(
            GameState state,
            PlayerId playerId,
            EntityId attackerId,
            out Minion attacker)
        {
            attacker = null;

            if (!state.TryGetEntity(attackerId, out Entity entity))
            {
                return RejectionReason.InvalidAttacker;
            }

            if (!(entity is Minion minion))
            {
                // Heroes attack with weapons, which do not exist yet.
                return RejectionReason.InvalidAttacker;
            }

            if (!minion.IsInPlay || minion.IsPendingDeath)
            {
                return RejectionReason.InvalidAttacker;
            }

            if (minion.Controller != playerId)
            {
                return RejectionReason.InvalidAttacker;
            }

            if (minion.Attack <= 0)
            {
                return RejectionReason.ZeroAttack;
            }

            if (minion.IsSummoningSick(state.TurnNumber))
            {
                return RejectionReason.SummoningSickness;
            }

            if (minion.AttacksThisTurn >= minion.MaxAttacksPerTurn)
            {
                return RejectionReason.AlreadyAttacked;
            }

            attacker = minion;
            return RejectionReason.None;
        }

        public static RejectionReason ValidateTarget(GameState state, Minion attacker, EntityId targetId)
        {
            if (!state.TryGetEntity(targetId, out Entity target))
            {
                return RejectionReason.InvalidTarget;
            }

            return IsLegalTarget(attacker, target)
                ? RejectionReason.None
                : RejectionReason.InvalidTarget;
        }

        /// <summary>
        /// Everything this minion may attack, enemy minions from left to right
        /// and then the enemy hero.
        ///
        /// The order is fixed so that two identical situations always produce
        /// the same list, and so a future targeting arrow highlights things in a
        /// stable order.
        ///
        /// Taunt belongs here: when it arrives, this method keeps only the
        /// enemy minions that have it and drops the hero. Putting that anywhere
        /// else would let the presentation and the validator disagree.
        /// </summary>
        public static void CollectLegalTargets(GameState state, Minion attacker, List<EntityId> destination)
        {
            destination.Clear();

            Player enemy = state.GetPlayer(attacker.Controller.Opponent);

            for (int index = 0; index < enemy.Board.Count; index++)
            {
                Minion candidate = enemy.Board[index];
                if (IsLegalTarget(attacker, candidate))
                {
                    destination.Add(candidate.Id);
                }
            }

            if (IsLegalTarget(attacker, enemy.Hero))
            {
                destination.Add(enemy.Hero.Id);
            }
        }

        /// <summary>
        /// A target is legal when it belongs to the other side and is a
        /// character still in play. Anything else, a card in hand, a card in a
        /// deck, a minion already removed, is not something an attack can reach.
        /// </summary>
        private static bool IsLegalTarget(Minion attacker, Entity target)
        {
            if (target.Controller == attacker.Controller)
            {
                return false;
            }

            if (target is Hero hero)
            {
                return !hero.HasDied;
            }

            if (target is Minion minion)
            {
                return minion.IsInPlay && !minion.IsPendingDeath;
            }

            return false;
        }
    }
}
