using System.Collections.Generic;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The migration from label-keyed adjustments to id-keyed ones, on data
    /// built here rather than on the project's own recipe.
    ///
    /// The one behaviour worth a dedicated file: a label is not a safe key to
    /// migrate by, because labels repeat on purpose. "NameText", "RulesText"
    /// and "Frame" are exactly the shape of label a recipe writes once for a
    /// minion and once for a spell, and a migration that picks one of several
    /// candidates is not migrating, it is guessing which layer a card's
    /// adjustment belongs to - silently, and with a fifty-fifty chance of
    /// being wrong. Every test here is either a label that resolves cleanly or
    /// one that does not, and the second kind must come back untouched.
    /// </summary>
    public sealed class CardVisualMigrationTests
    {
        private static CardVisualLayerDefinition Layer(string label) =>
            new CardVisualLayerDefinition { name = label };

        private static CardVisualRecipeAsset Recipe(params CardVisualLayerDefinition[] layers)
        {
            CardVisualRecipeAsset recipe = ScriptableObject.CreateInstance<CardVisualRecipeAsset>();
            recipe.Author(CardVisualStyle.Default, layers);
            return recipe;
        }

        private static CardVisualFactory Factory(
            CardVisualLibraryAsset library, params CardVisualRecipeAsset[] recipes)
        {
            CardVisualFactory factory = ScriptableObject.CreateInstance<CardVisualFactory>();
            factory.Wire(new List<CardVisualRecipeAsset>(recipes), null, library);
            return factory;
        }

        private static void DestroyAll(Object library, params Object[] rest)
        {
            Object.DestroyImmediate(library);

            for (int index = 0; index < rest.Length; index++)
            {
                if (rest[index] != null)
                {
                    Object.DestroyImmediate(rest[index]);
                }
            }
        }

        // ------------------------------------------------------------------
        //  1. A label that names exactly one layer migrates
        // ------------------------------------------------------------------

        [Test]
        public void A_unique_old_label_migrates_to_its_layers_id()
        {
            CardVisualLayerDefinition layer = Layer("UniqueLabel");
            CardVisualRecipeAsset recipe = Recipe(layer);
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("card_a").Set("UniqueLabel", "layer.y", CardVisualValue.Of(123f));

                CardVisualFactory factory = Factory(library, recipe);
                List<string> done = new List<string>();

                bool changed = CardVisualMigration.Run(factory, done);

                Assert.That(changed, Is.True);
                Assert.That(layer.HasStableId, Is.True);

                CardVisualOverrides overrides = library.OverridesFor(new CoH.Core.Identifiers.CardId("card_a"));

                Assert.That(overrides.TryGet(layer.LayerId, "layer.y", out CardVisualValue moved), Is.True,
                    "The adjustment did not follow the label to the layer's new id.\n" +
                    string.Join("\n", done));

                Assert.That(moved.number, Is.EqualTo(123f));
                Assert.That(overrides.Overrides("UniqueLabel", "layer.y"), Is.False,
                    "The row under the old label was migrated, not moved.");
            }
            finally
            {
                DestroyAll(library, recipe);
            }
        }

        // ------------------------------------------------------------------
        //  2 & 3. A label naming more than one layer is left alone, loudly
        // ------------------------------------------------------------------

        [Test]
        public void A_duplicate_old_label_does_not_migrate()
        {
            CardVisualLayerDefinition first = Layer("DuplicateLabel");
            CardVisualLayerDefinition second = Layer("DuplicateLabel");
            CardVisualRecipeAsset recipe = Recipe(first, second);
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("card_b")
                    .Set("DuplicateLabel", "layer.width", CardVisualValue.Of(456f));

                CardVisualFactory factory = Factory(library, recipe);
                List<string> done = new List<string>();

                CardVisualMigration.Run(factory, done);

                // Both layers now have their own id - ambiguity is about which
                // one the *adjustment* meant, not about assigning ids at all.
                Assert.That(first.LayerId, Is.Not.EqualTo(second.LayerId));

                CardVisualOverrides overrides = library.OverridesFor(new CoH.Core.Identifiers.CardId("card_b"));

                Assert.That(overrides.Overrides("DuplicateLabel", "layer.width"), Is.True,
                    "A label naming two layers was migrated to one of them anyway.");

                Assert.That(overrides.Overrides(first.LayerId, "layer.width"), Is.False);
                Assert.That(overrides.Overrides(second.LayerId, "layer.width"), Is.False);
            }
            finally
            {
                DestroyAll(library, recipe);
            }
        }

        [Test]
        public void A_duplicate_old_label_produces_an_explicit_ambiguity_diagnostic()
        {
            CardVisualLayerDefinition first = Layer("DuplicateLabel");
            CardVisualLayerDefinition second = Layer("DuplicateLabel");
            CardVisualRecipeAsset recipe = Recipe(first, second);
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("card_b")
                    .Set("DuplicateLabel", "layer.width", CardVisualValue.Of(456f));

                CardVisualFactory factory = Factory(library, recipe);
                List<string> done = new List<string>();

                CardVisualMigration.Run(factory, done);

                Assert.That(done, Has.Some.Contains("AMBIGUOUS"),
                    "Nothing reported that the label matched more than one layer.\n" +
                    string.Join("\n", done));

                string diagnostic = done.Find(line => line.Contains("AMBIGUOUS"));

                Assert.That(diagnostic, Does.Contain("card_b"), "The diagnostic did not name the card.");
                Assert.That(diagnostic, Does.Contain("DuplicateLabel"),
                    "The diagnostic did not name the old label.");
                Assert.That(diagnostic, Does.Contain(first.LayerId),
                    "The diagnostic did not list one of the candidate ids.");
                Assert.That(diagnostic, Does.Contain(second.LayerId),
                    "The diagnostic did not list the other candidate id.");
            }
            finally
            {
                DestroyAll(library, recipe);
            }
        }

        // ------------------------------------------------------------------
        //  4. Idempotence: a second pass changes nothing further
        // ------------------------------------------------------------------

        [Test]
        public void A_second_migration_pass_makes_no_further_change()
        {
            CardVisualLayerDefinition unique = Layer("UniqueLabel");
            CardVisualLayerDefinition first = Layer("DuplicateLabel");
            CardVisualLayerDefinition second = Layer("DuplicateLabel");
            CardVisualRecipeAsset recipe = Recipe(unique, first, second);
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("card_a").Set("UniqueLabel", "layer.y", CardVisualValue.Of(1f));
                library.EstablishOverrides("card_b")
                    .Set("DuplicateLabel", "layer.width", CardVisualValue.Of(2f));

                CardVisualFactory factory = Factory(library, recipe);

                bool firstChanged = CardVisualMigration.Run(factory, new List<string>());
                Assert.That(firstChanged, Is.True);

                string uniqueId = unique.LayerId;
                string firstId = first.LayerId;
                string secondId = second.LayerId;

                List<string> second_pass = new List<string>();
                bool secondChanged = CardVisualMigration.Run(factory, second_pass);

                Assert.That(secondChanged, Is.False,
                    "A second pass over already-migrated data still reported a change:\n" +
                    string.Join("\n", second_pass));

                Assert.That(unique.LayerId, Is.EqualTo(uniqueId));
                Assert.That(first.LayerId, Is.EqualTo(firstId));
                Assert.That(second.LayerId, Is.EqualTo(secondId));

                CardVisualOverrides overridesA =
                    library.OverridesFor(new CoH.Core.Identifiers.CardId("card_a"));

                Assert.That(overridesA.TryGet(uniqueId, "layer.y", out CardVisualValue value), Is.True);
                Assert.That(value.number, Is.EqualTo(1f));

                // The ambiguous row is still exactly where the first pass left
                // it - untouched, not "untouched differently".
                CardVisualOverrides overridesB =
                    library.OverridesFor(new CoH.Core.Identifiers.CardId("card_b"));

                Assert.That(overridesB.Overrides("DuplicateLabel", "layer.width"), Is.True);
            }
            finally
            {
                DestroyAll(library, recipe);
            }
        }

        // ------------------------------------------------------------------
        //  5. A layer that already has a stable id is never touched
        // ------------------------------------------------------------------

        [Test]
        public void A_layer_with_an_existing_id_keeps_it_and_its_rows_are_left_alone()
        {
            CardVisualLayerDefinition layer = new CardVisualLayerDefinition
            {
                id = "already-stable",
                name = "AmbiguousLabel"
            };

            // A second layer sharing the *label* would ordinarily make that
            // label ambiguous - proving that a row already naming a real id is
            // never routed through the label lookup at all, regardless.
            CardVisualLayerDefinition other = Layer("AmbiguousLabel");

            CardVisualRecipeAsset recipe = Recipe(layer, other);
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("card_c")
                    .Set("already-stable", "layer.y", CardVisualValue.Of(9f));

                CardVisualFactory factory = Factory(library, recipe);
                List<string> done = new List<string>();

                CardVisualMigration.Run(factory, done);

                Assert.That(layer.id, Is.EqualTo("already-stable"),
                    "A layer's existing, stable id was rewritten.");

                CardVisualOverrides overrides = library.OverridesFor(new CoH.Core.Identifiers.CardId("card_c"));

                Assert.That(overrides.TryGet("already-stable", "layer.y", out CardVisualValue value), Is.True);
                Assert.That(value.number, Is.EqualTo(9f));

                foreach (string line in done)
                {
                    Assert.That(line, Does.Not.Contain("card_c"),
                        "An already-stable row was reported as though it needed migrating: " + line);
                }
            }
            finally
            {
                DestroyAll(library, recipe);
            }
        }
    }
}
