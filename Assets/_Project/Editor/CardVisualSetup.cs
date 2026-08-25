using System.Collections.Generic;
using System.IO;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Builds the card composition assets: one recipe, one catalog, one artwork
    /// library and the factory that ties them together.
    ///
    /// The recipe's rectangles are the measurements the project's card layout
    /// has always used — an 800 by 1100 canvas with the mana gem top left, the
    /// name banner across the middle, the rules panel below it and the attack
    /// and health gems in the bottom corners. They were pixel coordinates
    /// buried in a scene builder before this; now they are the data a card is
    /// composed from, and moving a gem is moving a number.
    ///
    /// The sprites it generates are scaffolding, and they are deliberately
    /// flat, grey and ugly so that nobody mistakes them for a decision about
    /// how the game should look. Their only job is to give the composer
    /// something real to resolve, so the architecture can be finished and
    /// tested before the intended artwork exists. Replacing them is replacing
    /// the sprite on a catalog entry: no code, no recipe change, no rebuild.
    /// </summary>
    public static class CardVisualSetup
    {
        private const string Root = "Assets/_Project/Art/CardVisuals";
        private const string SpriteFolder = Root + "/Placeholder";
        private const string AssetFolder = "Assets/_Project/Data/CardVisuals";

        private const string RecipePath = AssetFolder + "/CardVisualRecipe_Standard.asset";
        private const string CatalogPath = AssetFolder + "/CardVisualCatalog.asset";
        private const string LibraryPath = AssetFolder + "/CardVisualLibrary.asset";
        private const string FactoryPath = AssetFolder + "/CardVisualFactory.asset";

        /// <summary>Where the finished factory lives, for whatever needs to load it.</summary>
        public static string FactoryAssetPath => FactoryPath;

        [MenuItem("Conquest of Hearthstone/Rebuild Card Visuals")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(SpriteFolder);
            Directory.CreateDirectory(AssetFolder);

            CardVisualRecipeAsset recipe = BuildRecipe();
            CardVisualCatalogAsset catalog = BuildCatalog();
            CardVisualLibraryAsset library = BuildLibrary();

            CardVisualFactory factory = Load<CardVisualFactory>(FactoryPath);
            factory.Wire(new[] { recipe }, catalog, library);

            EditorUtility.SetDirty(factory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<string> problems = new List<string>();
            factory.Validate(problems);

            if (problems.Count == 0)
            {
                Debug.Log(
                    "Card visuals rebuilt: " + recipe.Layers.Count + " layers, " +
                    catalog.Entries.Count + " catalog entries.");
                return;
            }

            Debug.LogWarning("Card visuals rebuilt with " + problems.Count + " problem(s):\n - " +
                string.Join("\n - ", problems));
        }

        // ------------------------------------------------------------------
        //  The recipe
        // ------------------------------------------------------------------

        private static CardVisualRecipeAsset BuildRecipe()
        {
            CardVisualRecipeAsset recipe = Load<CardVisualRecipeAsset>(RecipePath);

            List<CardVisualLayerDefinition> layers = new List<CardVisualLayerDefinition>
            {
                // --- the back of the card ---------------------------------
                Picture("CardBack", CardVisualSlot.CardBack, 120, 0, 0, 800, 1100,
                    face: CardVisualFace.FaceDown, required: true),

                // --- the front, back to front -----------------------------
                Picture("Backdrop", CardVisualSlot.Backdrop, 0, 0, 0, 800, 1100),

                // Under the frame, because the frame is a window rather than a
                // picture: it has a hole in it and the painting shows through.
                Picture("Artwork", CardVisualSlot.Artwork, 10, 186, 185, 434, 420,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.HasArtwork) }),

                // The one layer a card cannot do without. Everything else is
                // allowed to be missing; a card with no frame is not a card.
                Picture("Frame", CardVisualSlot.Frame, 20, 66, 92, 669, 1007, required: true),

                Picture("EliteFrame", CardVisualSlot.EliteFrame, 30, 40, 60, 720, 1075,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.IsElite) }),

                // Optional on purpose: a finished frame usually draws its own
                // banner and panel, and then these two simply find nothing and
                // are skipped. That is not a gap, it is the frame doing the job.
                Picture("NameBanner", CardVisualSlot.NameBanner, 40, 92, 572, 624, 159),

                Picture("RulesPanel", CardVisualSlot.RulesPanel, 50, 113, 718, 580, 341,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.HasRulesText) }),

                Picture("ManaGem", CardVisualSlot.ManaGem, 60, 25, 106, 195, 197,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.ShowsCost) }),

                Picture("AttackGem", CardVisualSlot.AttackGem, 70, 8, 885, 210, 215,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.ShowsStatistics) }),

                Picture("HealthGem", CardVisualSlot.HealthGem, 80, 582, 885, 210, 215,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.ShowsStatistics) }),

                // A basic card wears no rarity stone, which is a rule about
                // rarity and therefore a condition rather than a missing asset.
                Picture("RarityGem", CardVisualSlot.RarityGem, 90, 347, 663, 122, 92,
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.Rarity, CardVisualComparison.NotEquals,
                            (int)Core.Cards.Rarity.Free)
                    }),

                Picture("TribeBanner", CardVisualSlot.TribeBanner, 100, 145, 975, 511, 97,
                    conditions: new[] { CardVisualCondition.True(CardVisualField.HasTribe) }),

                // Nothing fills this slot yet, and nothing has to: a card
                // simply has no set symbol until one exists.
                Picture("ExpansionEmblem", CardVisualSlot.ExpansionEmblem, 110, 360, 890, 80, 80),

                // --- the words --------------------------------------------
                Label("NameText", CardVisualTextSlot.Name, 130, 110, 590, 588, 122, 3.4f, bold: true),
                Label("ManaText", CardVisualTextSlot.ManaCost, 140, 25, 116, 195, 177, 7.5f, bold: true),
                Label("RulesText", CardVisualTextSlot.RulesText, 150, 150, 760, 500, 200, 2.1f,
                    tint: new Color(0.12f, 0.09f, 0.06f)),
                Label("AttackText", CardVisualTextSlot.Attack, 160, 8, 895, 210, 195, 7.5f, bold: true),
                Label("HealthText", CardVisualTextSlot.Health, 170, 582, 895, 210, 195, 7.5f, bold: true),
                Label("TribeText", CardVisualTextSlot.Tribe, 180, 145, 985, 511, 77, 2.1f)
            };

            recipe.Author(CardVisualStyle.Default, layers);
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static CardVisualLayerDefinition Picture(
            string name, CardVisualSlot slot, int order,
            float x, float y, float width, float height,
            CardVisualFace face = CardVisualFace.FaceUp,
            bool required = false,
            CardVisualCondition[] conditions = null) =>
            new CardVisualLayerDefinition
            {
                name = name,
                slot = slot,
                text = CardVisualTextSlot.None,
                face = face,
                sortingOrder = order,
                x = x,
                y = y,
                width = width,
                height = height,
                required = required,
                tint = Color.white,
                conditions = conditions ?? System.Array.Empty<CardVisualCondition>()
            };

        private static CardVisualLayerDefinition Label(
            string name, CardVisualTextSlot slot, int order,
            float x, float y, float width, float height, float fontSize,
            bool bold = false, Color? tint = null) =>
            new CardVisualLayerDefinition
            {
                name = name,
                slot = CardVisualSlot.None,
                text = slot,
                face = CardVisualFace.FaceUp,
                sortingOrder = order,
                x = x,
                y = y,
                width = width,
                height = height,
                fontSize = fontSize,
                bold = bold,
                tint = tint ?? Color.white,
                conditions = System.Array.Empty<CardVisualCondition>()
            };

        // ------------------------------------------------------------------
        //  The catalog
        // ------------------------------------------------------------------

        private static CardVisualCatalogAsset BuildCatalog()
        {
            CardVisualCatalogAsset catalog = Load<CardVisualCatalogAsset>(CatalogPath);
            catalog.ClearEntries();

            // A frame per card type, and a neutral override on top of the
            // minion one. Two entries, and every combination of the two is
            // already answered: that is the whole override mechanism.
            catalog.AddEntry(Entry(CardVisualSlot.Frame, Frame("Frame_Minion", 0.55f, 0.38f, 0.21f),
                type: Core.Cards.CardType.Minion));
            catalog.AddEntry(Entry(CardVisualSlot.Frame, Frame("Frame_Spell", 0.24f, 0.34f, 0.55f),
                type: Core.Cards.CardType.Spell));
            catalog.AddEntry(Entry(CardVisualSlot.Frame, Frame("Frame_Weapon", 0.30f, 0.40f, 0.32f),
                type: Core.Cards.CardType.Weapon));

            // The default, for any type with no frame of its own. Without this
            // a hero card would compose with a hole where its frame goes, and
            // the report would say so rather than the card looking odd.
            catalog.AddEntry(Entry(CardVisualSlot.Frame, Frame("Frame_Default", 0.38f, 0.34f, 0.30f)));

            catalog.AddEntry(Entry(CardVisualSlot.Backdrop, Solid("Backdrop", 0.05f, 0.04f, 0.03f, 0.75f)));
            catalog.AddEntry(Entry(CardVisualSlot.CardBack, Solid("CardBack", 0.18f, 0.13f, 0.26f, 1f)));

            catalog.AddEntry(Entry(CardVisualSlot.ManaGem, Disc("Gem_Mana", 0.16f, 0.42f, 0.85f)));
            catalog.AddEntry(Entry(CardVisualSlot.AttackGem, Disc("Gem_Attack", 0.78f, 0.62f, 0.18f)));
            catalog.AddEntry(Entry(CardVisualSlot.HealthGem, Disc("Gem_Health", 0.74f, 0.18f, 0.18f)));

            catalog.AddEntry(Entry(CardVisualSlot.NameBanner, Solid("Banner_Name", 0.42f, 0.28f, 0.14f, 1f)));
            catalog.AddEntry(Entry(CardVisualSlot.RulesPanel, Solid("Panel_Rules", 0.88f, 0.82f, 0.68f, 1f)));
            catalog.AddEntry(Entry(CardVisualSlot.TribeBanner, Solid("Banner_Tribe", 0.30f, 0.22f, 0.12f, 1f)));

            // A stone per rarity, which is the whole of rarity support. No
            // frame, no prefab and no branch anywhere: four rows in a table.
            catalog.AddEntry(Entry(CardVisualSlot.RarityGem, Disc("Rarity_Common", 0.72f, 0.74f, 0.78f),
                rarity: Core.Cards.Rarity.Common));
            catalog.AddEntry(Entry(CardVisualSlot.RarityGem, Disc("Rarity_Rare", 0.22f, 0.42f, 0.86f),
                rarity: Core.Cards.Rarity.Rare));
            catalog.AddEntry(Entry(CardVisualSlot.RarityGem, Disc("Rarity_Epic", 0.62f, 0.28f, 0.80f),
                rarity: Core.Cards.Rarity.Epic));
            catalog.AddEntry(Entry(CardVisualSlot.RarityGem, Disc("Rarity_Legendary", 0.92f, 0.70f, 0.16f),
                rarity: Core.Cards.Rarity.Legendary));

            // An overlay rather than a second frame. A legendary in Hearthstone
            // keeps its frame and gains a dragon around the gem; a scaffolding
            // sprite that painted over the whole card would misrepresent that
            // and hide the very layering this is here to demonstrate.
            catalog.AddEntry(Entry(CardVisualSlot.EliteFrame, Border("Frame_Elite", 0.92f, 0.70f, 0.16f),
                rarity: Core.Cards.Rarity.Legendary));

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CardVisualEntry Entry(
            CardVisualSlot slot, Sprite sprite,
            Core.Cards.CardType? type = null,
            Core.Cards.CardClass? cardClass = null,
            Core.Cards.Rarity? rarity = null)
        {
            CardVisualMatch match = new CardVisualMatch
            {
                constrainType = type.HasValue,
                type = type ?? Core.Cards.CardType.None,
                constrainClass = cardClass.HasValue,
                cardClass = cardClass ?? Core.Cards.CardClass.Neutral,
                constrainRarity = rarity.HasValue,
                rarity = rarity ?? Core.Cards.Rarity.Free,
                constrainTribe = false,
                style = default
            };

            return new CardVisualEntry
            {
                slot = slot,
                match = match,
                sprite = sprite,
                notes = "Placeholder. Replace the sprite; leave the row."
            };
        }

        // ------------------------------------------------------------------
        //  The artwork library
        // ------------------------------------------------------------------

        private static CardVisualLibraryAsset BuildLibrary()
        {
            CardVisualLibraryAsset library = Load<CardVisualLibraryAsset>(LibraryPath);
            library.SetFallbackArtwork(Solid("Artwork_Placeholder", 0.26f, 0.33f, 0.42f, 1f));

            EditorUtility.SetDirty(library);
            return library;
        }

        // ------------------------------------------------------------------
        //  Scaffolding sprites
        // ------------------------------------------------------------------

        private static Sprite Solid(string name, float r, float g, float b, float a)
        {
            return Make(name, 128, 176, (x, y, width, height) => new Color(r, g, b, a));
        }

        private static Sprite Disc(string name, float r, float g, float b)
        {
            return Make(name, 128, 128, (x, y, width, height) =>
            {
                float dx = (x + 0.5f) / width - 0.5f;
                float dy = (y + 0.5f) / height - 0.5f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance > 0.5f)
                {
                    return Color.clear;
                }

                float edge = distance > 0.42f ? 0.55f : 1f;
                return new Color(r * edge, g * edge, b * edge, 1f);
            });
        }

        // Where the frame layer and the artwork layer sit on the card canvas.
        // The scaffolding frame needs both, because its hole has to line up
        // with the painting it is a window onto.
        private static readonly Rect FrameRect = new Rect(66f, 92f, 669f, 1007f);
        private static readonly Rect ArtworkRect = new Rect(186f, 185f, 434f, 420f);

        /// <summary>
        /// A border with a hole in it, which is what a card frame actually is.
        ///
        /// Solid would hide the artwork underneath and quietly turn the layer
        /// order into a lie, so the scaffolding is transparent where the real
        /// frame is transparent.
        ///
        /// The hole is worked out in the frame's own space rather than the
        /// card's, which is the whole of the arithmetic: the sprite is drawn
        /// into the frame's rectangle, so a window described in canvas
        /// coordinates would land somewhere else entirely.
        /// </summary>
        private static Sprite Frame(string name, float r, float g, float b)
        {
            float left = (ArtworkRect.xMin - FrameRect.xMin) / FrameRect.width;
            float right = (ArtworkRect.xMax - FrameRect.xMin) / FrameRect.width;

            // Texture coordinates run up the image and canvas coordinates run
            // down it, so the top of the window is the larger value.
            float top = 1f - (ArtworkRect.yMin - FrameRect.yMin) / FrameRect.height;
            float bottom = 1f - (ArtworkRect.yMax - FrameRect.yMin) / FrameRect.height;

            return Make(name, 128, 176, (x, y, width, height) =>
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                bool insideWindow = u > left && u < right && v > bottom && v < top;
                return insideWindow ? Color.clear : new Color(r, g, b, 1f);
            });
        }

        /// <summary>A thin outline, transparent everywhere else.</summary>
        private static Sprite Border(string name, float r, float g, float b)
        {
            return Make(name, 128, 176, (x, y, width, height) =>
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                const float thickness = 0.035f;

                bool onEdge =
                    u < thickness || u > 1f - thickness ||
                    v < thickness || v > 1f - thickness;

                return onEdge ? new Color(r, g, b, 1f) : Color.clear;
            });
        }

        private delegate Color Shade(int x, int y, int width, int height);

        private static Sprite Make(string name, int width, int height, Shade shade)
        {
            string path = SpriteFolder + "/" + name + ".png";

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, shade(x, y, width, height));
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }
    }
}
