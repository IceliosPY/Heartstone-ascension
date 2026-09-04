using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Registers the Necromancer's own Minion frame in the card visual
    /// catalog, the same way <c>CardVisualImport</c> registers every
    /// HearthCards component - a slot, a match, a sprite - except this frame
    /// is our own authored art, not a HearthCards download, so it is added
    /// directly rather than through that importer's manifest.
    ///
    /// The match is class and type only: <see cref="CardType.Minion"/> +
    /// <see cref="CardClass.Necromancer"/>. Nothing here names a servant, a
    /// card id or the Necromancer's hero power - any Necromancer minion,
    /// present or future, resolves this the moment it exists, through the
    /// same specificity scoring every other frame in the catalog already
    /// uses. Neutral's own frame entry is untouched and keeps drawing every
    /// card of every other class, exactly as before.
    /// </summary>
    public static class NecromancerMinionFrameInstaller
    {
        private const string CatalogAssetPath = "Assets/_Project/Data/CardVisuals/CardVisualCatalog.asset";

        /// <summary>
        /// Our own authored Necromancer Minion frame - not a HearthCards
        /// asset, so it lives under the project's own art folder rather than
        /// <c>Assets/ThirdParty/HearthCards</c>.
        /// </summary>
        private const string FrameAssetPath =
            "Assets/_Project/Art/CardVisuals/Frames/Necromancer/Card_Inhand_Minion_Necromancer.png";

        [MenuItem("Conquest of Hearthstone/Install Necromancer Minion Frame")]
        public static void Install()
        {
            EnsureSpriteImportSettings(FrameAssetPath);

            CardVisualCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<CardVisualCatalogAsset>(CatalogAssetPath);

            if (catalog == null)
            {
                Debug.LogError(
                    "No card visual catalog at " + CatalogAssetPath + ". Run Conquest of Hearthstone -> " +
                    "Create Missing Card Visual Assets first, then run this again.");

                return;
            }

            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FrameAssetPath);

            if (frame == null)
            {
                Debug.LogError("No sprite could be loaded at " + FrameAssetPath + ". Nothing was changed.");
                return;
            }

            CardVisualMatch match = new CardVisualMatch
            {
                constrainType = true,
                type = CardType.Minion,
                constrainClass = true,
                cardClass = CardClass.Necromancer
            };

            catalog.SetSprite(CardVisualSlot.Frame, match, frame, "Authored Necromancer Minion frame");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "Necromancer Minion frame registered: CardVisualSlot.Frame for CardType.Minion + " +
                "CardClass.Necromancer.");
        }

        /// <summary>
        /// The same settings every imported Minion frame already uses
        /// (<c>CardVisualImport.LoadAsSprite</c>): a card frame reads at
        /// close to its native size, so it needs none of the mipmap/trilinear
        /// treatment Raise's centre art needed for its much heavier
        /// minification.
        /// </summary>
        private static void EnsureSpriteImportSettings(string path)
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
