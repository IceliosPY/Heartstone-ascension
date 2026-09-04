using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// Huntress Shot's final artwork, resolved through the one real source
    /// card artwork is ever resolved from - the same
    /// <c>CardId -> CardVisualLibrary -> artwork</c> seam
    /// <see cref="NecromancerArtworkTests"/> already proves for the
    /// Necromancer set. Nothing here asserts anything about presentation
    /// code, because none of it knows a card id either.
    /// </summary>
    public sealed class HuntressShotArtworkTests
    {
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";
        private const string CardId = "starcaller_huntress_shot";

        private static CardVisualLibraryAsset Library()
        {
            CardVisualLibraryAsset library = AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);
            Assert.That(library, Is.Not.Null, "No card visual library at " + LibraryAssetPath + ".");
            return library;
        }

        [Test]
        public void Huntress_shot_resolves_its_final_artwork_through_the_library()
        {
            Sprite artwork = Library().ArtworkFor(new CoH.Core.Identifiers.CardId(CardId));

            Assert.That(artwork, Is.Not.Null, "Huntress Shot resolved no artwork at all from the library.");
            Assert.That(artwork.name, Is.EqualTo("Huntress_Shot"),
                "Huntress Shot resolved '" + artwork.name + "' instead of its own final artwork.");
        }

        [Test]
        public void The_bound_artwork_is_not_the_placeholder_or_an_unrelated_sprite()
        {
            Sprite artwork = Library().ArtworkFor(new CoH.Core.Identifiers.CardId(CardId));

            Assert.That(artwork, Is.Not.Null);
            Assert.That(artwork, Is.Not.SameAs(Library().ArtworkFor(default)),
                "Huntress Shot fell back to the library's default/placeholder artwork.");
        }

        [Test]
        public void The_artwork_file_lives_in_production_not_in_the_intake_folder()
        {
            string path = AssetDatabase.GetAssetPath(Library().ArtworkFor(new CoH.Core.Identifiers.CardId(CardId)));

            Assert.That(path, Does.StartWith("Assets/_Project/Art/CardVisuals/Artwork/Starcaller/"));
            Assert.That(path, Does.Not.Contain("NextPatch"),
                "Runtime must never depend on the temporary NextPatch intake folder.");
        }

        [Test]
        public void The_artwork_import_settings_match_the_established_production_convention()
        {
            string path = AssetDatabase.GetAssetPath(Library().ArtworkFor(new CoH.Core.Identifiers.CardId(CardId)));

            Assert.That(AssetImporter.GetAtPath(path), Is.TypeOf<TextureImporter>());
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
        }
    }
}
