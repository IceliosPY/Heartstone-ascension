using CoH.Core.Cards;
using CoH.Core.Diagnostics;
using CoH.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.DataEditMode
{
    /// <summary>
    /// Redrawing a card must not invalidate a replay of a match it was in.
    ///
    /// This is the test that can only live here. Everywhere else the artwork
    /// simply is not present, which is the whole design: a Sprite stops at the
    /// authoring asset and never reaches the rules. So rather than colouring one
    /// card in and checking a hash did not move, these check the reason it
    /// cannot move, which holds for every card that will ever be drawn.
    /// </summary>
    public sealed class ArtworkAndFingerprintTests
    {
        /// <summary>
        /// The gameplay definition has nowhere to put a Sprite. A fingerprint
        /// taken from it could not include presentation data even by mistake.
        /// </summary>
        [Test]
        public void A_runtime_definition_carries_no_unity_object_at_all()
        {
            foreach (System.Reflection.PropertyInfo property in typeof(CardDefinition).GetProperties())
            {
                Assert.That(
                    typeof(Object).IsAssignableFrom(property.PropertyType), Is.False,
                    "CardDefinition." + property.Name + " is a Unity object. " +
                    "Presentation data has to stop at the authoring asset.");
            }
        }

        /// <summary>The authoring side does hold one, so the split is real rather than accidental.</summary>
        [Test]
        public void The_authoring_asset_is_the_side_that_holds_the_artwork()
        {
            CardDefinitionAsset authored = AuthoredCards.TestSoldier();

            Assert.That(
                typeof(CardDefinitionAsset).GetProperty("Artwork"), Is.Not.Null,
                "The authoring asset is meant to be where artwork lives.");

            Assert.That(
                typeof(CardDefinition).GetProperty("Artwork"), Is.Null,
                "The gameplay definition must not have gained an artwork field.");

            // And converting reaches the engine without it.
            CardDefinition definition = authored.ToDefinition();
            Assert.That(definition.Id.Value, Is.EqualTo("test_soldier"));
        }

        /// <summary>
        /// The real authored catalog fingerprints the same every time it is
        /// converted, which is what a replay recorded against it relies on.
        /// </summary>
        [Test]
        public void The_authored_catalog_fingerprints_the_same_every_time_it_is_built()
        {
            string first = CatalogFingerprint.Of(AuthoredCards.Catalog().BuildRuntimeCatalog());

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.That(
                    CatalogFingerprint.Of(AuthoredCards.Catalog().BuildRuntimeCatalog()),
                    Is.EqualTo(first),
                    "The authored catalog fingerprinted differently between two conversions.");
            }
        }

        /// <summary>
        /// Two catalogs holding the same cards fingerprint alike whatever order
        /// the authoring listed them in.
        /// </summary>
        [Test]
        public void The_authored_catalog_does_not_depend_on_the_order_it_was_listed_in()
        {
            CardDefinition soldier = AuthoredCards.TestSoldier().ToDefinition();
            CardDefinition coin = AuthoredCards.TheCoin().ToDefinition();

            CardCatalog forwards = new CardCatalog(new[] { soldier, coin });
            CardCatalog backwards = new CardCatalog(new[] { coin, soldier });

            Assert.That(CatalogFingerprint.Of(backwards), Is.EqualTo(CatalogFingerprint.Of(forwards)));
        }
    }
}
