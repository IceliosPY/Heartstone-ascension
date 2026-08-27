using System.Collections.Generic;
using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Fills the card visual catalog from downloaded components.
    ///
    /// The manifest already says where each file belongs on a card — its slot,
    /// and which cards it applies to — so importing is reading that file rather
    /// than wiring anything by hand. Adding a component later is adding a line
    /// to the manifest, fetching it and running this again: no code, here or
    /// anywhere else.
    ///
    /// It runs on top of the placeholders rather than instead of them, and it
    /// adds rather than overwrites unless the constraints are identical. That
    /// distinction matters: the scaffolding frame is authored for a card type,
    /// while a real one is a particular class's frame, so importing the neutral
    /// minion frame does not claim to be every minion frame. It becomes the
    /// more specific row and the scaffolding stays underneath it as the
    /// fallback for a class nobody has drawn yet.
    ///
    /// So the catalog is never full of holes, the game always draws, and no
    /// card silently ends up with a grey rectangle. Which rows are real and
    /// which are still standing in is what the report says.
    ///
    /// The mapping is the same one the composer uses and nothing else:
    ///
    ///     slot + card type + class + rarity  ->  sprite
    ///
    /// There is no card id in the manifest, no place to put one, and a test
    /// that fails if one appears.
    /// </summary>
    public static class CardVisualImport
    {
        [MenuItem("Conquest of Hearthstone/Import HearthCards Components")]
        public static void Import()
        {
            CardVisualCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<CardVisualCatalogAsset>(CardVisualSetup.CatalogAssetPath);

            if (catalog == null)
            {
                Debug.LogError(
                    "There is no card visual catalog. Run Conquest of Hearthstone -> Rebuild Card Visuals first.");
                return;
            }

            if (!HearthCardsManifest.TryLoad(out HearthCardsManifestFile manifest))
            {
                return;
            }

            List<string> imported = new List<string>();
            List<string> waiting = new List<string>();

            foreach (HearthCardsEntry entry in HearthCardsManifest.Entries(manifest))
            {
                if (!entry.TryReadSlot(out CardVisualSlot slot))
                {
                    waiting.Add(entry.id + ": '" + entry.slot + "' is not a card visual slot.");
                    continue;
                }

                string path = HearthCardsManifest.ImportedPathOf(manifest, entry);

                if (!File.Exists(path))
                {
                    waiting.Add(entry.id + ": no file at " + path);
                    continue;
                }

                Sprite sprite = LoadAsSprite(path);

                if (sprite == null)
                {
                    waiting.Add(entry.id + ": " + path + " could not be read as a sprite.");
                    continue;
                }

                CardVisualMatch match = entry.Match();
                catalog.SetSprite(slot, match, sprite, "Imported from " + entry.filename);

                imported.Add(slot + "  " + match.Describe() + "  <- " + Path.GetFileName(path));
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report(imported, waiting);
        }

        // ------------------------------------------------------------------
        //  Importing one file
        // ------------------------------------------------------------------

        /// <summary>
        /// Makes sure Unity reads the file as a sprite, then loads it.
        ///
        /// Alpha matters more than anything else here: a card frame is mostly
        /// transparent, and an import that flattened it would fill the window
        /// the artwork shows through.
        /// </summary>
        private static Sprite LoadAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
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

                // A frame is over a thousand pixels tall and the default ceiling
                // would quietly halve it.
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

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------

        private static void Report(List<string> imported, List<string> waiting)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();

            text.AppendLine(
                "HearthCards components: " + imported.Count + " imported, " +
                waiting.Count + " still on scaffolding.");

            if (imported.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("IMPORTED");

                for (int index = 0; index < imported.Count; index++)
                {
                    text.AppendLine("  " + imported[index]);
                }
            }

            if (waiting.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("STILL PLACEHOLDER");

                for (int index = 0; index < waiting.Count; index++)
                {
                    text.AppendLine("  " + waiting[index]);
                }

                text.AppendLine();
                text.AppendLine(
                    "Fetch them with Tools/HearthCards/fetch_card_assets.py, then run this again.");
            }

            Debug.Log(text.ToString());
        }

    }
}
