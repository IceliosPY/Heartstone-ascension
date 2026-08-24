using System.Collections.Generic;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Resolving the opening-hand replacements.
    /// </summary>
    internal static class MulliganSystem
    {
        /// <summary>
        /// Carries out both players' mulligans and hands the extra card to the
        /// player going second.
        ///
        /// Resolution always runs seat one then seat two, never in the order
        /// the two confirmations happened to arrive. Both players draw from the
        /// same match random source, so submission order would otherwise change
        /// which replacement cards each player receives.
        /// </summary>
        public static void ResolveAll(ResolutionContext context)
        {
            GameState state = context.State;

            ResolveFor(context, state.GetPlayer(PlayerId.One));
            ResolveFor(context, state.GetPlayer(PlayerId.Two));

            GrantSecondPlayerExtraCard(context);
        }

        /// <summary>
        /// The replacement procedure, in the order that matters:
        ///
        ///   1. the chosen cards leave the hand and are set aside;
        ///   2. replacements are drawn from what remains of the deck;
        ///   3. only then do the set-aside cards go back into the deck;
        ///   4. the deck is shuffled.
        ///
        /// Steps 1 and 2 have to happen before step 3, otherwise a card a
        /// player just threw away could be dealt straight back as its own
        /// replacement.
        /// </summary>
        private static void ResolveFor(ResolutionContext context, Player player)
        {
            List<CardInstance> setAside = new List<CardInstance>();

            foreach (EntityId cardId in player.MulliganSelection)
            {
                CardInstance card = FindInHand(player, cardId);
                if (card == null)
                {
                    continue;
                }

                player.Hand.Remove(card);
                card.Zone = ZoneType.SetAside;
                setAside.Add(card);
            }

            for (int index = 0; index < setAside.Count; index++)
            {
                // No fatigue and no burning here: this happens before the match
                // has begun, and the deck is far larger than any opening hand.
                DrawSystem.DrawWithoutFatigue(context, player);
            }

            for (int index = 0; index < setAside.Count; index++)
            {
                CardInstance card = setAside[index];
                card.Zone = ZoneType.Deck;
                player.Deck.TryAdd(card);
            }

            if (setAside.Count > 0)
            {
                // Shuffled only when something actually went back. With nothing
                // returned there is nothing to hide, and reordering the deck
                // would consume randomness for no reason.
                player.Deck.Shuffle(context.State.RandomSource);
            }

            player.ClearMulliganSelection();
            context.Emit(new MulliganResolvedEvent(player.Id, setAside.Count));
        }

        private static void GrantSecondPlayerExtraCard(ResolutionContext context)
        {
            GameState state = context.State;
            CardId extraCard = state.Config.SecondPlayerExtraCard;

            if (extraCard.IsNone)
            {
                return;
            }

            Player receiver = state.GetPlayer(state.StartingPlayer.Opponent);

            CardInstance card = state.CreateCardInstance(extraCard, receiver.Id);
            card.Zone = ZoneType.Hand;
            receiver.Hand.TryAdd(card);

            context.Emit(new CardGeneratedEvent(receiver.Id, card.Id, card.CardId));
        }

        private static CardInstance FindInHand(Player player, EntityId cardInstanceId)
        {
            for (int index = 0; index < player.Hand.Count; index++)
            {
                if (player.Hand[index].Id == cardInstanceId)
                {
                    return player.Hand[index];
                }
            }

            return null;
        }
    }
}
