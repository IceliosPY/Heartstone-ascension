using System.Collections.Generic;
using CoH.Data;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The logic behind the card picker, apart from the window that draws it.
    ///
    /// "Pick a card" used to do nothing, because the button carrying that label
    /// only ever cleared the selection - there was no path that actually set
    /// one. The fix is two small pieces of non-UI logic: a roster that finds
    /// real cards without scanning the project on every repaint, and a
    /// translation that turns a chosen card into what the rest of the editor
    /// edits. Both are exercised here without opening a window, so a picker
    /// bug shows up in a test rather than only under a mouse.
    /// </summary>
    public sealed class CardRosterAndSelectionTests
    {
        private const string TempAssetPath = "Assets/_Project/Data/Cards/__CardRosterTestTemp.asset";

        [SetUp]
        public void SetUp() => CardRoster.Invalidate();

        [TearDown]
        public void TearDown()
        {
            CardRoster.Invalidate();

            if (AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(TempAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(TempAssetPath);
                AssetDatabase.Refresh();
            }
        }

        // ------------------------------------------------------------------
        //  Roster discovery
        // ------------------------------------------------------------------

        [Test]
        public void The_roster_finds_the_real_cards_the_project_has()
        {
            List<string> names = DisplayNames(CardRoster.All());

            Assert.That(names, Does.Contain("Test Soldier"));
            Assert.That(names, Does.Contain("The Coin"));
        }

        /// <summary>
        /// Proves the roster actually rescans the project rather than
        /// returning a canned list: a card added after the cache was warmed is
        /// invisible until <see cref="CardRoster.Invalidate"/> is called, and
        /// found the moment after.
        /// </summary>
        [Test]
        public void Invalidate_makes_the_next_call_rescan_the_project()
        {
            int before = CardRoster.All().Count;

            CardDefinitionAsset temp = ScriptableObject.CreateInstance<CardDefinitionAsset>();
            AssetDatabase.CreateAsset(temp, TempAssetPath);
            AssetDatabase.SaveAssets();

            Assert.That(CardRoster.All().Count, Is.EqualTo(before),
                "The roster rescanned on its own, without Invalidate being called.");

            CardRoster.Invalidate();

            Assert.That(CardRoster.All().Count, Is.EqualTo(before + 1),
                "Invalidate did not make the next call see the new card.");
        }

        // ------------------------------------------------------------------
        //  Search
        // ------------------------------------------------------------------

        [Test]
        public void Search_finds_a_card_by_a_fragment_of_its_name()
        {
            Assert.That(DisplayNames(CardRoster.Search("Soldier")), Does.Contain("Test Soldier"));
        }

        [Test]
        public void Search_is_case_insensitive()
        {
            Assert.That(DisplayNames(CardRoster.Search("soldier")), Does.Contain("Test Soldier"));
            Assert.That(DisplayNames(CardRoster.Search("SOLDIER")), Does.Contain("Test Soldier"));
        }

        [Test]
        public void A_blank_search_finds_everything()
        {
            Assert.That(CardRoster.Search(string.Empty).Count, Is.EqualTo(CardRoster.All().Count));
            Assert.That(CardRoster.Search(null).Count, Is.EqualTo(CardRoster.All().Count));
        }

        [Test]
        public void A_search_with_no_match_finds_nothing()
        {
            Assert.That(CardRoster.Search("zzz_no_card_is_named_this_zzz"), Is.Empty);
        }

        // ------------------------------------------------------------------
        //  What a selected card hands to the rest of the editor
        // ------------------------------------------------------------------

        private static CardDefinitionAsset Fixture(string path)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(path);
            Assert.That(asset, Is.Not.Null, "Fixture card missing: " + path);
            return asset;
        }

        private static CardDefinitionAsset TestSoldier() =>
            Fixture("Assets/_Project/Data/Cards/Card_TestSoldier.asset");

        private static CardDefinitionAsset TheCoin() =>
            Fixture("Assets/_Project/Data/Cards/Card_TheCoin.asset");

        [Test]
        public void Describe_reads_the_selected_cards_own_data()
        {
            CardDefinitionAsset card = TestSoldier();

            CardVisualDescriptor described = CardVisualSelection.Describe(card, null);

            Assert.That(described.Name, Is.EqualTo(card.DisplayName));
            Assert.That(described.Type, Is.EqualTo(card.CardType));
        }

        /// <summary>
        /// The core claim of "This card" scope: an adjustment made after
        /// selecting one card reaches that card's own row, and nowhere else.
        /// </summary>
        [Test]
        public void An_adjustment_reaches_only_the_row_of_the_card_that_was_selected()
        {
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                CardDefinitionAsset soldier = TestSoldier();
                CardDefinitionAsset coin = TheCoin();

                CardVisualSelection.Adjustments(soldier, library)
                    .Set("NameText (other)", "layer.width", CardVisualValue.Of(515f));

                CardVisualOverrides soldiersOwn = CardVisualSelection.Describe(soldier, library).Overrides;
                CardVisualOverrides coinsOwn = CardVisualSelection.Describe(coin, library).Overrides;

                Assert.That(soldiersOwn, Is.Not.Null);
                Assert.That(soldiersOwn.Overrides("NameText (other)", "layer.width"), Is.True);

                Assert.That(coinsOwn, Is.Null,
                    "An adjustment made for one selected card reached another card's row.");
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        /// <summary>
        /// Switching the selection away and back does not lose or blend state:
        /// each card's row is exactly its own, both times it is looked at.
        /// </summary>
        [Test]
        public void Switching_the_selected_card_shows_that_cards_own_state_each_time()
        {
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                CardDefinitionAsset soldier = TestSoldier();
                CardDefinitionAsset coin = TheCoin();

                CardVisualSelection.Adjustments(soldier, library)
                    .Set("NameText (other)", "layer.y", CardVisualValue.Of(648f));

                Assert.That(CardVisualSelection.Describe(coin, library).Overrides, Is.Null,
                    "Selecting a different card showed the first card's adjustment.");

                CardVisualOverrides restored = CardVisualSelection.Describe(soldier, library).Overrides;

                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.TryGet("NameText (other)", "layer.y", out CardVisualValue value), Is.True);
                Assert.That(value.number, Is.EqualTo(648f));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        private static List<string> DisplayNames(IReadOnlyList<CardDefinitionAsset> cards)
        {
            List<string> names = new List<string>();

            for (int index = 0; index < cards.Count; index++)
            {
                names.Add(cards[index].DisplayName);
            }

            return names;
        }
    }
}
