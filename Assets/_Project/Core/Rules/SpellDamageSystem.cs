using CoH.Core.Events;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// A player's own temporary bonus to damaging spells - Hearthstone's own
    /// Spell Damage, scoped for now to whatever grants it a turn at a time.
    ///
    /// One generic modifier on <see cref="Player"/>
    /// (<see cref="Player.SpellDamageBonus"/>) rather than a card-specific
    /// number: any future effect that grants Spell Damage adds through
    /// <see cref="Grant"/> exactly like Lunar Phase does, and
    /// <see cref="Apply"/> is the one place a damaging spell's printed
    /// number is adjusted. Nothing here knows a card id, and nothing outside
    /// this file should reach into <see cref="Player.SpellDamageBonus"/>
    /// directly - the same separation <see cref="ManaSystem"/> keeps around
    /// <see cref="Player.TemporaryMana"/>.
    /// </summary>
    internal static class SpellDamageSystem
    {
        /// <summary>
        /// Grants Spell Damage for the rest of the player's current turn.
        /// Additive, so a second source before this one expires stacks with
        /// it rather than replacing it - the generic behaviour, even though
        /// nothing today grants a second source.
        /// </summary>
        public static void Grant(ResolutionContext context, Player player, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            player.SpellDamageBonus += amount;
            context.Emit(new SpellDamageGrantedEvent(player.Id, amount, player.SpellDamageBonus));
        }

        /// <summary>
        /// Clears the bonus at the end of the turn it was granted in - not at
        /// the start of this player's next turn, which would leave it active
        /// through the whole of the opponent's intervening turn.
        /// </summary>
        public static void ExpireAtEndOfTurn(ResolutionContext context, Player player)
        {
            if (player.SpellDamageBonus == 0)
            {
                return;
            }

            player.SpellDamageBonus = 0;
            context.Emit(new SpellDamageExpiredEvent(player.Id));
        }

        /// <summary>
        /// What a damaging spell this player casts actually deals, after
        /// their current Spell Damage. The one place that arithmetic
        /// happens, so a damaging spell's own printed number is never
        /// adjusted anywhere else.
        /// </summary>
        public static int Apply(Player player, int baseAmount) => baseAmount + player.SpellDamageBonus;
    }
}
