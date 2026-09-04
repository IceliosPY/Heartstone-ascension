using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Data;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The four final Necromancer summon artworks, bound through
    /// <see cref="CardVisualLibraryAsset"/> - the one real source card
    /// artwork is ever resolved from. Nothing here asserts anything about
    /// Raise, a choice card, or any presentation code, because none of
    /// those know a card id either: the whole point of binding the library
    /// is that the ordinary <c>CardId -> CardVisualLibrary -> artwork</c>
    /// seam every other card already goes through is what shows these
    /// pictures, with nothing card-id-specific added anywhere downstream.
    /// </summary>
    public sealed class NecromancerArtworkTests
    {
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";

        private const string SkeletalWarrior = "Assets/_Project/Data/Cards/Card_NecromancerSkeletalWarrior.asset";
        private const string SkeletalRogue = "Assets/_Project/Data/Cards/Card_NecromancerSkeletalRogue.asset";
        private const string CryptFiend = "Assets/_Project/Data/Cards/Card_NecromancerCryptFiend.asset";
        private const string Abomination = "Assets/_Project/Data/Cards/Card_NecromancerAbomination.asset";

        private static CardVisualLibraryAsset Library()
        {
            CardVisualLibraryAsset library = AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);
            Assert.That(library, Is.Not.Null, "No card visual library at " + LibraryAssetPath + ".");
            return library;
        }

        [TestCase(SkeletalWarrior, "necromancer_skeletal_warrior", "Skeletal_Warrior")]
        [TestCase(SkeletalRogue, "necromancer_skeletal_rogue", "Skeletal_Rogue")]
        [TestCase(CryptFiend, "necromancer_crypt_fiend", "Crypt_Fiend")]
        [TestCase(Abomination, "necromancer_abomination", "Abomination")]
        public void Each_servant_resolves_its_own_final_artwork_through_the_library(
            string assetPath, string expectedCardId, string expectedSpriteName)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, "Missing servant asset at " + assetPath);

            CardDefinition definition = asset.ToDefinition();
            Assert.That(definition.Id.Value, Is.EqualTo(expectedCardId),
                assetPath + "'s CardId no longer matches what the library was bound against.");

            Sprite artwork = Library().ArtworkFor(definition.Id);

            Assert.That(artwork, Is.Not.Null,
                expectedCardId + " resolved no artwork at all from the library.");
            Assert.That(artwork.name, Is.EqualTo(expectedSpriteName),
                expectedCardId + " resolved '" + artwork.name + "' instead of the final '" +
                expectedSpriteName + "' artwork.");
        }

        [Test]
        public void The_four_final_artworks_are_four_distinct_sprites()
        {
            CardVisualLibraryAsset library = Library();

            Sprite warrior = library.ArtworkFor(new CardId("necromancer_skeletal_warrior"));
            Sprite rogue = library.ArtworkFor(new CardId("necromancer_skeletal_rogue"));
            Sprite crypt = library.ArtworkFor(new CardId("necromancer_crypt_fiend"));
            Sprite abomination = library.ArtworkFor(new CardId("necromancer_abomination"));

            Sprite[] all = { warrior, rogue, crypt, abomination };

            foreach (Sprite sprite in all)
            {
                Assert.That(sprite, Is.Not.Null);
            }

            for (int i = 0; i < all.Length; i++)
            {
                for (int j = i + 1; j < all.Length; j++)
                {
                    Assert.That(all[i], Is.Not.SameAs(all[j]),
                        "Two of the four Necromancer summons resolved the exact same artwork sprite - " +
                        "Skeletal Rogue and Crypt Fiend in particular must never be swapped.");
                }
            }
        }

        /// <summary>
        /// The exact conversion Card Visual Editor V2 uses when a real card
        /// is selected - proves the editor tool itself resolves the final
        /// artwork, not just the library in isolation.
        /// </summary>
        [TestCase(SkeletalWarrior, "Skeletal_Warrior")]
        [TestCase(SkeletalRogue, "Skeletal_Rogue")]
        [TestCase(CryptFiend, "Crypt_Fiend")]
        [TestCase(Abomination, "Abomination")]
        public void Card_visual_editor_v2_resolves_the_same_final_artwork(string assetPath, string expectedSpriteName)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, "Missing servant asset at " + assetPath);

            CardVisualDescriptor descriptor = CardVisualSelection.Describe(asset, Library());

            Assert.That(descriptor.Artwork, Is.Not.Null);
            Assert.That(descriptor.Artwork.name, Is.EqualTo(expectedSpriteName));
        }

        /// <summary>
        /// <see cref="CardDefinitionAsset.Artwork"/> is authored-but-orphaned:
        /// <c>ToDefinition()</c> never carries it into <c>CoH.Core</c>, and
        /// nothing in presentation reads it either. Every real servant's own
        /// asset still has it empty, which is the proof that binding the
        /// library - not filling in this field - is what actually made the
        /// pictures appear.
        /// </summary>
        [TestCase(SkeletalWarrior)]
        [TestCase(SkeletalRogue)]
        [TestCase(CryptFiend)]
        [TestCase(Abomination)]
        public void The_card_definitions_own_artwork_field_stays_empty(string assetPath)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, "Missing servant asset at " + assetPath);

            Assert.That(asset.Artwork, Is.Null,
                assetPath + " has its own Artwork field set - the library binding is the only source " +
                "of truth this integration is supposed to use.");
        }

        /// <summary>
        /// Card data itself is untouched by an artwork pass: cost, stats,
        /// keywords and the collectible flag all stay exactly what they
        /// were before these four pictures existed.
        /// </summary>
        [Test]
        public void Card_data_is_unchanged_by_the_artwork_integration()
        {
            AssertUnchanged(SkeletalWarrior, "Skeletal Warrior", 1, 1, 1);
            AssertUnchanged(SkeletalRogue, "Skeletal Rogue", 1, 0, 1);
            AssertUnchanged(CryptFiend, "Crypt Fiend", 1, 1, 2);
            AssertUnchanged(Abomination, "Abomination", 1, 0, 2);
        }

        private static void AssertUnchanged(
            string assetPath, string expectedName, int mana, int attack, int health)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);
            Assert.That(asset, Is.Not.Null, "Missing servant asset at " + assetPath);

            CardDefinition definition = asset.ToDefinition();

            Assert.That(definition.Name, Is.EqualTo(expectedName));
            Assert.That(definition.ManaCost, Is.EqualTo(mana));
            Assert.That(definition.Attack, Is.EqualTo(attack));
            Assert.That(definition.Health, Is.EqualTo(health));
            Assert.That(definition.Type, Is.EqualTo(CardType.Minion));
            Assert.That(definition.Class, Is.EqualTo(CardClass.Necromancer));
            Assert.That(asset.Collectible, Is.False, assetPath + " must stay non-collectible.");
        }
    }
}
