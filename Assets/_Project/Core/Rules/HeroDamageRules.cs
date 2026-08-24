using System;
using System.Collections.Generic;
using CoH.Core.Events;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Applying damage to a hero, armour included.
    ///
    /// Seam note: fatigue is the first thing in the engine that deals damage,
    /// and it needs the armour rule to be right. Rather than write a
    /// fatigue-only shortcut that combat would have to duplicate and then
    /// delete, this is the single place hero damage is applied. Combat and
    /// spell damage will call it too, and the death phase will read the result.
    /// </summary>
    internal static class HeroDamageRules
    {
        public static void ApplyDamage(Hero hero, int amount, List<GameEvent> events)
        {
            if (hero == null)
            {
                throw new ArgumentNullException(nameof(hero));
            }

            if (amount <= 0)
            {
                return;
            }

            int absorbed = Math.Min(hero.Armor, amount);
            hero.Armor -= absorbed;
            hero.Damage += amount - absorbed;

            events.Add(new HeroDamagedEvent(
                hero.Owner,
                hero.Id,
                amount,
                absorbed,
                hero.CurrentHealth));
        }
    }
}
