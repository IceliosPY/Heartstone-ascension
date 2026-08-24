using System.Collections.Generic;
using CoH.Core.Events;
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
        public static void Draw(Player player, List<GameEvent> events)
        {
            if (player.Deck.Count == 0)
            {
                ApplyFatigue(player, events);
                return;
            }

            CardInstance card = player.Deck.RemoveAt(0);

            if (player.Hand.IsFull)
            {
                // The card leaves the deck and is destroyed. It never reaches
                // the hand, so it is reported as burned rather than drawn.
                card.Zone = ZoneType.Graveyard;
                player.Graveyard.TryAdd(card);
                events.Add(new CardBurnedEvent(player.Id, card.Id, card.CardId, player.Deck.Count));
                return;
            }

            card.Zone = ZoneType.Hand;
            player.Hand.TryAdd(card);
            events.Add(new CardDrawnEvent(player.Id, card.Id, card.CardId, player.Deck.Count));
        }

        /// <summary>
        /// Takes the top card straight into the hand, with no fatigue and no
        /// burning. Used by the mulligan, where a replacement draw happens
        /// before the match has begun and must never hurt the player.
        /// </summary>
        public static CardInstance DrawWithoutFatigue(Player player, List<GameEvent> events)
        {
            if (player.Deck.Count == 0 || player.Hand.IsFull)
            {
                return null;
            }

            CardInstance card = player.Deck.RemoveAt(0);
            card.Zone = ZoneType.Hand;
            player.Hand.TryAdd(card);
            events.Add(new CardDrawnEvent(player.Id, card.Id, card.CardId, player.Deck.Count));
            return card;
        }

        private static void ApplyFatigue(Player player, List<GameEvent> events)
        {
            // The counter rises first, then deals its new value as damage, so
            // the sequence is 1, 2, 3 and so on, and it never resets.
            player.FatigueCounter++;

            events.Add(new FatigueDamageEvent(player.Id, player.FatigueCounter));
            HeroDamageRules.ApplyDamage(player.Hero, player.FatigueCounter, events);
        }
    }
}
