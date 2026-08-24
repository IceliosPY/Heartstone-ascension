using System;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// The single place damage is applied, heroes and minions alike.
    ///
    /// A plain helper rather than an action, and that distinction matters. An
    /// action is queued, and a death phase runs between queued actions. Two
    /// characters trading blows must both take their damage before anything
    /// dies, so combat will call this twice inside one action rather than
    /// queueing two. Queue damage only when it genuinely happens later, as
    /// fatigue does.
    ///
    /// Nothing is removed here. A character reduced to zero is left standing
    /// until the next death phase decides what to do with it.
    /// </summary>
    internal static class DamageRules
    {
        /// <param name="sourceId">What deals the damage, or None for sourceless damage such as fatigue.</param>
        public static void Deal(ResolutionContext context, EntityId sourceId, EntityId targetId, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (!context.State.TryGetEntity(targetId, out Entity target))
            {
                return;
            }

            if (target is Minion minion)
            {
                DamageMinion(context, sourceId, minion, amount);
                return;
            }

            if (target is Hero hero)
            {
                DamageHero(context, sourceId, hero, amount);
            }
        }

        private static void DamageMinion(ResolutionContext context, EntityId sourceId, Minion minion, int amount)
        {
            // A minion already removed from the board cannot be hit again.
            if (!minion.IsInPlay)
            {
                return;
            }

            minion.Damage += amount;

            context.Emit(new DamageDealtEvent(
                sourceId,
                minion.Id,
                minion.Controller,
                amount,
                absorbedByArmor: 0,
                remainingHealth: minion.CurrentHealth));
        }

        private static void DamageHero(ResolutionContext context, EntityId sourceId, Hero hero, int amount)
        {
            if (hero.HasDied)
            {
                return;
            }

            // Armour soaks damage before health does, and only heroes have it.
            int absorbed = Math.Min(hero.Armor, amount);
            hero.Armor -= absorbed;
            hero.Damage += amount - absorbed;

            context.Emit(new DamageDealtEvent(
                sourceId,
                hero.Id,
                hero.Owner,
                amount,
                absorbed,
                hero.CurrentHealth));
        }
    }
}
