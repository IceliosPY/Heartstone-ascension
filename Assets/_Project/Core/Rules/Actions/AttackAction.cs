using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Resolves one attack.
    ///
    /// The whole point of this action is that both blows land inside it, with
    /// no death phase in between. Both attack values are read before anything
    /// is mutated, so a minion that dies in the exchange still deals its damage.
    /// Splitting the two hits into two queued actions would break that, because
    /// a death phase runs between queued actions.
    ///
    /// A hero being attacked does not strike back. That is not a simplification:
    /// in Hearthstone only the defending minion retaliates, whatever attack the
    /// hero may have from a weapon.
    /// </summary>
    internal sealed class AttackAction : ResolutionAction
    {
        private readonly PlayerId _playerId;
        private readonly EntityId _attackerId;
        private readonly EntityId _targetId;

        public AttackAction(PlayerId playerId, EntityId attackerId, EntityId targetId)
        {
            _playerId = playerId;
            _attackerId = attackerId;
            _targetId = targetId;
        }

        public override void Resolve(ResolutionContext context)
        {
            GameState state = context.State;

            // Rechecked rather than trusted from validation: effects that make a
            // minion attack will queue this action directly, and by then the
            // board may have changed.
            if (CombatRules.ValidateAttacker(state, _playerId, _attackerId, out Minion attacker) != RejectionReason.None)
            {
                return;
            }

            if (CombatRules.ValidateTarget(state, attacker, _targetId) != RejectionReason.None)
            {
                return;
            }

            state.TryGetEntity(_targetId, out Entity target);

            context.Emit(new AttackDeclaredEvent(attacker.Controller, attacker.Id, target.Id));

            // Both values captured here, before a single point of damage is
            // applied. Everything below works from these two numbers.
            int damageToTarget = attacker.Attack;
            int damageBackToAttacker = target is Minion defender ? defender.Attack : 0;

            // The attack is spent the moment it is declared, whatever it kills.
            attacker.AttacksThisTurn++;

            // And stealth is spent with it. Striking is what reveals a hidden
            // minion, so this happens on declaring the attack rather than on
            // surviving it: a minion that trades and dies was still visible for
            // the exchange.
            attacker.RemoveKeyword(CardKeywords.Stealth);

            // Order is a convention, not causality: the two hits are
            // simultaneous, but a list of events has to be in some order, so the
            // attacker's blow is always reported first.
            DamageRules.Deal(context, attacker.Id, target.Id, damageToTarget);
            DamageRules.Deal(context, target.Id, attacker.Id, damageBackToAttacker);

            // Extension point (Phase 11): after-attack triggers are queued here,
            // before the death phase that follows this action.
        }
    }
}
