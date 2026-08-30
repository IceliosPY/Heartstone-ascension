using System.Collections.Generic;
using System.Text;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Brings authored visual data up to the current contracts.
    ///
    /// Two things are migrated, both because their identity changed rather than
    /// their meaning: layers gained a permanent id distinct from their label,
    /// and the adjustments that used to name a layer by that label now name the
    /// id. Doing it as a command rather than by hand-editing the asset means it
    /// is repeatable, reportable, and can be run again on data authored
    /// elsewhere.
    ///
    /// Idempotent. A layer that already has an id keeps it, and a row that
    /// already names an id is left alone, so running this twice is the same as
    /// running it once.
    /// </summary>
    public static class CardVisualMigration
    {
        [MenuItem("Conquest of Hearthstone/Migrate Card Visual Data")]
        public static void Migrate()
        {
            CardVisualFactory factory =
                AssetDatabase.LoadAssetAtPath<CardVisualFactory>(CardVisualSetup.FactoryAssetPath);

            if (factory == null)
            {
                Debug.LogError("No card visual factory at " + CardVisualSetup.FactoryAssetPath + ".");
                return;
            }

            List<string> done = new List<string>();

            bool changed = Run(factory, done);

            if (changed)
            {
                for (int index = 0; index < factory.Recipes.Count; index++)
                {
                    if (factory.Recipes[index] != null)
                    {
                        EditorUtility.SetDirty(factory.Recipes[index]);
                    }
                }

                if (factory.Library != null)
                {
                    EditorUtility.SetDirty(factory.Library);
                }

                AssetDatabase.SaveAssets();
            }

            Debug.Log(done.Count == 0
                ? "Card visual data is already up to date; nothing was changed."
                : "Card visual data migrated:\n - " + string.Join("\n - ", done));
        }

        /// <summary>
        /// Does the work, reporting each change. Separated from the command so
        /// tests can run it against assets they build themselves.
        /// </summary>
        public static bool Run(CardVisualFactory factory, List<string> done)
        {
            if (factory == null)
            {
                return false;
            }

            bool changed = false;

            for (int index = 0; index < factory.Recipes.Count; index++)
            {
                changed |= AssignLayerIds(factory.Recipes[index], done);
            }

            changed |= RetargetOverrides(factory, done);

            return changed;
        }

        /// <summary>
        /// Gives every layer a permanent id, derived once from its label.
        ///
        /// Derived, not equal: the slug is only how the first value is chosen,
        /// and the two are independent from that moment on. That is the whole
        /// point - a label is for reading and an id is for pointing at, and the
        /// bug being designed out is a rename quietly orphaning authored data.
        /// </summary>
        public static bool AssignLayerIds(CardVisualRecipeAsset recipe, List<string> done)
        {
            if (recipe == null)
            {
                return false;
            }

            HashSet<string> taken = new HashSet<string>(System.StringComparer.Ordinal);

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer != null && layer.HasStableId)
                {
                    taken.Add(layer.id);
                }
            }

            bool changed = false;

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null || layer.HasStableId)
                {
                    continue;
                }

                string wanted = Unique(Slug(layer.name), taken);

                layer.id = wanted;
                taken.Add(wanted);
                changed = true;

                done?.Add(recipe.name + ": layer '" + layer.name + "' given id '" + wanted + "'.");
            }

            return changed;
        }

        /// <summary>
        /// Points every saved adjustment at a layer id rather than a label.
        ///
        /// A row already naming an id is left alone. A row naming a label that
        /// exactly one layer, across every recipe, currently carries is
        /// rewritten to that layer's id. Everything else is left exactly as it
        /// is and reported, for two different reasons:
        ///
        ///   the label matches no layer at all - authored data nobody can
        ///   interpret, and quietly discarding it would be worse than leaving
        ///   it for the validator to complain about;
        ///
        ///   the label matches *more than one* layer - "NameText", "RulesText"
        ///   and "Frame" are exactly the kind of label a recipe repeats once
        ///   for a minion and once for a spell, and picking one of several
        ///   candidates is not a migration, it is a guess that silently
        ///   attaches a card's adjustment to the wrong layer. Guessing here is
        ///   worse than doing nothing, because nothing at least fails loudly.
        /// </summary>
        public static bool RetargetOverrides(CardVisualFactory factory, List<string> done)
        {
            if (factory?.Library == null)
            {
                return false;
            }

            Dictionary<string, List<string>> candidatesByLabel =
                new Dictionary<string, List<string>>(System.StringComparer.Ordinal);

            HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);

            for (int recipeIndex = 0; recipeIndex < factory.Recipes.Count; recipeIndex++)
            {
                CardVisualRecipeAsset recipe = factory.Recipes[recipeIndex];

                if (recipe == null)
                {
                    continue;
                }

                for (int index = 0; index < recipe.Layers.Count; index++)
                {
                    CardVisualLayerDefinition layer = recipe.Layers[index];

                    if (layer == null)
                    {
                        continue;
                    }

                    ids.Add(layer.LayerId);

                    if (string.IsNullOrEmpty(layer.name))
                    {
                        continue;
                    }

                    if (!candidatesByLabel.TryGetValue(layer.name, out List<string> candidates))
                    {
                        candidates = new List<string>();
                        candidatesByLabel[layer.name] = candidates;
                    }

                    // The same layer counted twice - the same recipe asked for
                    // twice, say - is one candidate, not two.
                    if (!candidates.Contains(layer.LayerId))
                    {
                        candidates.Add(layer.LayerId);
                    }
                }
            }

            bool changed = false;

            foreach (CardVisualBinding binding in factory.Library.Cards)
            {
                if (binding?.overrides == null)
                {
                    continue;
                }

                foreach (CardVisualPropertyOverride row in binding.overrides.Properties)
                {
                    if (row == null || ids.Contains(row.layer))
                    {
                        continue;
                    }

                    if (!candidatesByLabel.TryGetValue(row.layer, out List<string> candidates) ||
                        candidates.Count == 0)
                    {
                        done?.Add(
                            "LEFT ALONE - " + binding.cardId + ": adjustment to '" + row.property +
                            "' names layer '" + row.layer + "', which is neither a layer id nor a " +
                            "layer label in any recipe. Nothing was guessed; the validator reports it.");
                    }
                    else if (candidates.Count == 1)
                    {
                        string id = candidates[0];

                        done?.Add(
                            binding.cardId + ": adjustment to '" + row.layer + "'.'" + row.property +
                            "' now names layer id '" + id + "'.");

                        row.layer = id;
                        changed = true;
                    }
                    else
                    {
                        done?.Add(
                            "LEFT ALONE, AMBIGUOUS - " + binding.cardId + ": adjustment to '" +
                            row.property + "' names layer '" + row.layer + "', which " +
                            candidates.Count + " layers across the recipes answer to (" +
                            string.Join(", ", candidates) + "). Picking one would silently attach " +
                            "this adjustment to the wrong layer, so nothing was changed - repoint " +
                            "it by hand to whichever of those ids is the right one.");
                    }
                }
            }

            return changed;
        }

        /// <summary>"NameText (other)" becomes "nametext-other".</summary>
        public static string Slug(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return "layer";
            }

            StringBuilder text = new StringBuilder(label.Length);
            bool pendingBreak = false;

            for (int index = 0; index < label.Length; index++)
            {
                char letter = label[index];

                if (char.IsLetterOrDigit(letter))
                {
                    if (pendingBreak && text.Length > 0)
                    {
                        text.Append('-');
                    }

                    text.Append(char.ToLowerInvariant(letter));
                    pendingBreak = false;
                }
                else
                {
                    pendingBreak = true;
                }
            }

            return text.Length == 0 ? "layer" : text.ToString();
        }

        private static string Unique(string wanted, ICollection<string> taken)
        {
            if (!taken.Contains(wanted))
            {
                return wanted;
            }

            for (int suffix = 2; suffix < 1000; suffix++)
            {
                string candidate = wanted + "-" + suffix;

                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            return wanted + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
