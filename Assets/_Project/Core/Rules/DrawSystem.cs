using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Actions;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Drawing cards, and what happens when a draw cannot be honoured.
    ///
    /// Drawing uses no randomness at all: the deck was shuffled once at setup,
    /// and a draw simply takes the card on top. That is what keeps a whole
    /// match reproducible from its seed.
    /// </summary>
    internal static class DrawSystem
    {
        /// <summary>
        /// The normal turn draw: top of deck to hand, or fatigue when the deck
        /// is empty, or a burned card when the hand is full.
        /// </summary>
        public static void Draw(ResolutionContext context, Player player)
        {
            if (player.Deck.Count == 0)
            {
                ApplyFatigue(context, player);
                return;
            }

            CardInstance card = player.Deck.RemoveAt(0);

            if (player.Hand.IsFull)
            {
                // The card leaves the deck and is destroyed. It never reaches
                // the hand, so it is reported as burned rather than drawn.
                card.Zone = ZoneType.Graveyard;
                player.Graveyard.TryAdd(card);
                context.Emit(new CardBurnedEvent(player.Id, card.Id, card.CardId, player.Deck.Count));
                return;
            }

            card.Zone = ZoneType.Hand;
            player.Hand.TryAdd(card);
            context.Emit(new CardDrawnEvent(player.Id, card.Id, card.CardId, player.Deck.Count));
        }

        /// <summary>
        /// Takes the top card straight into the hand, with no fatigue and no
        /// burning. Used by setup and by the mulligan, where a draw happens
        /// before the match has begun and must never hurt the player.
        /// </summary>
        public static CardInstance DrawWithoutFatigue(ResolutionContext context, Player player)
        {
            if (player.Deck.Count == 0 || player.Hand.IsFull)
            {
                return null;
            }

            CardInstance card = player.Deck.RemoveAt(0);
            card.Zone = ZoneType.Hand;
            player.Hand.TryAdd(card);
            context.Emit(new CardDrawnEvent(player.Id, card.Id, card.CardId, player.Deck.Count));
            return card;
        }

        /// <summary>
        /// Fatigue rises first, then deals its new value as damage, so the
        /// sequence is 1, 2, 3 and so on, and it never resets.
        ///
        /// The damage is queued rather than applied here, so it goes through
        /// the one place damage is handled and is followed by a death phase
        /// like any other damage. Fatigue has no special path to ending a
        /// match.
        /// </summary>
        private static void ApplyFatigue(ResolutionContext context, Player player)
        {
            player.FatigueCounter++;

            context.Emit(new FatigueDamageEvent(player.Id, player.FatigueCounter));
            context.Enqueue(new DealDamageAction(EntityId.None, player.Hero.Id, player.FatigueCounter));
        }
    }
}
