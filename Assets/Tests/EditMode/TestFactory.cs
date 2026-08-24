using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The smallest set of helpers the domain tests actually need today.
    ///
    /// Deliberately not a full scenario DSL: it grows only when it removes real
    /// noise from real tests. Right now that means building a catalog, a deck
    /// list, and a match already taken through setup and mulligan.
    /// </summary>
    internal static class TestFactory
    {
        public const string MinionCardId = "test_minion";
        public const string SpellCardId = "test_spell";

        /// <summary>Id of the extra card the second player receives, as configured by default.</summary>
        public static CardId CoinCardId => GameConfig.DefaultSecondPlayerExtraCard;

        public static CardDefinition MinionDefinition(
            string id = MinionCardId,
            string name = "Test Minion",
            int manaCost = 2,
            int attack = 2,
            int health = 3) =>
            new CardDefinition(new CardId(id), name, CardType.Minion, manaCost, attack, health);

        public static CardDefinition SpellDefinition(
            string id = SpellCardId,
            string name = "Test Spell",
            int manaCost = 1) =>
            new CardDefinition(new CardId(id), name, CardType.Spell, manaCost);

        /// <summary>The extra card given to the player going second. Never collectible.</summary>
        public static CardDefinition CoinDefinition() =>
            new CardDefinition(CoinCardId, "The Coin", CardType.Spell, 0, collectible: false);

        /// <summary>A catalog holding the standard test cards plus the extra card.</summary>
        public static CardCatalog Catalog(params CardDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                definitions = new[] { MinionDefinition(), SpellDefinition(), CoinDefinition() };
            }

            return new CardCatalog(definitions);
        }

        /// <summary>A freshly constructed match state: two heroes, four empty zones each.</summary>
        public static GameState Game(ulong seed = 1UL, params CardDefinition[] definitions) =>
            new GameState(GameConfig.Default, Catalog(definitions), seed);

        public static DeckList Deck(int count = 30, string cardId = MinionCardId)
        {
            List<CardId> cards = new List<CardId>(count);
            for (int index = 0; index < count; index++)
            {
                cards.Add(new CardId(cardId));
            }

            return new DeckList(cards);
        }

        public static GameEngine Engine(ulong seed = 1UL, GameConfig config = null, ICardCatalog catalog = null) =>
            new GameEngine(config ?? GameConfig.Default, catalog ?? Catalog(), seed);

        /// <summary>An engine sitting in the mulligan phase, hands already dealt.</summary>
        public static GameEngine MatchInMulligan(
            ulong seed = 1UL,
            int deckSize = 30,
            GameConfig config = null,
            ICardCatalog catalog = null)
        {
            GameEngine engine = Engine(seed, config, catalog);
            engine.StartMatch(Deck(deckSize), Deck(deckSize));
            return engine;
        }

        /// <summary>
        /// An engine in the playing phase, both players having kept their whole
        /// opening hand. The first turn has already been started.
        /// </summary>
        public static GameEngine StartedMatch(
            ulong seed = 1UL,
            int deckSize = 30,
            GameConfig config = null,
            ICardCatalog catalog = null)
        {
            GameEngine engine = MatchInMulligan(seed, deckSize, config, catalog);
            engine.Execute(new MulliganCommand(PlayerId.One));
            engine.Execute(new MulliganCommand(PlayerId.Two));
            return engine;
        }

        /// <summary>Removes every card from a deck, so the next draw hits fatigue.</summary>
        public static void EmptyDeck(Player player)
        {
            while (player.Deck.Count > 0)
            {
                player.Deck.RemoveAt(0);
            }
        }

        /// <summary>Moves cards from deck to hand until the hand holds the requested number.</summary>
        public static void FillHandFromDeck(Player player, int targetHandSize)
        {
            while (player.Hand.Count < targetHandSize && player.Deck.Count > 0)
            {
                CardInstance card = player.Deck.RemoveAt(0);
                card.Zone = ZoneType.Hand;
                player.Hand.TryAdd(card);
            }
        }

        /// <summary>Ends the current turn, asserting nothing; callers check the result.</summary>
        public static CommandResult EndTurn(GameEngine engine) =>
            engine.Execute(new EndTurnCommand(engine.State.CurrentPlayer));
    }

    /// <summary>
    /// A plain reference type for zone tests, used instead of string so that
    /// interned literals cannot make two logically distinct items compare as
    /// the same reference.
    /// </summary>
    internal sealed class TestItem
    {
        public TestItem(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString() => Name;
    }
}
