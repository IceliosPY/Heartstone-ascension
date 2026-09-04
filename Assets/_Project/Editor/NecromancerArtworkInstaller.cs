using System.IO;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Binds the four final Necromancer summon artworks into the shared
    /// <see cref="CardVisualLibraryAsset"/> - the same seam every other
    /// card's own artwork is resolved from
    /// (<see cref="CardVisualLibraryAsset.ArtworkFor"/>, read by
    /// <c>CardVisualFactory</c> at runtime and by <c>CardVisualSelection</c>
    /// in the editor tool). Nothing card-id-specific is added anywhere
    /// else: Raise's choice cards, a hand card and Card Visual Editor V2 all
    /// already go through this one binding, so populating it is the whole
    /// integration.
    ///
    /// <c>Assets/_Project/Art/CardVisuals/NextPatch</c> is a temporary
    /// intake folder only - nothing at runtime is allowed to depend on it.
    /// This installer reads the four selected files from there once, copies
    /// them to their permanent production home under
    /// <c>Assets/_Project/Art/CardVisuals/Artwork/Necromancer/</c>, and
    /// binds the library from the copies.
    /// </summary>
    public static class NecromancerArtworkInstaller
    {
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";
        private const string ArtworkFolder = "Assets/_Project/Art/CardVisuals/Artwork/Necromancer/";

        private static readonly (string cardId, string fileName)[] Bindings =
        {
            ("necromancer_skeletal_warrior", "Skeletal_Warrior.png"),
            ("necromancer_skeletal_rogue", "Skeletal_Rogue.png"),
            ("necromancer_crypt_fiend", "Crypt_Fiend.png"),
            ("necromancer_abomination", "Abomination.png")
        };

        [MenuItem("Conquest of Hearthstone/Bind Necromancer Summon Artwork")]
        public static void Install()
        {
            CardVisualLibraryAsset library = AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);

            if (library == null)
            {
                Debug.LogError("No card visual library at " + LibraryAssetPath + ". Nothing was changed.");
                return;
            }

            bool boundAny = false;

            foreach ((string cardId, string fileName) in Bindings)
            {
                string path = ArtworkFolder + fileName;

                if (!File.Exists(path))
                {
                    Debug.LogError("Missing artwork for " + cardId + " at " + path + ". Nothing was bound for it.");
                    continue;
                }

                EnsureArtworkImportSettings(path);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (sprite == null)
                {
                    Debug.LogError(path + " did not import as a Sprite. Nothing was bound for " + cardId + ".");
                    continue;
                }

                library.Set(cardId, sprite);
                boundAny = true;
            }

            if (boundAny)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                Debug.Log("Necromancer summon artwork bound into " + LibraryAssetPath + ".");
            }
        }

        /// <summary>
        /// The same standard artwork settings every other bound card
        /// artwork already uses (see <c>the_coin.png</c>'s own import
        /// settings) - a plain Sprite, alpha preserved, no mipmaps, not
        /// read/write enabled, full source resolution kept up to 2048px.
        /// Deliberately not the Hero Power centre-art exception
        /// (<c>HeroPowerSceneInstaller.EnsureCenterArtImportSettings</c>):
        /// that trilinear/uncompressed/mipmapped treatment was a fix for
        /// one specific heavily-minified image, not the artwork norm.
        /// </summary>
        private static void EnsureArtworkImportSettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
