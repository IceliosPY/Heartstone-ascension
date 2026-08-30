using System;
using System.Collections.Generic;
using System.IO;
using CoH.Core.Cards;
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

        /// <summary>Where the catalog lives, for whatever fills it in.</summary>
        public static string CatalogAssetPath => CatalogPath;

        /// <summary>
        /// Creates whatever baseline assets are missing, and leaves everything
        /// that already exists exactly as authored.
        ///
        /// This used to rebuild the recipe from the scaffolding below every
        /// time it ran, which was reasonable while the recipe *was* the
        /// scaffolding and became dangerous the moment the Card Visual Editor
        /// made it the authored source of truth. Every rectangle, font size and
        /// curve in the recipe is now somebody's work, and a command called
        /// "rebuild" sitting in a menu is not an acceptable way to lose it.
        ///
        /// So the safe command is this one, and it is the one on the menu. It
        /// will not touch a recipe that already has layers, or a catalog that
        /// already has entries. The scaffolding is still there, and still
        /// reachable, behind <see cref="ReplaceAuthoredData"/> - which says
        /// what it does and asks first.
        /// </summary>
        [MenuItem("Conquest of Hearthstone/Create Missing Card Visual Assets")]
        public static void Rebuild() => Run(replaceAuthored: false);

        /// <summary>
        /// Throws the authored recipe and catalog away and writes the
        /// scaffolding over them.
        ///
        /// Kept because starting again is occasionally the right thing, and
        /// deleting the scaffolding would mean a new project could not be
        /// bootstrapped at all. It asks first, it says what it is about to
        /// destroy, and its name does not pretend to be maintenance.
        /// </summary>
        [MenuItem("Conquest of Hearthstone/Danger - Replace Authored Card Visuals With Scaffolding")]
        public static void ReplaceAuthoredData()
        {
            CardVisualRecipeAsset existing = AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(RecipePath);
            int layers = existing == null ? 0 : existing.Layers.Count;

            bool confirmed = EditorUtility.DisplayDialog(
                "Replace authored card visuals?",
                "This throws away the authored recipe (" + layers + " layers - every rectangle, " +
                "font size, curve and outline in it) and the authored catalog, and writes the " +
                "built-in scaffolding over them." + Break +
                "Per-card adjustments in the library are not touched, but any that name a layer " +
                "the scaffolding does not recreate will be left pointing at nothing." + Break +
                "There is no undo.",
                "Replace them",
                "Cancel");

            if (!confirmed)
            {
                Debug.Log("Card visuals left alone.");
                return;
            }

            Run(replaceAuthored: true);
        }

        /// <summary>A blank line in a dialog, kept out of the string literals above.</summary>
        private static readonly string Break = Environment.NewLine + Environment.NewLine;

        private static void Run(bool replaceAuthored)
        {
            Directory.CreateDirectory(SpriteFolder);
            Directory.CreateDirectory(AssetFolder);

            CardVisualRecipeAsset recipe = BuildRecipe(replaceAuthored);
            CardVisualCatalogAsset catalog = BuildCatalog(replaceAuthored);
            CardVisualLibraryAsset library = BuildLibrary();

            CardVisualFactory factory = Load<CardVisualFactory>(FactoryPath);
            factory.Wire(new[] { recipe }, catalog, library);

            EditorUtility.SetDirty(factory);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<string> problems = new List<string>();
            factory.Validate(problems);

            // And the contracts the authoring tools depend on: layer identity,
            // and every saved adjustment naming something that exists.
            CardVisualDataValidator.Validate(factory, problems);

            if (problems.Count == 0)
            {
                Debug.Log(
                    "Card visuals rebuilt: " + recipe.Layers.Count + " layers, " +
                    catalog.Entries.Count + " catalog entries.");
            }
            else
            {
                Debug.LogWarning("Card visuals rebuilt with " + problems.Count + " problem(s):\n - " +
                    string.Join("\n - ", problems));
            }

            // Rebuilding starts the catalog again from scaffolding, so
            // anything already downloaded has to be laid back over the top
            // of it. Doing that here rather than leaving it to be
            // remembered is the difference between two commands that
            // compose and two commands where one silently undoes the other.
            CardVisualImport.Import();
        }

        // ------------------------------------------------------------------
        //  The recipe
        // ------------------------------------------------------------------

        private static CardVisualRecipeAsset BuildRecipe(bool replaceAuthored)
        {
            CardVisualRecipeAsset recipe = Load<CardVisualRecipeAsset>(RecipePath);

            // The one guard that matters. Everything below this line rebuilds
            // the layer list from scratch, and an authored recipe is the
            // project's source of truth for what a card looks like.
            if (recipe.Layers.Count > 0 && !replaceAuthored)
            {
                Debug.Log(
                    "The card visual recipe already has " + recipe.Layers.Count + " authored " +
                    "layers and was left untouched. Use the Card Visual Editor to change them, " +
                    "or the Danger menu item to start again from scaffolding.");

                return recipe;
            }

            HearthCardsManifestFile manifest = HearthCardsManifest.LoadOrEmpty();

            List<CardVisualLayerDefinition> layers = new List<CardVisualLayerDefinition>
            {
                // --- the back of the card ---------------------------------
                Picture("CardBack", CardVisualSlot.CardBack, 120, WholeCard,
                    face: CardVisualFace.FaceDown, required: true),

                // --- the front, back to front -----------------------------
                //
                // Every rectangle below comes from the renderer's own layer
                // template by way of the manifest, and a slot that differs by
                // card type is two layers rather than one layer that decides.
                // A minion frame is 669x1007 at y 92; a spell frame is 669x947
                // at y 150. One rectangle could not have been right for both,
                // and putting the choice in code would have made the composer
                // know what a spell is.
                Picture("Backdrop (minion)", CardVisualSlot.Backdrop, 0,
                    Rect(manifest, CardVisualSlot.Backdrop, CardType.Minion, WholeCard),
                    conditions: new[] { CardVisualCondition.Is(CardType.Minion) }),

                Picture("Backdrop (spell)", CardVisualSlot.Backdrop, 0,
                    Rect(manifest, CardVisualSlot.Backdrop, CardType.Spell, WholeCard),
                    conditions: new[] { CardVisualCondition.Is(CardType.Spell) }),

                // Under the frame, because a frame is a window rather than a
                // picture, and clipped to that window's shape. The rectangles
                // are the renderer's own art masks: an ellipse for a minion, a
                // rectangle for a spell.
                //
                // Cover rather than Stretch: a painting is whatever shape it
                // was painted, and squashing it into a window is never the
                // right answer. It fills the window, overflows, and the mask
                // crops the overflow.
                Picture("Artwork (spell)", CardVisualSlot.Artwork, 10, SpellArtwork,
                    fill: CardVisualFill.Cover,
                    maskSlot: CardVisualSlot.ArtworkMask,
                    conditions: new[]
                    {
                        CardVisualCondition.Is(CardType.Spell),
                        CardVisualCondition.True(CardVisualField.HasArtwork)
                    }),

                // Everything else, so a weapon or a hero shows its painting
                // rather than a hole.
                Picture("Artwork (other)", CardVisualSlot.Artwork, 10, MinionArtwork,
                    fill: CardVisualFill.Cover,
                    maskSlot: CardVisualSlot.ArtworkMask,
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell),
                        CardVisualCondition.True(CardVisualField.HasArtwork)
                    }),

                // The one layer a card cannot do without.
                Picture("Frame (spell)", CardVisualSlot.Frame, 20,
                    Rect(manifest, CardVisualSlot.Frame, CardType.Spell, MinionFrame),
                    required: true,
                    conditions: new[] { CardVisualCondition.Is(CardType.Spell) }),

                Picture("Frame (other)", CardVisualSlot.Frame, 20,
                    Rect(manifest, CardVisualSlot.Frame, CardType.Minion, MinionFrame),
                    required: true,
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell)
                    }),

                Picture("EliteFrame (minion)", CardVisualSlot.EliteFrame, 30,
                    Rect(manifest, CardVisualSlot.EliteFrame, CardType.Minion, MinionFrame),
                    conditions: new[]
                    {
                        CardVisualCondition.Is(CardType.Minion),
                        CardVisualCondition.True(CardVisualField.IsElite)
                    }),

                Picture("EliteFrame (spell)", CardVisualSlot.EliteFrame, 30,
                    Rect(manifest, CardVisualSlot.EliteFrame, CardType.Spell, MinionFrame),
                    conditions: new[]
                    {
                        CardVisualCondition.Is(CardType.Spell),
                        CardVisualCondition.True(CardVisualField.IsElite)
                    }),

                Picture("NameBanner (spell)", CardVisualSlot.NameBanner, 40,
                    Rect(manifest, CardVisualSlot.NameBanner, CardType.Spell, MinionNameBanner),
                    conditions: new[] { CardVisualCondition.Is(CardType.Spell) }),

                Picture("NameBanner (other)", CardVisualSlot.NameBanner, 40,
                    Rect(manifest, CardVisualSlot.NameBanner, CardType.Minion, MinionNameBanner),
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell)
                    }),

                Picture("RulesPanel (spell)", CardVisualSlot.RulesPanel, 50,
                    Rect(manifest, CardVisualSlot.RulesPanel, CardType.Spell, MinionRulesPanel),
                    conditions: new[]
                    {
                        CardVisualCondition.Is(CardType.Spell),
                        CardVisualCondition.True(CardVisualField.HasRulesText)
                    }),

                Picture("RulesPanel (other)", CardVisualSlot.RulesPanel, 50,
                    Rect(manifest, CardVisualSlot.RulesPanel, CardType.Minion, MinionRulesPanel),
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell),
                        CardVisualCondition.True(CardVisualField.HasRulesText)
                    }),

                // Shared: the spell template reuses the minion's mana gem, so
                // one rectangle and one file serve every card.
                Picture("ManaGem", CardVisualSlot.ManaGem, 60,
                    Rect(manifest, CardVisualSlot.ManaGem, CardType.Minion, MinionManaGem),
                    conditions: new[] { CardVisualCondition.True(CardVisualField.ShowsCost) }),

                Picture("AttackGem", CardVisualSlot.AttackGem, 70,
                    Rect(manifest, CardVisualSlot.AttackGem, CardType.Minion, MinionAttackGem),
                    conditions: new[] { CardVisualCondition.True(CardVisualField.ShowsStatistics) }),

                Picture("HealthGem", CardVisualSlot.HealthGem, 80,
                    Rect(manifest, CardVisualSlot.HealthGem, CardType.Minion, MinionHealthGem),
                    conditions: new[] { CardVisualCondition.True(CardVisualField.ShowsStatistics) }),

                // A basic card wears no rarity stone, which is a rule about
                // rarity and therefore a condition rather than a missing asset.
                Picture("RarityGem (spell)", CardVisualSlot.RarityGem, 90,
                    Rect(manifest, CardVisualSlot.RarityGem, CardType.Spell, MinionRarityGem),
                    conditions: new[]
                    {
                        CardVisualCondition.Is(CardType.Spell),
                        NotBasic
                    }),

                Picture("RarityGem (other)", CardVisualSlot.RarityGem, 90,
                    Rect(manifest, CardVisualSlot.RarityGem, CardType.Minion, MinionRarityGem),
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell),
                        NotBasic
                    }),

                Picture("TribeBanner", CardVisualSlot.TribeBanner, 100,
                    Rect(manifest, CardVisualSlot.TribeBanner, CardType.Minion, MinionTribeBanner),
                    conditions: new[] { CardVisualCondition.True(CardVisualField.HasTribe) }),

                // Nothing fills this slot: the renderer draws card fronts and
                // has no set symbol of its own. A card simply has none.
                Picture("ExpansionEmblem", CardVisualSlot.ExpansionEmblem, 110,
                    new Rect(360f, 890f, 80f, 80f)),

                // --- the words --------------------------------------------
                //
                // Each label sits on the component it belongs to, so moving a
                // gem moves its number with it.
                Label("NameText (spell)", CardVisualTextSlot.Name, 130, SpellNameText,
                    Ceiling, NameFloor, bold: true, wrap: false,
                    conditions: new[] { CardVisualCondition.Is(CardType.Spell) }),

                Label("NameText (other)", CardVisualTextSlot.Name, 130, MinionNameText,
                    Ceiling, NameFloor, bold: true, wrap: false,
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell)
                    }),

                Label("ManaText", CardVisualTextSlot.ManaCost, 140, ManaNumber,
                    Ceiling, NumberFloor, bold: true, wrap: false),

                Label("RulesText (spell)", CardVisualTextSlot.RulesText, 150, SpellRulesText,
                    RulesCeiling, RulesFloor, tint: RulesInk,
                    conditions: new[] { CardVisualCondition.Is(CardType.Spell) }),

                Label("RulesText (other)", CardVisualTextSlot.RulesText, 150, MinionRulesText,
                    RulesCeiling, RulesFloor, tint: RulesInk,
                    conditions: new[]
                    {
                        new CardVisualCondition(
                            CardVisualField.CardType, CardVisualComparison.NotEquals, (int)CardType.Spell)
                    }),

                Label("AttackText", CardVisualTextSlot.Attack, 160, AttackNumber,
                    Ceiling, NumberFloor, bold: true, wrap: false),

                Label("HealthText", CardVisualTextSlot.Health, 170, HealthNumber,
                    Ceiling, NumberFloor, bold: true, wrap: false),

                Label("TribeText", CardVisualTextSlot.Tribe, 180, TribeName,
                    Ceiling, TribeFloor, wrap: false)
            };

            recipe.Author(CardVisualStyle.Default, layers);
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        // The card space every rectangle is written in: the renderer's own
        // 800 x 1100 canvas, origin top left, y running down. No conversion is
        // needed anywhere, and every component image is exactly the size of its
        // rectangle, so nothing is scaled either.
        private static readonly Rect WholeCard = new Rect(0f, 0f, CardCanvas.Width, CardCanvas.Height);

        // Fallbacks, used only when the manifest has no measurement for a slot.
        private static readonly Rect MinionFrame = new Rect(66f, 92f, 669f, 1007f);
        // The art window, and its shape: an ellipse for a minion, a rectangle
        // for a spell. The painting fills these and is clipped to them.
        //
        // The minion ellipse is measured off the frame's own transparent hole
        // rather than taken from the template's artMask, which is smaller and
        // sits high. Theirs is a default crop for art a user can then pan and
        // zoom; ours has to fill the window on its own, and a painting floating
        // in the middle of a hole with shadow all round it looks unfinished
        // rather than deliberate.
        private static readonly Rect MinionArtwork = new Rect(198f, 120f, 409f, 563f);
        private static readonly Rect SpellArtwork = new Rect(140f, 195f, 525f, 400f);
        private static readonly Rect MinionNameBanner = new Rect(92f, 572f, 624f, 159f);
        private static readonly Rect MinionRulesPanel = new Rect(113f, 718f, 580f, 341f);
        private static readonly Rect MinionManaGem = new Rect(33f, 114f, 179f, 181f);
        private static readonly Rect MinionAttackGem = new Rect(0f, 893f, 222f, 245f);
        private static readonly Rect MinionHealthGem = new Rect(590f, 906f, 170f, 231f);
        private static readonly Rect MinionRarityGem = new Rect(347f, 663f, 122f, 92f);
        private static readonly Rect MinionTribeBanner = new Rect(145f, 975f, 511f, 97f);

        // Text size is decided by the box it has to fit in, not by a number
        // chosen to look right. The ceiling is deliberately far above anything
        // that will be used: it lets the box do the deciding, and a label only
        // stops shrinking at its floor, where it would rather overflow than
        // become unreadable.
        private const float Ceiling = 12f;
        // Low enough that a name half again as long as "Test Soldier" still
        // fits between the curls of the banner. A name does not wrap - a card
        // name on two lines is a mistake, not a layout - so shrinking is the
        // only room it has, and the floor is what decides how much.
        private const float NameFloor = 0.55f;
        // Two digits have to fit where one does. A ten mana card is ordinary
        // and a thirty health hero is not exotic either, so the floor is set by
        // the widest number a card can carry rather than by the prettiest.
        // Single digits are unaffected: they are limited by the box long before
        // they reach this.
        private const float NumberFloor = 1.6f;
        private const float RulesCeiling = 2.4f;

        // Low enough that a card with four lines of rules shrinks to fit rather
        // than wrapping past the edges of its parchment. A floor is a promise
        // about readability, and this is where that promise stops being one.
        private const float RulesFloor = 0.45f;
        private const float TribeFloor = 0.9f;

        // The numbers, each centred on the gem it belongs to and sized to a
        // little over half its height — which is where a Hearthstone number
        // sits, and leaves room for two digits without touching the rim.
        private static readonly Rect ManaNumber = new Rect(42f, 152f, 161f, 105f);
        private static readonly Rect AttackNumber = new Rect(20f, 958f, 178f, 115f);
        private static readonly Rect HealthNumber = new Rect(600f, 968f, 151f, 108f);
        private static readonly Rect TribeName = new Rect(220f, 996f, 360f, 56f);

        // Where the renderer prints a card's rules, from its templates'
        // descriptionBox: a box centred at y 880 for a minion and 877 for a
        // spell, 240 tall, starting at x 137 and x 162. Their box narrows
        // toward the bottom to clear the attack and health gems; ours is the
        // widest part of it, which is the same shape until the text is long
        // enough to reach the gems.
        private static readonly Rect MinionRulesText = new Rect(150f, 772f, 506f, 232f);
        private static readonly Rect SpellRulesText = new Rect(172f, 768f, 460f, 214f);

        // The name. The renderer sets it along a curve, which a flat label
        // cannot follow, so ours is centred on the banner's writing area
        // instead — inset from the scroll's curled ends.
        private static readonly Rect MinionNameText = new Rect(160f, 618f, 490f, 68f);
        private static readonly Rect SpellNameText = new Rect(150f, 618f, 510f, 72f);

        private static readonly Color RulesInk = new Color(0.12f, 0.09f, 0.06f);

        private static readonly CardVisualCondition NotBasic = new CardVisualCondition(
            CardVisualField.Rarity, CardVisualComparison.NotEquals, (int)Core.Cards.Rarity.Free);

        /// <summary>The measured rectangle for a slot and a type, or the fallback.</summary>
        private static Rect Rect(
            HearthCardsManifestFile manifest, CardVisualSlot slot, CardType type, Rect fallback) =>
            HearthCardsManifest.TryFindRect(manifest, slot, type, out Rect measured) ? measured : fallback;

        /// <summary>A rectangle pulled in from the edges of the thing it sits on.</summary>
        private static Rect Inset(Rect rect, float horizontal, float vertical) =>
            new Rect(
                rect.x + horizontal,
                rect.y + vertical,
                Mathf.Max(1f, rect.width - horizontal * 2f),
                Mathf.Max(1f, rect.height - vertical * 2f));

        private static CardVisualLayerDefinition Picture(
            string name, CardVisualSlot slot, int order, Rect rect,
            CardVisualFace face = CardVisualFace.FaceUp,
            bool required = false,
            CardVisualFill fill = CardVisualFill.Stretch,
            CardVisualSlot maskSlot = CardVisualSlot.None,
            CardVisualCondition[] conditions = null) =>
            new CardVisualLayerDefinition
            {
                name = name,
                slot = slot,
                text = CardVisualTextSlot.None,
                face = face,
                sortingOrder = order,
                x = rect.x,
                y = rect.y,
                width = rect.width,
                height = rect.height,
                required = required,
                fill = fill,
                maskSlot = maskSlot,
                tint = Color.white,
                conditions = conditions ?? System.Array.Empty<CardVisualCondition>()
            };

        private static CardVisualLayerDefinition Label(
            string name, CardVisualTextSlot slot, int order, Rect rect,
            float ceiling, float floor,
            bool bold = false, bool wrap = true, Color? tint = null,
            CardVisualCondition[] conditions = null) =>
            new CardVisualLayerDefinition
            {
                name = name,
                slot = CardVisualSlot.None,
                text = slot,
                face = CardVisualFace.FaceUp,
                sortingOrder = order,
                x = rect.x,
                y = rect.y,
                width = rect.width,
                height = rect.height,
                fontSize = ceiling,
                fontSizeMin = floor,
                bold = bold,
                wrap = wrap,
                alignment = CardVisualAlignment.Center,
                tint = tint ?? Color.white,
                conditions = conditions ?? System.Array.Empty<CardVisualCondition>()
            };

        // ------------------------------------------------------------------
        //  The catalog
        // ------------------------------------------------------------------

        private static CardVisualCatalogAsset BuildCatalog(bool replaceAuthored)
        {
            CardVisualCatalogAsset catalog = Load<CardVisualCatalogAsset>(CatalogPath);

            if (catalog.Entries.Count > 0 && !replaceAuthored)
            {
                Debug.Log(
                    "The card visual catalog already has " + catalog.Entries.Count +
                    " entries and was left untouched.");

                return catalog;
            }

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

            // The shape the artwork is clipped to. Ours rather than
            // HearthCards': a mask is geometry, not artwork, and the geometry
            // is stated in their template as an ellipse for a minion and a
            // rectangle for a spell.
            catalog.AddEntry(Entry(CardVisualSlot.ArtworkMask, Ellipse("Mask_Ellipse")));
            catalog.AddEntry(Entry(CardVisualSlot.ArtworkMask, RoundedRectangle("Mask_Rectangle"),
                type: Core.Cards.CardType.Spell));

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

        /// <summary>
        /// A filled ellipse, transparent outside it. Only the alpha is read.
        ///
        /// Kept a pixel clear of the edge so that a painting scaled up past the
        /// window has somewhere transparent to be clamped against: without that
        /// border the outermost row would repeat outwards and the crop would
        /// smear instead of stopping.
        /// </summary>
        private static Sprite Ellipse(string name)
        {
            return Make(name, 256, 256, (x, y, width, height) =>
            {
                float dx = (x + 0.5f) / width - 0.5f;
                float dy = (y + 0.5f) / height - 0.5f;

                // A hair under a half, so the shape never touches the border.
                float inside = dx * dx + dy * dy <= 0.2465f ? 1f : 0f;
                return new Color(1f, 1f, 1f, inside);
            });
        }

        /// <summary>A filled rectangle with softened corners, transparent outside.</summary>
        private static Sprite RoundedRectangle(string name)
        {
            return Make(name, 256, 256, (x, y, width, height) =>
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                const float margin = 0.008f;
                const float radius = 0.04f;

                bool outside = u < margin || u > 1f - margin || v < margin || v > 1f - margin;

                if (outside)
                {
                    return new Color(1f, 1f, 1f, 0f);
                }

                // Corners, measured from the nearest one.
                float cornerX = Mathf.Min(u - margin, 1f - margin - u);
                float cornerY = Mathf.Min(v - margin, 1f - margin - v);

                if (cornerX < radius && cornerY < radius)
                {
                    float dx = radius - cornerX;
                    float dy = radius - cornerY;

                    if (dx * dx + dy * dy > radius * radius)
                    {
                        return new Color(1f, 1f, 1f, 0f);
                    }
                }

                return Color.white;
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
            UnityEngine.Object.DestroyImmediate(texture);

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
