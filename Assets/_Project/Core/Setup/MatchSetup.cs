using System;
using CoH.Core.Cards;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Setup
{
    /// <summary>
    /// Everything that happens between "two deck lists and a seed" and "both
    /// players are looking at an opening hand".
    /// </summary>
    internal static class MatchSetup
    {
        /// <summary>
        /// Runs setup. The order of the randomised steps is fixed and part of
        /// the engine's contract, since it decides what the seed produces:
        ///
        ///   1. build both decks (no randomness);
        ///   2. shuffle seat one's deck, then seat two's;
        ///   3. draw the starting player;
        ///   4. deal the opening hands.
        ///
        /// Shuffling happens in seat order rather than starting-player order on
        /// purpose, so that the two decks are shuffled the same way whichever
        /// player ends up going first.
        /// </summary>
        public static void Run(ResolutionContext context, DeckList deckForSeatOne, DeckList deckForSeatTwo)
        {
            if (deckForSeatOne == null)
            {
                throw new ArgumentNullException(nameof(deckForSeatOne));
            }

            if (deckForSeatTwo == null)
            {
                throw new ArgumentNullException(nameof(deckForSeatTwo));
            }

            GameState state = context.State;

            RequireExtraCardIsKnown(state);

            BuildDeck(state, PlayerId.One, deckForSeatOne);
            BuildDeck(state, PlayerId.Two, deckForSeatTwo);

            state.GetPlayer(PlayerId.One).Deck.Shuffle(state.RandomSource);
            state.GetPlayer(PlayerId.Two).Deck.Shuffle(state.RandomSource);

            state.StartingPlayer = state.RandomSource.NextInt(2) == 0 ? PlayerId.One : PlayerId.Two;
            context.Emit(new GameStartedEvent(state.StartingPlayer, state.Seed));

            DealOpeningHands(context);

            state.Phase = GamePhase.Mulligan;
            context.Emit(new MulliganStartedEvent());
        }

        private static void BuildDeck(GameState state, PlayerId playerId, DeckList deckList)
        {
            Player player = state.GetPlayer(playerId);

            for (int index = 0; index < deckList.Cards.Count; index++)
            {
                CardInstance card = state.CreateCardInstance(deckList.Cards[index], playerId);
                card.Zone = ZoneType.Deck;
                player.Deck.TryAdd(card);
            }
        }

        private static void DealOpeningHands(ResolutionContext context)
        {
            GameState state = context.State;
            Player starting = state.GetPlayer(state.StartingPlayer);
            Player second = state.GetPlayer(state.StartingPlayer.Opponent);

            Deal(context, starting, state.Config.StartingPlayerHandSize);
            Deal(context, second, state.Config.SecondPlayerHandSize);
        }

        private static void Deal(ResolutionContext context, Player player, int count)
        {
            for (int index = 0; index < count; index++)
            {
                // Dealing must never inflict fatigue: a deck shorter than an
                // opening hand is a deck-building problem, not a game event.
                DrawSystem.DrawWithoutFatigue(context, player);
            }
        }

        private static void RequireExtraCardIsKnown(GameState state)
        {
            CardId extraCard = state.Config.SecondPlayerExtraCard;
            if (extraCard.IsNone)
            {
                return;
            }

            if (!state.Catalog.TryGet(extraCard, out CardDefinition _))
            {
                throw new InvalidOperationException(
                    "The catalog has no definition for the second player's extra card: " + extraCard);
            }
        }
    }
}
