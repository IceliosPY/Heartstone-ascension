using CoH.Core.Events;
using CoH.Core.Rules.Resolution;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Mana crystals at the start of a turn.
    /// </summary>
    internal static class ManaSystem
    {
        /// <summary>
        /// Grants a crystal up to the cap, then refills for the turn.
        ///
        /// The refill deliberately subtracts locked crystals rather than simply
        /// assigning MaxMana. Overload is not implemented yet and nothing sets
        /// OverloadOwed, so the subtraction is inert today, but writing
        /// "AvailableMana = MaxMana" would be a formula we know to be wrong and
        /// would have to be found and fixed later.
        /// </summary>
        public static void StartTurn(Player player, GameConfig config, ResolutionContext context)
        {
            if (player.MaxMana < config.MaxManaCrystals)
            {
                player.MaxMana++;
                context.Emit(new ManaCrystalGainedEvent(player.Id, player.MaxMana));
            }

            player.OverloadLocked = player.OverloadOwed;
            player.OverloadOwed = 0;

            // Temporary mana lasts for the turn it was granted in and no longer.
            player.TemporaryMana = 0;

            player.AvailableMana = player.MaxMana - player.OverloadLocked;
            context.Emit(new ManaRefilledEvent(player.Id, player.AvailableMana, player.MaxMana));
        }
    }
}
