using System.IO;
using CoH.Core.Cards;
using CoH.Editor;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The hero power's frame, traced all the way from the manifest to the
    /// catalog.
    ///
    /// This is the same shape of test as <see cref="RealAssetIntegrationTests"/>:
    /// read the project's own manifest and catalog rather than building a
    /// private one, because what is under test is the wiring between them, and
    /// a test with its own copy would prove nothing about the real one. What
    /// is specific to this file is the fact these tests exist to guard: that
    /// "Power" was never a real component name on the source site, and the
    /// naive guess for it resolves to nothing rather than failing loudly.
    /// </summary>
    public sealed class HeroPowerAssetIntegrationTests
    {
        private const string CatalogAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualCatalog.asset";

        // ------------------------------------------------------------------
        //  The manifest
        // ------------------------------------------------------------------

        [Test]
        public void The_manifest_names_a_hero_power_frame_and_backdrop()
        {
            Assert.That(HearthCardsManifest.TryLoad(out HearthCardsManifestFile manifest), Is.True);

            HearthCardsEntry frame = Find(manifest, "frame_heropower");
            HearthCardsEntry backdrop = Find(manifest, "backdrop_heropower");

            Assert.That(frame, Is.Not.Null, "No 'frame_heropower' entry in the manifest.");
            Assert.That(frame.cardType, Is.EqualTo("HeroPower"));
            Assert.That(frame.slot, Is.EqualTo("Frame"));
            Assert.That(frame.filename, Is.EqualTo("Card_Inhand_HeroPower_Neutral.webp"));

            Assert.That(backdrop, Is.Not.Null, "No 'backdrop_heropower' entry in the manifest.");
            Assert.That(backdrop.cardType, Is.EqualTo("HeroPower"));
            Assert.That(backdrop.slot, Is.EqualTo("Backdrop"));
        }

        /// <summary>
        /// The renderer's own template draws this component the same for
        /// every class - a "static" layer, not a "classMapping" one, unlike
        /// the minion, spell and hero frames. The manifest records that fact
        /// by leaving the class unconstrained; this is the test that would
        /// fail if someone "fixed" it to say Neutral to match its siblings.
        /// </summary>
        [Test]
        public void The_frame_entry_is_not_constrained_to_a_class()
        {
            Assert.That(HearthCardsManifest.TryLoad(out HearthCardsManifestFile manifest), Is.True);

            HearthCardsEntry frame = Find(manifest, "frame_heropower");

            Assert.That(frame.cardClass, Is.Null.Or.Empty,
                "The real template draws one frame for every class. Constraining this entry to a " +
                "class would make every class but that one fall back to scaffolding.");
        }

        private static HearthCardsEntry Find(HearthCardsManifestFile manifest, string id)
        {
            foreach (HearthCardsEntry entry in HearthCardsManifest.Entries(manifest))
            {
                if (entry.id == id)
                {
                    return entry;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------
        //  The imported files
        // ------------------------------------------------------------------

        [Test]
        public void The_imported_frame_and_backdrop_exist_on_disk()
        {
            Assert.That(
                File.Exists("Assets/ThirdParty/HearthCards/Imported/Card_Inhand_HeroPower_Neutral.png"),
                Is.True);

            Assert.That(
                File.Exists("Assets/ThirdParty/HearthCards/Imported/Card_Inhand_HeroPower_DropShadow.png"),
                Is.True);
        }

        // ------------------------------------------------------------------
        //  The catalog
        // ------------------------------------------------------------------

        private static CardVisualCatalogAsset Catalog()
        {
            CardVisualCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogAsset>(CatalogAssetPath);

            Assert.That(catalog, Is.Not.Null,
                "No card visual catalog. Run Conquest of Hearthstone -> Create Missing Card Visual Assets.");

            return catalog;
        }

        [Test]
        public void The_catalog_resolves_a_real_frame_for_hero_power()
        {
            CardVisualResolution resolution = Catalog().Resolve(
                CardVisualSlot.Frame, new CardVisualDescriptor(CardType.HeroPower, CardClass.Neutral));

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite.name, Is.EqualTo("Card_Inhand_HeroPower_Neutral"));
        }

        [Test]
        public void The_catalog_resolves_a_real_backdrop_for_hero_power()
        {
            CardVisualResolution resolution = Catalog().Resolve(
                CardVisualSlot.Backdrop, new CardVisualDescriptor(CardType.HeroPower, CardClass.Neutral));

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite.name, Is.EqualTo("Card_Inhand_HeroPower_DropShadow"));
        }

        /// <summary>
        /// The real point of leaving the frame entry class-unconstrained:
        /// Necromancer is not a class HearthCards has ever heard of, and never
        /// will be. Without this, the Necromancer's own hero power - the only
        /// hero power the project has - would draw scaffolding forever.
        /// </summary>
        [Test]
        public void The_catalog_resolves_the_same_real_frame_for_necromancer()
        {
            CardVisualResolution neutral = Catalog().Resolve(
                CardVisualSlot.Frame, new CardVisualDescriptor(CardType.HeroPower, CardClass.Neutral));

            CardVisualResolution necromancer = Catalog().Resolve(
                CardVisualSlot.Frame, new CardVisualDescriptor(CardType.HeroPower, CardClass.Necromancer));

            Assert.That(necromancer.Found, Is.True);
            Assert.That(necromancer.Sprite, Is.SameAs(neutral.Sprite));
        }

        [Test]
        public void Hero_and_minion_frames_are_unaffected_by_the_new_entries()
        {
            CardVisualCatalogAsset catalog = Catalog();

            CardVisualResolution hero = catalog.Resolve(
                CardVisualSlot.Frame, new CardVisualDescriptor(CardType.Hero, CardClass.Neutral));
            CardVisualResolution minion = catalog.Resolve(
                CardVisualSlot.Frame, new CardVisualDescriptor(CardType.Minion, CardClass.Neutral));

            Assert.That(hero.Sprite.name, Is.EqualTo("Card_Inhand_Hero_Neutral"));
            Assert.That(minion.Sprite.name, Is.EqualTo("Card_Inhand_Minion_Neutral"));
        }
    }
}
