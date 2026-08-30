using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Shows what the card visual editor is for, in pictures and in numbers.
    ///
    /// A window cannot be photographed without a screen, so what is captured
    /// here is the window's viewport — its stage, its framing, its painter —
    /// which is the part worth looking at. The rest of the window is chrome
    /// around these pictures.
    ///
    /// The series that matters is the inheritance one. It retunes the type
    /// profile and shows two cards follow while a third, which asked for its own
    /// value, does not. That is the whole architecture in six stills, and the
    /// numbers logged beside them are what make it a measurement rather than an
    /// impression.
    ///
    /// Nothing here is saved. The recipe is changed in memory and put back.
    /// </summary>
    public static class CardVisualEditorDemo
    {
        private const string Folder = "CardCaptures";

        /// <summary>The layer the demonstration retunes, and the property.</summary>
        private const string TitleLayerLabel = "NameText (other)";
        private const string TitleY = "layer.y";

        [MenuItem("Conquest of Hearthstone/Capture Editor V2 Demonstration")]
        public static void Capture()
        {
            CardVisualFactory factory =
                AssetDatabase.LoadAssetAtPath<CardVisualFactory>(CardVisualSetup.FactoryAssetPath);

            if (factory == null)
            {
                Debug.LogError("No card visual factory.");
                return;
            }

            Directory.CreateDirectory(Folder);

            List<string> log = new List<string>();

            ReportTheSchema(log);
            CaptureTheThreeLooks(log);
            CaptureInheritance(factory, log);

            Debug.Log("Editor V2 demonstration:\n" + string.Join("\n", log));
        }

        /// <summary>
        /// What the editor found to edit, without being told any of it.
        ///
        /// Printed because the number is the claim: nothing lists these
        /// anywhere, so whatever appears here appeared by adding a field.
        /// </summary>
        private static void ReportTheSchema(List<string> log)
        {
            Report(CardVisualPropertyOwner.Layer, "Layer", log);
            Report(CardVisualPropertyOwner.Style, "Text style", log);
        }

        private static void Report(CardVisualPropertyOwner owner, string what, List<string> log)
        {
            IReadOnlyList<CardVisualProperty> found = CardVisualSchema.For(owner);

            int adjustable = 0;

            for (int index = 0; index < found.Count; index++)
            {
                if (found[index].SupportsCardOverride)
                {
                    adjustable++;
                }
            }

            log.Add(what + ": " + found.Count + " editable properties, " +
                    adjustable + " of them adjustable per card.");

            for (int index = 0; index < found.Count; index++)
            {
                CardVisualProperty property = found[index];

                log.Add("  " + property.Id.PadRight(26) +
                        property.Type.Name.PadRight(22) + "  " +
                        (property.SupportsCardOverride ? "per card" : "profile only"));
            }
        }

        // ------------------------------------------------------------------
        //  The three ways of looking at one composition
        // ------------------------------------------------------------------

        private static void CaptureTheThreeLooks(List<string> log)
        {
            CardVisualDescriptor minion = Minion("Gizath of the Hive", 0, null);

            Shot(minion, "v2-look-general-minion", log);

            Shot(minion, "v2-look-hand-rest", log, 900, inHand: true);
            Shot(minion, "v2-look-hand-hover", log, 900, inHand: true, hovered: true);
            Shot(minion, "v2-look-hand-rest-dimmed", log, 900, inHand: true, dimmed: true);

            CardVisualDescriptor spell = new CardVisualDescriptor(
                CardType.Spell, CardClass.Neutral, Rarity.Rare, Tribe.None,
                artwork: null, name: "Arcane Volley",
                rulesText: "Deal 1 damage to all enemy minions.",
                manaCost: 3, attack: 0, health: 0,
                showsCost: true, showsStatistics: false);

            Shot(spell, "v2-look-general-spell", log);
        }

        // ------------------------------------------------------------------
        //  What the whole design is for
        // ------------------------------------------------------------------

        /// <summary>
        /// Three cards, one of which asks for its own title height.
        ///
        /// Captured before and after the type profile is retuned. The two that
        /// asked for nothing move; the one that asked stays where it asked to
        /// be, and keeps inheriting everything it did not mention.
        /// </summary>
        private static void CaptureInheritance(CardVisualFactory factory, List<string> log)
        {
            CardVisualRecipeAsset recipe = factory.RecipeFor(CardVisualStyle.Default);

            CardVisualLayerDefinition title = null;

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                if (recipe.Layers[index].name == TitleLayerLabel)
                {
                    title = recipe.Layers[index];
                }
            }

            if (title == null)
            {
                Debug.LogError("No layer called " + TitleLayerLabel + " to demonstrate with.");
                return;
            }

            float authored = title.y;

            // One card, one row: this one wants its title lower than its kind.
            CardVisualOverrides ownValue = new CardVisualOverrides();
            ownValue.Set(title.LayerId, TitleY, CardVisualValue.Of(authored + 44f));

            log.Add(string.Empty);
            log.Add("Inheritance. Profile " + title.LayerId + "." + TitleY +
                    " authored at " + Number(authored) +
                    "; one card of three overrides it to " + Number(authored + 44f) +
                    " (" + ownValue.Count + " row).");

            try
            {
                Series(factory, "before", authored, ownValue, log);

                // Retune the kind, the way somebody would in Type profile scope.
                title.y = authored - 52f;

                log.Add(string.Empty);
                log.Add("Profile retuned to " + Number(title.y) + ".");

                Series(factory, "after", title.y, ownValue, log);
            }
            finally
            {
                // Never saved, never marked dirty, always put back.
                title.y = authored;
            }
        }

        private static void Series(
            CardVisualFactory factory,
            string when,
            float profile,
            CardVisualOverrides ownValue,
            List<string> log)
        {
            Measure(factory, "Inherits (Alpha)", Minion("Grovekeeper Alpha", 0, null), profile, log);
            Measure(factory, "Overrides (Beta)", Minion("Grovekeeper Beta", 1, ownValue), profile, log);
            Measure(factory, "Inherits (Gamma)", Minion("Grovekeeper Gamma", 2, null), profile, log);

            Shot(Minion("Grovekeeper Alpha", 0, null), "v2-inherits-alpha-" + when, log);
            Shot(Minion("Grovekeeper Beta", 1, ownValue), "v2-overrides-beta-" + when, log);
            Shot(Minion("Grovekeeper Gamma", 2, null), "v2-inherits-gamma-" + when, log);
        }

        /// <summary>
        /// Reads back where the title actually landed, from the composed plan.
        ///
        /// The picture shows it and the number proves it. Reading the plan
        /// rather than the recipe is the point: it is the value after the whole
        /// chain — defaults, profile, that card's own adjustments — has run.
        /// </summary>
        private static void Measure(
            CardVisualFactory factory,
            string what,
            in CardVisualDescriptor card,
            float profile,
            List<string> log)
        {
            CardVisualPlan plan = new CardVisualPlan();
            factory.Compose(card, plan);

            for (int index = 0; index < plan.Layers.Count; index++)
            {
                CardVisualPlannedLayer layer = plan.Layers[index];

                if (layer.LayerName != TitleLayerLabel)
                {
                    continue;
                }

                bool follows = Mathf.Abs(layer.Rect.y - profile) < 0.01f;

                log.Add("  " + what.PadRight(18) +
                        " title y = " + Number(layer.Rect.y) +
                        "   (profile says " + Number(profile) + ")" +
                        (follows ? string.Empty : "   <- its own"));

                return;
            }

            log.Add("  " + what.PadRight(18) + " no title layer composed.");
        }

        private static void Shot(
            in CardVisualDescriptor card,
            string name,
            List<string> log,
            int width = 420,
            bool inHand = false,
            bool hovered = false,
            bool dimmed = false)
        {
            string path = Path.Combine(Folder, name + ".png");

            CardVisualEditorWindow.Capture(card, path, width, inHand, hovered, dimmed);

            log.Add(path);
        }

        private static CardVisualDescriptor Minion(string name, int variation, CardVisualOverrides own) =>
            new CardVisualDescriptor(
                CardType.Minion,
                CardClass.Neutral,
                Rarity.Common,
                Tribe.None,
                artwork: null,
                name: name,
                rulesText: "Battlecry: Deal 2 damage to an enemy character.",
                manaCost: 3 + variation,
                attack: 3,
                health: 4 + variation,
                showsCost: true,
                showsStatistics: true,
                style: default,
                secondaryClass: CardClass.Neutral,
                expansion: string.Empty,
                faceDown: false,
                overrides: own);

        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
