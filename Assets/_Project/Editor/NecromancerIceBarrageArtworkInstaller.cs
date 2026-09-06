using System.IO;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Binds Ice Barrage's final artwork into the shared
    /// <see cref="CardVisualLibraryAsset"/> - the same seam every other
    /// card's own artwork is resolved from (see
    /// <c>StarcallerHuntressShotArtworkInstaller</c>, which this mirrors
    /// exactly). Nothing card-id-specific is added anywhere else.
    ///
    /// <c>Assets/_Project/Art/CardVisuals/NextPatch</c> is a temporary
    /// intake folder only - nothing at runtime is allowed to depend on it.
    /// The file has already been copied to its permanent production home
    /// under <c>Assets/_Project/Art/CardVisuals/Artwork/Necromancer/</c>;
    /// this installer binds the library from that copy.
    /// </summary>
    public static class NecromancerIceBarrageArtworkInstaller
    {
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";
        private const string ArtworkPath = "Assets/_Project/Art/CardVisuals/Artwork/Necromancer/Ice_Barrage.png";
        private const string CardId = "necromancer_ice_barrage";

        [MenuItem("Conquest of Hearthstone/Bind Ice Barrage Artwork")]
        public static void Install()
        {
            CardVisualLibraryAsset library = AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);

            if (library == null)
            {
                Debug.LogError("No card visual library at " + LibraryAssetPath + ". Nothing was changed.");
                return;
            }

            if (!File.Exists(ArtworkPath))
            {
                Debug.LogError("Missing artwork for " + CardId + " at " + ArtworkPath + ". Nothing was bound.");
                return;
            }

            EnsureArtworkImportSettings(ArtworkPath);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);

            if (sprite == null)
            {
                Debug.LogError(ArtworkPath + " did not import as a Sprite. Nothing was bound for " + CardId + ".");
                return;
            }

            library.Set(CardId, sprite);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log("Ice Barrage artwork bound into " + LibraryAssetPath + ".");
        }

        /// <summary>
        /// The same standard artwork settings every other bound card
        /// artwork already uses (see <c>NecromancerArtworkInstaller</c>'s
        /// own copy of this method) - a plain Sprite, alpha preserved, no
        /// mipmaps, not read/write enabled, full source resolution kept up
        /// to 2048px.
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
