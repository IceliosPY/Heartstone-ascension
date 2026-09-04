using CoH.Core.Cards;
using CoH.Data;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The Necromancer's own Minion frame, resolved the same way every other
    /// frame in the catalog is: by type and class, never by card id.
    ///
    /// Every test here reaches the frame through <see cref="CardVisualMatch"/>
    /// resolution rather than by asserting a servant's card id is on some
    /// list, because the whole point of this pass was that a future
    /// Necromancer minion should never need to be added anywhere for its
    /// frame to work.
    /// </summary>
    public sealed class NecromancerMinionFrameTests
    {
        private const string CatalogAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualCatalog.asset";
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";

        private static CardVisualCatalogAsset Catalog()
        {
            CardVisualCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogAsset>(CatalogAssetPath);

            Assert.That(catalog, Is.Not.Null,
                "No card visual catalog. Run Conquest of Hearthstone -> Create Missing Card Visual Assets.");

            return catalog;
        }

        private static CardVisualLibraryAsset Library() =>
            AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);

        private static CardVisualDescriptor Describe(CardType type, CardClass cardClass) =>
            new CardVisualDescriptor(type, cardClass);

        // ------------------------------------------------------------------
        //  Class + type resolution
        // ------------------------------------------------------------------

        [Test]
        public void Neutral_minion_still_resolves_the_neutral_frame()
        {
            CardVisualResolution resolution =
                Catalog().Resolve(CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Neutral));

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite.name, Is.EqualTo("Card_Inhand_Minion_Neutral"),
                "Neutral minions must keep drawing the Neutral frame - this pass must not have disturbed it.");
        }

        [Test]
        public void Necromancer_minion_resolves_the_necromancer_frame()
        {
            CardVisualResolution resolution =
                Catalog().Resolve(CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Necromancer));

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite.name, Is.EqualTo("Card_Inhand_Minion_Necromancer"));
        }

        [Test]
        public void The_necromancer_frame_is_a_different_sprite_from_the_neutral_frame()
        {
            CardVisualCatalogAsset catalog = Catalog();

            Sprite neutral = catalog.Resolve(
                CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Neutral)).Sprite;
            Sprite necromancer = catalog.Resolve(
                CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Necromancer)).Sprite;

            Assert.That(necromancer, Is.Not.SameAs(neutral),
                "Necromancer minions are drawing the same frame sprite as Neutral - the class-specific " +
                "entry is not actually being picked up.");
        }

        /// <summary>
        /// The match is exactly type + class, proven by breaking each half in
        /// turn: change only the type and the frame must fall away, change
        /// only the class and it must fall away too.
        /// </summary>
        [Test]
        public void The_necromancer_frame_requires_both_minion_type_and_necromancer_class()
        {
            CardVisualCatalogAsset catalog = Catalog();

            Sprite necromancerMinion = catalog.Resolve(
                CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Necromancer)).Sprite;

            Sprite necromancerSpell = catalog.Resolve(
                CardVisualSlot.Frame, Describe(CardType.Spell, CardClass.Necromancer)).Sprite;

            Sprite neutralMinion = catalog.Resolve(
                CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Neutral)).Sprite;

            Assert.That(necromancerSpell, Is.Not.SameAs(necromancerMinion),
                "A Necromancer spell picked up the Necromancer Minion frame - the entry is not " +
                "constrained by CardType.");

            Assert.That(neutralMinion, Is.Not.SameAs(necromancerMinion),
                "A Neutral minion picked up the Necromancer Minion frame - the entry is not " +
                "constrained by CardClass.");
        }

        /// <summary>
        /// A Necromancer spell must keep drawing whatever a Necromancer spell
        /// already drew before this pass (the shared spell frame, or
        /// scaffolding) - never silently fall through to the Minion frame just
        /// because it shares a class.
        /// </summary>
        [Test]
        public void Necromancer_spell_does_not_draw_the_necromancer_minion_frame()
        {
            CardVisualResolution resolution =
                Catalog().Resolve(CardVisualSlot.Frame, Describe(CardType.Spell, CardClass.Necromancer));

            if (resolution.Found)
            {
                Assert.That(resolution.Sprite.name, Is.Not.EqualTo("Card_Inhand_Minion_Necromancer"));
            }
        }

        /// <summary>
        /// Nothing about the match names a card id, so a Necromancer minion
        /// that does not exist yet resolves the frame exactly like one that
        /// does - proving the resolution is genuinely class/type-based rather
        /// than secretly enumerating today's four servants.
        /// </summary>
        [Test]
        public void A_hypothetical_future_necromancer_minion_resolves_the_same_frame()
        {
            CardVisualDescriptor futureServant = new CardVisualDescriptor(
                CardType.Minion, CardClass.Necromancer, name: "Not Yet Authored",
                manaCost: 4, attack: 4, health: 4, showsStatistics: true);

            CardVisualResolution resolution = Catalog().Resolve(CardVisualSlot.Frame, futureServant);

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite.name, Is.EqualTo("Card_Inhand_Minion_Necromancer"));
        }

        // ------------------------------------------------------------------
        //  The four real Raise servants, through the real Card Visual
        //  Editor V2 code path
        // ------------------------------------------------------------------

        /// <summary>
        /// The exact conversion Card Visual Editor V2 uses when a real card
        /// is selected (<see cref="CardVisualSelection.Describe"/>), not a
        /// hand-built descriptor - so this proves the editor itself, not just
        /// the catalog in isolation.
        /// </summary>
        [TestCase("Assets/_Project/Data/Cards/Card_NecromancerSkeletalWarrior.asset")]
        [TestCase("Assets/_Project/Data/Cards/Card_NecromancerSkeletalRogue.asset")]
        [TestCase("Assets/_Project/Data/Cards/Card_NecromancerCryptFiend.asset")]
        [TestCase("Assets/_Project/Data/Cards/Card_NecromancerAbomination.asset")]
        public void Each_raise_servant_resolves_the_necromancer_frame_through_the_editor_path(string assetPath)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(assetPath);

            Assert.That(asset, Is.Not.Null, "Missing servant asset at " + assetPath);

            CardDefinition definition = asset.ToDefinition();

            Assert.That(definition.Class, Is.EqualTo(CardClass.Necromancer),
                assetPath + " is no longer authored as Necromancer.");
            Assert.That(definition.Type, Is.EqualTo(CardType.Minion));

            CardVisualDescriptor descriptor = CardVisualSelection.Describe(asset, Library());

            CardVisualResolution resolution = Catalog().Resolve(CardVisualSlot.Frame, descriptor);

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite.name, Is.EqualTo("Card_Inhand_Minion_Necromancer"),
                assetPath + " did not resolve the Necromancer Minion frame.");
        }

        // ------------------------------------------------------------------
        //  Structural sanity: alpha, and the rest of a Minion card still
        //  composes around the new frame
        // ------------------------------------------------------------------

        [Test]
        public void The_necromancer_frame_sprite_preserves_alpha()
        {
            Sprite sprite = Catalog().Resolve(
                CardVisualSlot.Frame, Describe(CardType.Minion, CardClass.Necromancer)).Sprite;

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture.format, Is.Not.EqualTo(TextureFormat.RGB24),
                "The imported texture has no alpha channel format.");
        }

        /// <summary>
        /// The frame changed; the gems, banner and rules panel a Minion card
        /// composes around it did not, and must still resolve to something
        /// for a Necromancer minion exactly as they do for a Neutral one.
        /// </summary>
        [Test]
        public void Other_minion_layers_still_resolve_for_a_necromancer_minion()
        {
            CardVisualCatalogAsset catalog = Catalog();
            CardVisualDescriptor card = Describe(CardType.Minion, CardClass.Necromancer);

            Assert.That(catalog.Resolve(CardVisualSlot.ManaGem, card).Found, Is.True);
            Assert.That(catalog.Resolve(CardVisualSlot.AttackGem, card).Found, Is.True);
            Assert.That(catalog.Resolve(CardVisualSlot.HealthGem, card).Found, Is.True);
        }
    }
}
