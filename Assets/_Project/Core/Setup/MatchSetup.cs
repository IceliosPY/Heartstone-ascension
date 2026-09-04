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

            AssignHeroPowers(state);

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

        /// <summary>
        /// Gives each hero the power its seat was configured with.
        ///
        /// Before any shuffling, because it consumes no randomness and must not
        /// move what the seed produces: a match set up with hero powers deals
        /// exactly the same opening hands as the same match set up without
        /// them.
        ///
        /// A configured power the catalog does not know is refused here rather
        /// than at the moment a player clicks it. Failing at setup names the
        /// mistake; failing later would look like a broken button.
        /// </summary>
        private static void AssignHeroPowers(GameState state)
        {
            for (int index = 0; index < state.Players.Count; index++)
            {
                Player player = state.Players[index];
                CardId heroPower = state.Config.HeroPowerFor(player.Id);

                if (heroPower.IsNone)
                {
                    continue;
                }

                if (!state.Catalog.TryGet(heroPower, out CardDefinition definition))
                {
                    throw new InvalidOperationException(
                        "The catalog has no definition for " + player.Id + "'s hero power: " + heroPower);
                }

                if (definition.Type != CardType.HeroPower)
                {
                    throw new InvalidOperationException(
                        player.Id + "'s hero power " + heroPower + " is a " + definition.Type +
                        ", not a hero power.");
                }

                player.Hero.HeroPowerCardId = heroPower;
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
