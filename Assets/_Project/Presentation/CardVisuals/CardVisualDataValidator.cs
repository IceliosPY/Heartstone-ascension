using System;
using System.Collections.Generic;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Checks authored visual data against the contracts the rest of the system
    /// relies on, and says plainly what is wrong.
    ///
    /// Everything here describes a failure that is otherwise silent. An
    /// adjustment naming a layer nobody has still loads, still serialises and
    /// still shows up in the editor's count - it simply never reaches a card.
    /// A colour saved where a number belongs reads back as zero and is
    /// indistinguishable from an authored zero. A layer with no id is fine
    /// until somebody renames it. None of these throw, none of them log, and
    /// all of them look like the tool having no effect.
    ///
    /// So the rule is: never turn malformed authored data into a plausible
    /// default without saying so. Resolution still falls back, because a card
    /// that half-draws is better than a card that throws in a match - but the
    /// fallback is reportable, and the editor and the tests both ask.
    /// </summary>
    public static class CardVisualDataValidator
    {
        /// <summary>Everything wrong with a factory's recipes and its library.</summary>
        public static void Validate(CardVisualFactory factory, List<string> problems)
        {
            if (problems == null)
            {
                return;
            }

            if (factory == null)
            {
                problems.Add("No card visual factory to validate.");
                return;
            }

            // The schema itself first: two properties claiming one id makes
            // every judgement below meaningless.
            foreach (string problem in CardVisualSchema.Problems)
            {
                problems.Add("Schema: " + problem);
            }

            HashSet<string> layerIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < factory.Recipes.Count; index++)
            {
                ValidateRecipe(factory.Recipes[index], layerIds, problems);
            }

            ValidateLibrary(factory.Library, layerIds, problems);
        }

        /// <summary>Layer identity: present, unique, and stable.</summary>
        public static void ValidateRecipe(
            CardVisualRecipeAsset recipe, HashSet<string> layerIds, List<string> problems)
        {
            if (recipe == null || problems == null)
            {
                return;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> styles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < recipe.TextStyles.Count; index++)
            {
                CardTextStyleDefinition style = recipe.TextStyles[index];

                if (style == null)
                {
                    problems.Add(recipe.name + ": text style " + index + " is empty.");
                    continue;
                }

                if (!styles.Add(style.name))
                {
                    problems.Add(
                        recipe.name + ": two text styles are called '" + style.name +
                        "'. A layer naming it reaches whichever comes first.");
                }
            }

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null)
                {
                    problems.Add(recipe.name + ": layer " + index + " is empty.");
                    continue;
                }

                if (!layer.HasStableId)
                {
                    problems.Add(
                        recipe.name + ": layer '" + layer.name + "' has no id, so its adjustments " +
                        "are keyed by its label and renaming it would orphan them. " +
                        "Run Migrate Card Visual Data.");
                }

                if (!seen.Add(layer.LayerId))
                {
                    problems.Add(
                        recipe.name + ": two layers answer to the id '" + layer.LayerId +
                        "' ('" + layer.name + "' is the second). Adjustments naming it would " +
                        "reach both.");
                }

                layerIds?.Add(layer.LayerId);

                if (layer.IsText &&
                    !string.IsNullOrEmpty(layer.textStyle) &&
                    recipe.FindTextStyle(layer.textStyle) == null)
                {
                    problems.Add(
                        recipe.name + ": layer '" + layer.name + "' is set in style '" +
                        layer.textStyle + "', which this recipe does not define.");
                }
            }
        }

        /// <summary>Adjustments: do they name anything, and do they mean anything.</summary>
        public static void ValidateLibrary(
            CardVisualLibraryAsset library, HashSet<string> layerIds, List<string> problems)
        {
            if (library == null || problems == null)
            {
                return;
            }

            foreach (CardVisualBinding binding in library.Cards)
            {
                if (binding?.overrides == null)
                {
                    continue;
                }

                string who = library.name + " / " + binding.cardId;
                HashSet<string> rows = new HashSet<string>(StringComparer.Ordinal);

                foreach (CardVisualPropertyOverride row in binding.overrides.Properties)
                {
                    if (row == null)
                    {
                        problems.Add(who + ": an empty adjustment row.");
                        continue;
                    }

                    if (!rows.Add(row.layer + "::" + row.property))
                    {
                        problems.Add(
                            who + ": '" + row.layer + "'.'" + row.property +
                            "' is adjusted twice. Only the first is ever read.");
                    }

                    if (layerIds != null && layerIds.Count > 0 && !layerIds.Contains(row.layer))
                    {
                        problems.Add(
                            who + ": adjusts layer '" + row.layer + "', which no recipe defines. " +
                            "The adjustment is stored and reaches nothing.");
                    }

                    ValidateRow(who, row, problems);
                }
            }
        }

        private static void ValidateRow(string who, CardVisualPropertyOverride row, List<string> problems)
        {
            CardVisualProperty property = CardVisualSchema.Find(row.property);

            if (property == null)
            {
                problems.Add(
                    who + ": adjusts '" + row.property + "', which is not a property anything " +
                    "answers to. It is stored and reaches nothing - a field renamed without a " +
                    "FormerIds entry does exactly this.");

                return;
            }

            if (!string.Equals(property.Id, row.property, StringComparison.Ordinal))
            {
                problems.Add(
                    who + ": adjusts '" + row.property + "', which is now called '" + property.Id +
                    "'. It still resolves through the alias; re-saving it writes the new id.");
            }

            if (!property.SupportsCardOverride)
            {
                problems.Add(
                    who + ": adjusts '" + row.property + "', which one card may not differ on (" +
                    property.Authorability + "). It is stored and reaches nothing.");

                return;
            }

            if (!row.value.Fits(property.Type))
            {
                problems.Add(
                    who + ": '" + row.property + "' is a " + property.Type.Name +
                    " but the value was stored as " + row.value.kind +
                    ". It reads back as an empty value rather than as what was authored.");

                return;
            }

            if (!row.value.IsDefinedFor(property.Type))
            {
                problems.Add(
                    who + ": '" + row.property + "' holds " + row.value.number +
                    ", which " + property.Type.Name + " does not define.");
            }
        }
    }
}
