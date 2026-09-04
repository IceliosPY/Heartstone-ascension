using System.IO;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Binds Lunar Phase's centre art into the shared
    /// <see cref="CardVisualLibraryAsset"/> - the exact seam Raise's own
    /// centre art already goes through (see
    /// <c>HeroPowerSceneInstaller.Install</c>). The medallion itself needs
    /// no second instance and no Starcaller-specific presentation code: the
    /// single <c>HeroPowerView</c> already rebinds its Frame, ManaGem and
    /// CenterArt fresh from whichever player is "near" every refresh, all
    /// three already class-agnostic (see <c>HeroPowerView.Bind</c>). Once
    /// this one binding exists, Lunar Phase's medallion appears the moment
    /// Player 2's turn - and their real <c>starcaller_lunar_phase</c> hero
    /// power - is near, with nothing else built or wired.
    /// </summary>
    public static class StarcallerArtworkInstaller
    {
        private const string LibraryAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset";
        private const string LunarPhaseCenterArtPath = "Assets/_Project/Art/HeroPowers/CenterArt/LunarPhase_CenterArt.png";
        private const string LunarPhaseCardId = "starcaller_lunar_phase";

        [MenuItem("Conquest of Hearthstone/Bind Starcaller Hero Power Artwork")]
        public static void Install()
        {
            EnsureCenterArtImportSettings(LunarPhaseCenterArtPath);

            CardVisualLibraryAsset library = AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(LibraryAssetPath);

            if (library == null)
            {
                Debug.LogError("No card visual library at " + LibraryAssetPath + ". Nothing was changed.");
                return;
            }

            Sprite centerArt = AssetDatabase.LoadAssetAtPath<Sprite>(LunarPhaseCenterArtPath);

            if (centerArt == null)
            {
                Debug.LogError(
                    "No Lunar Phase centre art at " + LunarPhaseCenterArtPath + ". Nothing was bound.");
                return;
            }

            library.Set(LunarPhaseCardId, centerArt);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log("Lunar Phase centre art bound into " + LibraryAssetPath + ".");
        }

        /// <summary>
        /// The same treatment Raise's own centre art needed
        /// (<c>HeroPowerSceneInstaller.EnsureCenterArtImportSettings</c>),
        /// applied here rather than shared, since that method is private to
        /// its own installer: a Hero Power medallion draws its centre art at
        /// roughly a tenth of the source image's own size, and bilinear
        /// sampling with no mip chain under that much minification aliases
        /// and blurs. Kept local to this one asset rather than becoming
        /// every artwork's default, for the same reason the original fix
        /// was - most artwork is never minified anywhere near this hard.
        /// </summary>
        private static void EnsureCenterArtImportSettings(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

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

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
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

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (settings.filterMode != FilterMode.Trilinear)
            {
                settings.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            importer.SetTextureSettings(settings);

            TextureImporterPlatformSettings defaultPlatform = importer.GetDefaultPlatformTextureSettings();

            if (defaultPlatform.format != TextureImporterFormat.RGBA32 ||
                defaultPlatform.textureCompression != TextureImporterCompression.Uncompressed)
            {
                defaultPlatform.format = TextureImporterFormat.RGBA32;
                defaultPlatform.textureCompression = TextureImporterCompression.Uncompressed;
                defaultPlatform.overridden = true;
                importer.SetPlatformTextureSettings(defaultPlatform);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
