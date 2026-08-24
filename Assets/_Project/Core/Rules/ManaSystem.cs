using System;
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

        /// <summary>
        /// What this particular copy costs to play right now.
        ///
        /// The single place a play cost is worked out. Nothing else reads
        /// CardDefinition.ManaCost to decide what to charge, so when cards start
        /// costing less or more there is exactly one method to change.
        ///
        /// The floor at zero lives here rather than on CardInstance: a negative
        /// number is a perfectly valid modifier total, and clamping it is a game
        /// rule, not a property of the data.
        /// </summary>
        public static int GetPlayCost(GameState state, CardInstance card)
        {
            int cost = card.GetCost(state.Catalog);

            // Extension point (Phase 11): cost-changing auras and enchantments
            // are folded in here, before the floor is applied.

            return Math.Max(0, cost);
        }

        public static bool CanPay(Player player, int cost) => player.AvailableMana >= cost;

        /// <summary>
        /// Spends mana. Free cards emit nothing: there is no such thing as an
        /// animation for spending zero.
        /// </summary>
        public static void Pay(ResolutionContext context, Player player, int cost)
        {
            if (cost <= 0)
            {
                return;
            }

            player.AvailableMana -= cost;
            context.Emit(new ManaSpentEvent(player.Id, cost, player.AvailableMana));
        }
    }
}
