using System.Collections.Generic;
using System.Linq;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Setup;
using CoH.Core.State;
using CoH.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.DataEditMode
{
    /// <summary>
    /// The authored catalog and deck, and the proof that a real match can be
    /// built from them without a single Unity type reaching the engine.
    /// </summary>
    public sealed class CatalogAndDeckTests
    {
        /// <summary>
        /// The development set. Named rather than counted, so adding a card
        /// later does not fail a test that has no opinion about the number.
        /// </summary>
        private static readonly string[] ExpectedCards =
        {
            "test_soldier",
            "the_coin",
            "test_token",
            "test_battlecry_damage",
            "test_deathrattle_draw",
            "test_summoner",
            "test_buff",
            "test_aoe"
        };

        [Test]
        public void The_catalog_holds_the_development_cards()
        {
            CardCatalogAsset asset = AuthoredCards.Catalog();

            Assert.That(asset.Cards.Select(card => card.RawId), Is.EquivalentTo(ExpectedCards));
        }

        [Test]
        public void The_runtime_catalog_finds_a_card_by_id()
        {
            CardCatalog catalog = AuthoredCards.Catalog().BuildRuntimeCatalog();

            Assert.That(catalog.Count, Is.EqualTo(ExpectedCards.Length));
            Assert.That(catalog.Get(new CardId("test_soldier")).Name, Is.EqualTo("Test Soldier"));
            Assert.That(catalog.TryGet(new CardId("the_coin"), out CardDefinition coin), Is.True);
            Assert.That(coin.Collectible, Is.False);
        }

        /// <summary>
        /// The Coin works because of the row of data below and for no other
        /// reason. Nothing anywhere recognises its id.
        /// </summary>
        [Test]
        public void The_coin_carries_its_effect_as_data()
        {
            CardDefinition coin = AuthoredCards.Catalog().BuildRuntimeCatalog().Get(new CardId("the_coin"));

            Assert.That(coin.Effects.Count, Is.EqualTo(1));

            EffectDefinition effect = coin.Effects[0];

            Assert.That(effect.Trigger, Is.EqualTo(EffectTrigger.OnPlay));
            Assert.That(effect.Selector.Kind, Is.EqualTo(SelectorKind.FriendlyHero));
            Assert.That(effect.Action.Kind, Is.EqualTo(EffectActionKind.GainTemporaryMana));
            Assert.That(effect.Action.Amount, Is.EqualTo(1));
        }

        /// <summary>Every demonstration card reaches the engine with its effects intact.</summary>
        [Test]
        public void The_authored_effect_cards_convert_with_their_effects()
        {
            CardCatalog catalog = AuthoredCards.Catalog().BuildRuntimeCatalog();

            AssertEffect(catalog, "test_battlecry_damage",
                EffectTrigger.Battlecry, SelectorKind.ChosenTarget, EffectActionKind.DealDamage);

            AssertEffect(catalog, "test_deathrattle_draw",
                EffectTrigger.Deathrattle, SelectorKind.FriendlyHero, EffectActionKind.DrawCards);

            AssertEffect(catalog, "test_summoner",
                EffectTrigger.Battlecry, SelectorKind.Self, EffectActionKind.Summon);

            AssertEffect(catalog, "test_buff",
                EffectTrigger.Battlecry, SelectorKind.ChosenTarget, EffectActionKind.ModifyStats);

            AssertEffect(catalog, "test_aoe",
                EffectTrigger.OnPlay, SelectorKind.AllEnemyMinions, EffectActionKind.DealDamage);

            // And a plain body still has none at all.
            Assert.That(catalog.Get(new CardId("test_soldier")).Effects, Is.Empty);
            Assert.That(catalog.Get(new CardId("test_token")).Effects, Is.Empty);
        }

        private static void AssertEffect(
            CardCatalog catalog, string cardId,
            EffectTrigger trigger, SelectorKind selector, EffectActionKind action)
        {
            CardDefinition card = catalog.Get(new CardId(cardId));

            Assert.That(card.Effects.Count, Is.EqualTo(1), cardId + " should have one effect.");
            Assert.That(card.Effects[0].Trigger, Is.EqualTo(trigger), cardId);
            Assert.That(card.Effects[0].Selector.Kind, Is.EqualTo(selector), cardId);
            Assert.That(card.Effects[0].Action.Kind, Is.EqualTo(action), cardId);
        }

        [Test]
        public void An_unknown_id_is_handled_rather_than_guessed()
        {
            CardCatalog catalog = AuthoredCards.Catalog().BuildRuntimeCatalog();

            Assert.That(catalog.TryGet(new CardId("does_not_exist"), out CardDefinition missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.Throws<KeyNotFoundException>(() => catalog.Get(new CardId("does_not_exist")));
        }

        [Test]
        public void The_authored_catalog_is_valid()
        {
            List<string> problems = new List<string>();
            AuthoredCards.Catalog().Validate(problems);

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void A_repeated_id_is_reported()
        {
            CardCatalogAsset catalog = ScriptableObject.CreateInstance<CardCatalogAsset>();
            catalog.name = "CardCatalog_UnderTest";

            CardDefinitionAsset soldier = AuthoredCards.TestSoldier();
            SetCards(catalog, new List<CardDefinitionAsset> { soldier, soldier });

            List<string> problems = new List<string>();
            catalog.Validate(problems);

            Assert.That(problems, Has.Some.Contains("appears more than once"));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void An_empty_slot_is_reported()
        {
            CardCatalogAsset catalog = ScriptableObject.CreateInstance<CardCatalogAsset>();
            catalog.name = "CardCatalog_UnderTest";
            SetCards(catalog, new List<CardDefinitionAsset> { AuthoredCards.TestSoldier(), null });

            List<string> problems = new List<string>();
            catalog.Validate(problems);

            Assert.That(problems, Has.Some.Contains("entry 1 is empty"));

            // Building must not choke on it either.
            Assert.That(catalog.BuildRuntimeCatalog().Count, Is.EqualTo(1));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void An_empty_catalog_is_reported()
        {
            CardCatalogAsset catalog = ScriptableObject.CreateInstance<CardCatalogAsset>();
            catalog.name = "CardCatalog_UnderTest";

            List<string> problems = new List<string>();
            catalog.Validate(problems);

            Assert.That(problems, Has.Some.Contains("catalog is empty"));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void The_catalog_can_find_the_authoring_asset_behind_an_id()
        {
            CardCatalogAsset catalog = AuthoredCards.Catalog();

            // This is how the presentation will reach artwork from an event that
            // only carries a card id.
            Assert.That(catalog.TryFindAsset(new CardId("test_soldier"), out CardDefinitionAsset asset), Is.True);
            Assert.That(asset.DisplayName, Is.EqualTo("Test Soldier"));
            Assert.That(catalog.TryFindAsset(new CardId("nope"), out CardDefinitionAsset _), Is.False);
        }

        [Test]
        public void The_test_deck_is_thirty_test_soldiers()
        {
            DeckListAsset deck = AuthoredCards.Deck();

            Assert.That(deck.TotalCards, Is.EqualTo(30));

            DeckList runtime = deck.BuildRuntimeDeckList();

            Assert.That(runtime.Count, Is.EqualTo(30));
            Assert.That(runtime.Cards.All(id => id == new CardId("test_soldier")), Is.True);
        }

        [Test]
        public void The_authored_deck_is_valid()
        {
            List<string> problems = new List<string>();
            AuthoredCards.Deck().Validate(problems);

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void A_match_can_be_played_from_the_authored_assets()
        {
            // The whole point of the phase: Unity data in, plain C# engine out.
            CardCatalog catalog = AuthoredCards.Catalog().BuildRuntimeCatalog();
            DeckList deck = AuthoredCards.Deck().BuildRuntimeDeckList();

            GameEngine engine = new GameEngine(GameConfig.Default, catalog, seed: 1234UL);
            engine.StartMatch(deck, deck);

            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Mulligan));

            engine.Execute(new CoH.Core.Commands.MulliganCommand(PlayerId.One));
            engine.Execute(new CoH.Core.Commands.MulliganCommand(PlayerId.Two));

            Assert.That(engine.State.Phase, Is.EqualTo(GamePhase.Playing));

            Player starting = engine.State.GetPlayer(engine.State.StartingPlayer);
            Player second = engine.State.GetPlayer(engine.State.StartingPlayer.Opponent);

            Assert.That(starting.Hand.Count, Is.EqualTo(4), "Three dealt plus the first turn draw.");
            Assert.That(second.Hand.Count, Is.EqualTo(5), "Four dealt plus the extra card.");
            Assert.That(
                second.Hand.Any(card => card.CardId == new CardId("the_coin")),
                Is.True,
                "The Coin came from the authored catalog, not from a hardcoded definition.");
            Assert.That(starting.Deck.Count + starting.Hand.Count, Is.EqualTo(30));
        }

        [Test]
        public void A_card_from_the_authored_catalog_can_actually_be_played()
        {
            CardCatalog catalog = AuthoredCards.Catalog().BuildRuntimeCatalog();
            DeckList deck = AuthoredCards.Deck().BuildRuntimeDeckList();

            GameEngine engine = new GameEngine(GameConfig.Default, catalog, seed: 77UL);
            engine.StartMatch(deck, deck);
            engine.Execute(new CoH.Core.Commands.MulliganCommand(PlayerId.One));
            engine.Execute(new CoH.Core.Commands.MulliganCommand(PlayerId.Two));

            // Two turns each so the starting player can afford a 2 mana card.
            PlayerId first = engine.State.StartingPlayer;
            engine.Execute(new CoH.Core.Commands.EndTurnCommand(engine.State.CurrentPlayer));
            engine.Execute(new CoH.Core.Commands.EndTurnCommand(engine.State.CurrentPlayer));

            Player player = engine.State.GetPlayer(first);
            CardInstance soldier = player.Hand.First(card => card.CardId == new CardId("test_soldier"));

            Assert.That(
                engine.Execute(new CoH.Core.Commands.PlayCardCommand(first, soldier.Id)).IsAccepted,
                Is.True);

            Minion summoned = player.Board[0];
            Assert.That(summoned.CardId, Is.EqualTo(new CardId("test_soldier")));
            Assert.That(summoned.Attack, Is.EqualTo(2));
            Assert.That(summoned.CurrentHealth, Is.EqualTo(3));
        }

        [Test]
        public void A_deck_holding_a_non_collectible_card_is_reported()
        {
            DeckListAsset deck = ScriptableObject.CreateInstance<DeckListAsset>();
            deck.name = "Deck_UnderTest";
            SetEntries(deck, AuthoredCards.TheCoin(), 2);

            List<string> problems = new List<string>();
            deck.Validate(problems);

            Assert.That(problems, Has.Some.Contains("not collectible"));
            Object.DestroyImmediate(deck);
        }

        [Test]
        public void An_empty_deck_is_reported()
        {
            DeckListAsset deck = ScriptableObject.CreateInstance<DeckListAsset>();
            deck.name = "Deck_UnderTest";

            List<string> problems = new List<string>();
            deck.Validate(problems);

            Assert.That(problems, Has.Some.Contains("deck is empty"));
            Object.DestroyImmediate(deck);
        }

        private static void SetCards(CardCatalogAsset catalog, List<CardDefinitionAsset> cards)
        {
            typeof(CardCatalogAsset)
                .GetField("cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(catalog, cards);
        }

        private static void SetEntries(DeckListAsset deck, CardDefinitionAsset card, int count)
        {
            DeckListAsset.Entry entry = new DeckListAsset.Entry();

            System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

            typeof(DeckListAsset.Entry).GetField("card", flags).SetValue(entry, card);
            typeof(DeckListAsset.Entry).GetField("count", flags).SetValue(entry, count);

            typeof(DeckListAsset).GetField("entries", flags)
                .SetValue(deck, new List<DeckListAsset.Entry> { entry });
        }
    }
}
