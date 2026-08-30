using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The contracts the card visual system relies on, each one written after a
    /// way of breaking it silently.
    ///
    /// Every failure guarded here has the same shape: authored data that loads,
    /// serialises and shows up in the editor, and never reaches a card. An
    /// override the runtime drops on the floor. A layer renamed out from under
    /// the rows that pointed at it. A property offered for editing that nothing
    /// downstream reads. None of them throw and none of them log, so none of
    /// them are found by using the tool - only by measuring it.
    /// </summary>
    public sealed class CardVisualContractTests
    {
        private const string FactoryPath = "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset";
        private const string RecipePath = "Assets/_Project/Data/CardVisuals/CardVisualRecipe_Standard.asset";

        private static CardVisualFactory Factory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(FactoryPath);
            Assert.That(factory, Is.Not.Null, "No card visual factory.");
            return factory;
        }

        private static CardVisualRecipeAsset Recipe()
        {
            CardVisualRecipeAsset recipe = AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(RecipePath);
            Assert.That(recipe, Is.Not.Null, "No standard recipe.");
            return recipe;
        }

        /// <summary>The layer a minion's title is drawn by, whatever it is called today.</summary>
        private static CardVisualLayerDefinition TitleLayer()
        {
            CardVisualDescriptor minion = Minion("x");

            foreach (CardVisualLayerDefinition layer in Recipe().Layers)
            {
                if (layer != null && layer.text == CardVisualTextSlot.Name && layer.AppliesTo(minion))
                {
                    return layer;
                }
            }

            Assert.Fail("No title layer applies to a minion.");
            return null;
        }

        private static CardVisualLayerDefinition SpriteLayer()
        {
            CardVisualDescriptor minion = Minion("x");

            foreach (CardVisualLayerDefinition layer in Recipe().Layers)
            {
                if (layer != null && !layer.IsText &&
                    layer.slot != CardVisualSlot.None &&
                    layer.slot != CardVisualSlot.ArtworkMask &&
                    layer.AppliesTo(minion))
                {
                    return layer;
                }
            }

            Assert.Fail("No picture layer applies to a minion.");
            return null;
        }

        private static CardVisualDescriptor Minion(
            string name, CardVisualOverrides overrides = null) =>
            new CardVisualDescriptor(
                CardType.Minion, CardClass.Neutral, Rarity.Common, Tribe.None,
                artwork: null, name: name, rulesText: "Taunt.",
                manaCost: 2, attack: 2, health: 3,
                showsCost: true, showsStatistics: true,
                style: default, secondaryClass: CardClass.Neutral, expansion: "",
                faceDown: false, overrides: overrides);

        private static CardVisualPlannedLayer Find(CardVisualPlan plan, string layerName)
        {
            for (int index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].LayerName == layerName)
                {
                    return plan.Layers[index];
                }
            }

            Assert.Fail("The plan has no layer called '" + layerName + "'.");
            return default;
        }

        // ==================================================================
        //  1. The runtime path: library to plan, the way a match takes it
        // ==================================================================

        /// <summary>
        /// A real adjustment stored in a real library reaches a composed card
        /// through the factory's own runtime entry point.
        ///
        /// This is the bug that made the audit worth having.
        /// <see cref="CardVisualFactory.Describe"/> fetched the adjustments and
        /// handed them to <see cref="CardVisualDescriptor.FromViewModel"/>,
        /// which took the argument and never passed it on. Everything either
        /// side of that line worked, so the library held polish, the editor
        /// showed polish, and every card in a running match composed without
        /// any. Nothing failed.
        ///
        /// The test goes through <c>Describe</c> from a view model on purpose -
        /// building a descriptor by hand is exactly what the editor did, and it
        /// is why the editor could not see this.
        /// </summary>
        [Test]
        public void An_adjustment_in_the_library_reaches_a_card_composed_the_way_a_match_composes_one()
        {
            CardVisualLayerDefinition title = TitleLayer();

            CardVisualFactory factory = Factory();
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                factory.Wire(new List<CardVisualRecipeAsset>(factory.Recipes), factory.Catalog, library);

                const string id = "runtime_override_probe";
                float authored = title.y;

                CardViewModel model = new CardViewModel(
                    new EntityId(1), new CardId(id), "Runtime Probe",
                    2, 2, 3, "Taunt.",
                    CardType.Minion, CardClass.Neutral, Tribe.None, Rarity.Common, true);

                // Before: nothing authored for this card.
                CardVisualPlan plain = new CardVisualPlan();
                factory.Compose(factory.Describe(model), plain);

                Assert.That(Find(plain, title.name).Rect.y, Is.EqualTo(authored).Within(0.001f));

                // One sparse row, stored the way the editor stores it.
                library.EstablishOverrides(id)
                    .Set(title.LayerId, "layer.y", CardVisualValue.Of(authored + 37f));

                CardVisualDescriptor described = factory.Describe(model);

                Assert.That(described.Overrides, Is.Not.Null,
                    "The factory fetched the card's adjustments and the descriptor dropped them. " +
                    "Every polished card in a running match would compose unpolished.");

                CardVisualPlan polished = new CardVisualPlan();
                factory.Compose(described, polished);

                Assert.That(Find(polished, title.name).Rect.y,
                    Is.EqualTo(authored + 37f).Within(0.001f),
                    "The adjustment reached the descriptor but not the composed plan.");
            }
            finally
            {
                factory.Wire(new List<CardVisualRecipeAsset>(factory.Recipes), factory.Catalog,
                    AssetDatabase.LoadAssetAtPath<CardVisualLibraryAsset>(
                        "Assets/_Project/Data/CardVisuals/CardVisualLibrary.asset"));

                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        /// <summary>
        /// And a view showing that card paints it, rather than deciding nothing
        /// has changed.
        ///
        /// <see cref="CardVisualDescriptor.LooksTheSameAs"/> is the gate: it
        /// exists so a minion being buffed re-letters two labels instead of
        /// re-resolving a stack of sprites, and it used to compare adjustments
        /// by reference. The editor edits one set of adjustments in place, so
        /// the description held from last time and the one offered now are the
        /// same object - reference-equal however much it changed - and the view
        /// would skip the recompose that was the whole point of the edit.
        /// </summary>
        [Test]
        public void A_view_notices_when_a_cards_adjustments_are_edited_underneath_it()
        {
            CardVisualLayerDefinition title = TitleLayer();
            CardVisualOverrides live = new CardVisualOverrides();

            CardVisualDescriptor before = Minion("Probe", live);

            Assert.That(before.LooksTheSameAs(Minion("Probe", live)), Is.True,
                "The same card described twice was reported as needing a full recompose.");

            // Edited in place, which is what the library hands out and what the
            // editor writes to.
            live.Set(title.LayerId, "layer.y", CardVisualValue.Of(title.y + 12f));

            Assert.That(before.LooksTheSameAs(Minion("Probe", live)), Is.False,
                "A card whose adjustments were just edited was reported as unchanged, so a " +
                "view would have skipped recomposing it.");

            // Two separate sets asking for the same thing still compose the
            // same card, and must not force a needless recompose.
            CardVisualOverrides copy = new CardVisualOverrides();
            copy.Set(title.LayerId, "layer.y", CardVisualValue.Of(title.y + 12f));

            Assert.That(Minion("Probe", live).LooksTheSameAs(Minion("Probe", copy)), Is.True,
                "Two identical sets of adjustments were treated as different cards.");
        }

        /// <summary>
        /// And the whole way to a painted card: the label a view puts on
        /// screen sits where the card's own adjustment asked for.
        ///
        /// The step past the plan. Everything above proves the value reaches
        /// the composed description of a card; this proves the real
        /// <see cref="CardView"/> and the real painter act on it, through the
        /// prefab the game uses rather than a painter assembled here.
        /// </summary>
        [Test]
        public void A_painted_card_view_puts_the_label_where_the_cards_adjustment_asked()
        {
            CardVisualLayerDefinition title = TitleLayer();

            GameObject stage = new GameObject("Runtime override probe")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                CoH.Editor.CardPreviewCard.Make(stage.transform, out GameObject card);

                CardView view = card.GetComponent<CardView>();

                Assert.That(view, Is.Not.Null, "The card prefab has no CardView.");

                view.Show(Minion("Runtime Probe"));

                float plain = LabelHeight(card, "Runtime Probe");

                CardVisualOverrides own = new CardVisualOverrides();
                own.Set(title.LayerId, "layer.y", CardVisualValue.Of(title.y + 60f));

                view.Show(Minion("Runtime Probe", own));

                float adjusted = LabelHeight(card, "Runtime Probe");

                // Canvas y runs down and local y runs up, so a larger authored
                // y puts the label lower on the card.
                Assert.That(adjusted, Is.LessThan(plain - 0.01f),
                    "A card's own adjustment reached the plan but not the painted card: the " +
                    "title was drawn at " + adjusted + " either way.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static float LabelHeight(GameObject card, string text)
        {
            foreach (TMPro.TextMeshPro label in card.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                if (label.text == text)
                {
                    return label.transform.localPosition.y;
                }
            }

            Assert.Fail("No painted label reads '" + text + "'.");
            return 0f;
        }

        // ==================================================================
        //  2. Layer identity
        // ==================================================================

        [Test]
        public void Every_layer_in_the_recipe_has_a_stable_id()
        {
            foreach (CardVisualLayerDefinition layer in Recipe().Layers)
            {
                Assert.That(layer.HasStableId, Is.True,
                    "Layer '" + layer.name + "' has no id, so its adjustments are keyed by its " +
                    "label and renaming it would orphan them.");
            }
        }

        [Test]
        public void Renaming_a_layers_label_does_not_move_its_adjustment()
        {
            CardVisualLayerDefinition title = TitleLayer();
            string label = title.name;
            float authored = title.y;

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.y", CardVisualValue.Of(authored + 21f));

            try
            {
                title.name = "Something Else Entirely";

                CardVisualPlan plan = new CardVisualPlan();
                Factory().Compose(Minion("Renamed", own), plan);

                Assert.That(Find(plan, title.name).Rect.y, Is.EqualTo(authored + 21f).Within(0.001f),
                    "Renaming a layer's label orphaned the adjustment that pointed at it.");
            }
            finally
            {
                title.name = label;
            }
        }

        [Test]
        public void Reordering_the_layers_does_not_move_an_adjustment()
        {
            CardVisualRecipeAsset recipe = Recipe();
            CardVisualLayerDefinition title = TitleLayer();

            float authored = title.y;
            int order = title.sortingOrder;

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.y", CardVisualValue.Of(authored + 14f));

            // The plan is sorted by depth, so moving a layer through the draw
            // order is the reordering that could plausibly confuse a positional
            // identity - which is exactly why identity is not positional.
            try
            {
                title.sortingOrder = 999;

                CardVisualPlan plan = new CardVisualPlan();
                Factory().Compose(Minion("Reordered", own), plan);

                Assert.That(Find(plan, title.name).Rect.y, Is.EqualTo(authored + 14f).Within(0.001f),
                    "Reordering the layers orphaned an adjustment.");
            }
            finally
            {
                title.sortingOrder = order;
            }

            Assert.That(recipe.Layers.Count, Is.GreaterThan(1));
        }

        /// <summary>
        /// And a reorder of the recipe's own list, not only of a sorting
        /// number - the same layer objects, in the reverse order the recipe
        /// stores them in. <c>sortingOrder</c> controls draw depth and is a
        /// property of a layer; this changes where in <c>Layers</c> the layer
        /// sits, which is the positional identity a label-keyed or an
        /// index-keyed scheme would have relied on and this one does not.
        /// </summary>
        [Test]
        public void Reordering_the_recipes_own_layer_list_does_not_move_an_adjustment()
        {
            CardVisualRecipeAsset recipe = Recipe();
            CardVisualLayerDefinition title = TitleLayer();
            float authored = title.y;

            List<CardVisualLayerDefinition> original = new List<CardVisualLayerDefinition>(recipe.Layers);

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.y", CardVisualValue.Of(authored + 22f));

            try
            {
                List<CardVisualLayerDefinition> reversed = new List<CardVisualLayerDefinition>(original);
                reversed.Reverse();
                recipe.Author(recipe.Style, reversed);

                Assert.That(recipe.Layers[0], Is.Not.SameAs(original[0]),
                    "The list was not actually reordered.");

                CardVisualPlan plan = new CardVisualPlan();
                Factory().Compose(Minion("List-reordered", own), plan);

                Assert.That(Find(plan, title.name).Rect.y, Is.EqualTo(authored + 22f).Within(0.001f),
                    "Reversing the recipe's own layer list orphaned an adjustment.");
            }
            finally
            {
                recipe.Author(recipe.Style, original);
            }
        }

        [Test]
        public void A_duplicate_layer_id_is_reported()
        {
            CardVisualRecipeAsset recipe = Recipe();
            CardVisualLayerDefinition title = TitleLayer();
            CardVisualLayerDefinition other = null;

            foreach (CardVisualLayerDefinition layer in recipe.Layers)
            {
                if (layer != null && !ReferenceEquals(layer, title))
                {
                    other = layer;
                    break;
                }
            }

            string was = other.id;
            List<string> problems = new List<string>();

            try
            {
                other.id = title.id;
                CardVisualDataValidator.ValidateRecipe(recipe, null, problems);
            }
            finally
            {
                other.id = was;
            }

            Assert.That(problems, Has.Some.Contains("two layers answer to the id"),
                "Two layers sharing an id was not reported.\n" + string.Join("\n", problems));
        }

        [Test]
        public void A_layer_with_no_id_is_reported()
        {
            CardVisualRecipeAsset recipe = Recipe();
            CardVisualLayerDefinition title = TitleLayer();

            string was = title.id;
            List<string> problems = new List<string>();

            try
            {
                title.id = string.Empty;

                Assert.That(title.LayerId, Is.EqualTo(title.name),
                    "A layer with no id must still resolve, by falling back to its label.");

                CardVisualDataValidator.ValidateRecipe(recipe, null, problems);
            }
            finally
            {
                title.id = was;
            }

            Assert.That(problems, Has.Some.Contains("has no id"),
                "A layer with no stable id was not reported.\n" + string.Join("\n", problems));
        }

        // ==================================================================
        //  3. Property identity
        // ==================================================================

        /// <summary>
        /// What a saved override names is stated on the field, and survives the
        /// C# field being renamed.
        ///
        /// Built from a type declared here rather than from the real ones,
        /// because the mechanism is what is under test and the real types have
        /// no aliases to exercise - the point of the mechanism is that they do
        /// not need any until somebody renames something.
        /// </summary>
        private sealed class Renamed
        {
            [CardVisualProperty(CardVisualAuthorability.PerCard,
                Id = "width", FormerIds = new[] { "boxWidth", "w" })]
            public float rectangleWidth;
        }

        [Test]
        public void A_saved_override_names_a_stated_id_rather_than_a_field_name()
        {
            FieldInfo field = typeof(Renamed).GetField("rectangleWidth");

            CardVisualProperty property =
                new CardVisualProperty(CardVisualPropertyOwner.Layer, field, string.Empty);

            Assert.That(property.Id, Is.EqualTo("layer.width"),
                "The stated id was ignored in favour of the C# field name, so renaming the " +
                "field would orphan every override that named it.");

            Assert.That(property.FieldName, Is.EqualTo("rectangleWidth"));

            Assert.That(property.FormerIds, Is.EquivalentTo(new[] { "layer.boxWidth", "layer.w" }),
                "Former ids must carry the owner's prefix, or they would never match a saved row.");
        }

        // ------------------------------------------------------------------
        //  3b. Former ids resolve at runtime, not only in CardVisualSchema.Find
        // ------------------------------------------------------------------

        /// <summary>
        /// An override saved under a property's former id - the way a row
        /// written before a rename actually looks on disk - reaches the
        /// composed plan exactly as if it had been saved under the current
        /// one.
        ///
        /// This is the behaviour <see cref="CardVisualSchema.Find"/> and the
        /// validator both already claimed before it was true:
        /// <c>Find("layer.boxWidth")</c> resolved to the property, and the
        /// validator reported "it still resolves through the alias" - but
        /// <see cref="CardVisualInheritance.Resolve"/> and
        /// <see cref="CardVisualInheritance.WithOverrides{T}"/> looked a row up
        /// by <c>property.Id</c> alone, so an aliased row was accepted by the
        /// metadata and ignored by composition. Checking <c>Find()</c> would
        /// not have caught that - <c>Find()</c> was already right. Only
        /// composing a real card proves it.
        ///
        /// Anchored on <c>layer.width</c>'s own <c>FormerIds</c>, which exists
        /// for exactly this test - see the comment beside the field.
        /// </summary>
        [Test]
        public void An_override_saved_under_a_former_property_id_reaches_the_composed_plan()
        {
            CardVisualLayerDefinition title = TitleLayer();
            CardVisualProperty width = CardVisualSchema.Find("layer.width");

            Assert.That(width, Is.Not.Null);
            Assert.That(width.FormerIds, Has.Some.EqualTo("layer.boxWidth"),
                "This test anchors on layer.width declaring 'boxWidth' as a former id.");

            float authored = title.width;

            CardVisualOverrides own = new CardVisualOverrides();

            // The raw string overload, not Set(layer, property, value) - a row
            // saved before the rename never goes through the normalising
            // overload, and the point is to resolve it as it actually is.
            own.Set(title.LayerId, "layer.boxWidth", CardVisualValue.Of(authored + 40f));

            CardVisualPlan plan = new CardVisualPlan();
            Factory().Compose(Minion("Alias probe", own), plan);

            Assert.That(Find(plan, title.name).Rect.width, Is.EqualTo(authored + 40f).Within(0.001f),
                "An override saved under a former property id did not reach the composed plan.");

            CardVisualResolved resolved = CardVisualInheritance.Resolve(
                width, title, title.LayerId, "Standard", own);

            Assert.That(resolved.Source, Is.EqualTo(CardVisualSource.CardOverride),
                "Resolve reported the aliased value as though nobody had asked for it.");
        }

        /// <summary>
        /// If a row exists under both the current id and a former one for the
        /// same layer at once - authored data from mid-migration, or a bad
        /// merge - the current id wins, predictably, rather than whichever
        /// happened to be stored first or iterated last.
        /// </summary>
        [Test]
        public void A_current_property_id_wins_over_a_former_one_when_both_are_present()
        {
            CardVisualLayerDefinition title = TitleLayer();
            float authored = title.width;

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.boxWidth", CardVisualValue.Of(authored + 10f));
            own.Set(title.LayerId, "layer.width", CardVisualValue.Of(authored + 99f));

            CardVisualPlan plan = new CardVisualPlan();
            Factory().Compose(Minion("Coexist probe", own), plan);

            Assert.That(Find(plan, title.name).Rect.width, Is.EqualTo(authored + 99f).Within(0.001f),
                "The former id's value was applied even though the current id also had a row.");
        }

        /// <summary>
        /// Editing a value found through a former id, the way the editor does,
        /// leaves the card with one row under the current id - not two. This
        /// is what keeps "the current id is the only persisted target" true
        /// once somebody actually touches an aliased row, rather than merely
        /// inheriting through it.
        /// </summary>
        [Test]
        public void Writing_through_a_former_id_normalises_it_to_the_current_one()
        {
            CardVisualProperty width = CardVisualSchema.Find("layer.width");
            CardVisualLayerDefinition title = TitleLayer();
            float authored = title.width;

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.boxWidth", CardVisualValue.Of(authored + 10f));

            own.Set(title.LayerId, width, CardVisualValue.Of(authored + 55f));

            Assert.That(own.Overrides(title.LayerId, "layer.boxWidth"), Is.False,
                "The former id's row survived a write made through the current property.");

            Assert.That(own.TryGet(title.LayerId, "layer.width", out CardVisualValue stored), Is.True);
            Assert.That(stored.number, Is.EqualTo(authored + 55f));
            Assert.That(own.Count, Is.EqualTo(1),
                "Writing through a former id left two rows instead of normalising to one.");
        }

        /// <summary>Clearing an adjustment removes it whichever id it happens to be stored under.</summary>
        [Test]
        public void Clearing_a_property_removes_it_under_a_former_id_too()
        {
            CardVisualProperty width = CardVisualSchema.Find("layer.width");
            CardVisualLayerDefinition title = TitleLayer();

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.boxWidth", CardVisualValue.Of(title.width + 10f));

            own.Clear(title.LayerId, width);

            Assert.That(own.IsEmpty, Is.True,
                "Clearing through the current property left a row still sitting under a former id.");
        }

        // ------------------------------------------------------------------
        //  Runtime safety: a malformed override never applies
        // ------------------------------------------------------------------

        /// <summary>A. The wrong kind of value for a float property is refused, not read as zero.</summary>
        [Test]
        public void A_wrong_kind_override_leaves_the_inherited_value_unchanged()
        {
            CardVisualLayerDefinition title = TitleLayer();
            float authored = title.width;

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.width", CardVisualValue.Of(Color.red));

            CardVisualPlan plan = new CardVisualPlan();
            Factory().Compose(Minion("Wrong kind probe", own), plan);

            Assert.That(Find(plan, title.name).Rect.width, Is.EqualTo(authored).Within(0.001f),
                "A value stored as the wrong kind was applied instead of falling back to the profile.");
        }

        /// <summary>B. An enumeration value nothing defines is refused, not applied as a garbage member.</summary>
        [Test]
        public void An_invalid_enum_override_leaves_the_inherited_value_unchanged()
        {
            CardVisualLayerDefinition title = TitleLayer();
            CardVisualAlignment authored = title.alignment;

            CardVisualValue nonsense = new CardVisualValue
            {
                kind = CardVisualValueKind.Number,
                number = 4242f
            };

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.alignment", nonsense);

            CardVisualLayerDefinition placed = CardVisualInheritance.WithOverrides(title, title.LayerId, own);

            Assert.That(placed.alignment, Is.EqualTo(authored),
                "An enum value nothing defines was applied instead of falling back to the authored one.");
        }

        /// <summary>C. A genuinely valid value still applies normally.</summary>
        [Test]
        public void A_valid_override_still_applies_normally()
        {
            CardVisualLayerDefinition title = TitleLayer();
            float authored = title.width;

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.width", CardVisualValue.Of(authored + 17f));

            CardVisualPlan plan = new CardVisualPlan();
            Factory().Compose(Minion("Valid probe", own), plan);

            Assert.That(Find(plan, title.name).Rect.width, Is.EqualTo(authored + 17f).Within(0.001f));
        }

        /// <summary>
        /// D. The same malformed row composes safely and is reported by the
        /// validator, in one place - so a future change cannot fix the runtime
        /// side without this test noticing the editor side went silent, or the
        /// other way round.
        /// </summary>
        [Test]
        public void A_malformed_override_is_refused_at_runtime_and_reported_by_the_validator()
        {
            CardVisualLayerDefinition title = TitleLayer();
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("malformed_probe")
                    .Set(title.LayerId, "layer.width", CardVisualValue.Of(Color.red));

                CardVisualOverrides own =
                    library.OverridesFor(new CoH.Core.Identifiers.CardId("malformed_probe"));

                CardVisualLayerDefinition placed = CardVisualInheritance.WithOverrides(title, title.LayerId, own);

                Assert.That(placed.width, Is.EqualTo(title.width).Within(0.001f),
                    "The malformed row was applied at runtime.");

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string> { title.LayerId }, problems);

                Assert.That(problems, Has.Some.Contains("stored as"),
                    "The same malformed row was not reported by the validator.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        /// <summary>
        /// E. Composing a card whose adjustments are thoroughly malformed - a
        /// wrong kind, an undefined enum, an unknown property and an unknown
        /// layer, all at once - does not throw. A build where nobody ever
        /// opened the validator must still be safe against hand-edited or
        /// corrupted data.
        /// </summary>
        [Test]
        public void Composing_a_card_with_thoroughly_malformed_adjustments_does_not_throw()
        {
            CardVisualLayerDefinition title = TitleLayer();

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(title.LayerId, "layer.width", CardVisualValue.Of(Color.red));
            own.Set(title.LayerId, "layer.alignment",
                new CardVisualValue { kind = CardVisualValueKind.Number, number = 99999f });
            own.Set(title.LayerId, "layer.thisPropertyHasNeverExisted", CardVisualValue.Of(1f));
            own.Set("a-layer-that-does-not-exist", "layer.width", CardVisualValue.Of(1f));

            CardVisualPlan plan = new CardVisualPlan();

            Assert.DoesNotThrow(() => Factory().Compose(Minion("Chaos probe", own), plan));

            Assert.That(Find(plan, title.name).Rect.width, Is.EqualTo(title.width).Within(0.001f),
                "Malformed adjustments changed the composed width instead of being refused.");
        }

        [Test]
        public void An_override_naming_no_known_property_is_reported()
        {
            CardVisualLayerDefinition title = TitleLayer();

            Assert.That(CardVisualSchema.Find("layer.thisWasRenamedAwayYearsAgo"), Is.Null);

            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("orphan_probe")
                    .Set(title.LayerId, "layer.thisWasRenamedAwayYearsAgo", CardVisualValue.Of(1f));

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string>(new[] { title.LayerId }), problems);

                Assert.That(problems, Has.Some.Contains("not a property anything"),
                    "An override naming a property nothing answers to was not reported.\n" +
                    string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void An_override_naming_a_layer_no_recipe_defines_is_reported()
        {
            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("orphan_layer_probe")
                    .Set("a-layer-that-was-deleted", "layer.y", CardVisualValue.Of(1f));

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string>(new[] { "nametext-other" }), problems);

                Assert.That(problems, Has.Some.Contains("which no recipe defines"),
                    "An override naming a layer nobody has was not reported.\n" +
                    string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void An_override_stored_as_the_wrong_kind_of_value_is_reported_rather_than_read_as_zero()
        {
            CardVisualLayerDefinition title = TitleLayer();

            // A colour saved where a number belongs. CardVisualValue.As reads
            // whichever field the wanted type lives in without checking that
            // anything was written there, so this reads back as 0 and is
            // indistinguishable from an authored zero.
            CardVisualValue wrong = CardVisualValue.Of(Color.red);

            Assert.That(wrong.Fits(typeof(float)), Is.False);
            Assert.That(wrong.As(typeof(float)), Is.EqualTo(0f),
                "This is the silent failure being guarded: it reads as a plausible zero.");

            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("kind_probe").Set(title.LayerId, "layer.y", wrong);

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string>(new[] { title.LayerId }), problems);

                Assert.That(problems, Has.Some.Contains("stored as"),
                    "A value stored as the wrong kind was absorbed silently.\n" +
                    string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void An_override_holding_an_undefined_enum_value_is_reported()
        {
            CardVisualLayerDefinition title = TitleLayer();

            CardVisualValue nonsense = new CardVisualValue
            {
                kind = CardVisualValueKind.Number,
                number = 4242f
            };

            Assert.That(nonsense.IsDefinedFor(typeof(CardVisualAlignment)), Is.False);

            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("enum_probe")
                    .Set(title.LayerId, "layer.alignment", nonsense);

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string>(new[] { title.LayerId }), problems);

                Assert.That(problems, Has.Some.Contains("does not define"),
                    "An enum value nothing defines was accepted.\n" + string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void An_override_of_a_property_no_card_may_differ_on_is_reported()
        {
            CardVisualLayerDefinition title = TitleLayer();

            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                library.EstablishOverrides("structural_probe")
                    .Set(title.LayerId, "layer.slot", CardVisualValue.Of(3));

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string>(new[] { title.LayerId }), problems);

                Assert.That(problems, Has.Some.Contains("may not differ on"),
                    "An override of a structural property was stored without complaint.\n" +
                    string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void The_same_property_adjusted_twice_is_reported()
        {
            CardVisualLayerDefinition title = TitleLayer();

            CardVisualLibraryAsset library = ScriptableObject.CreateInstance<CardVisualLibraryAsset>();

            try
            {
                CardVisualOverrides own = library.EstablishOverrides("duplicate_probe");
                own.Set(title.LayerId, "layer.y", CardVisualValue.Of(1f));

                // Set() replaces, so a duplicate has to be built the way a bad
                // merge or a hand-edited asset would produce one.
                List<CardVisualPropertyOverride> rows =
                    (List<CardVisualPropertyOverride>)own.Properties;

                rows.Add(new CardVisualPropertyOverride
                {
                    layer = title.LayerId,
                    property = "layer.y",
                    value = CardVisualValue.Of(2f)
                });

                List<string> problems = new List<string>();

                CardVisualDataValidator.ValidateLibrary(
                    library, new HashSet<string>(new[] { title.LayerId }), problems);

                Assert.That(problems, Has.Some.Contains("adjusted twice"),
                    "A property adjusted twice was not reported.\n" + string.Join("\n", problems));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        // ==================================================================
        //  4. Only expose what works
        // ==================================================================

        /// <summary>
        /// Everything the schema offers as a per-card adjustment actually
        /// changes the composed card.
        ///
        /// The contract test the whole authorability model exists for. It
        /// overrides each per-card property in turn, on a picture layer and on
        /// a text layer, recomposes, and requires the result to differ. A
        /// property that fails this is one the editor would accept a change to
        /// and quietly discard, which is the failure mode that makes an
        /// authoring tool untrustworthy.
        /// </summary>
        [Test]
        public void Every_property_offered_per_card_actually_changes_the_composed_card()
        {
            CardVisualLayerDefinition text = TitleLayer();
            CardVisualLayerDefinition picture = SpriteLayer();

            List<string> inert = new List<string>();

            foreach (CardVisualProperty property in CardVisualSchema.LayerProperties)
            {
                if (property.SupportsCardOverride && !ChangesSomething(property, text, picture))
                {
                    inert.Add(property.Id + " (" + property.Type.Name + ")");
                }
            }

            foreach (CardVisualProperty property in CardVisualSchema.StyleProperties)
            {
                if (property.SupportsCardOverride && !ChangesSomething(property, text, picture))
                {
                    inert.Add(property.Id + " (" + property.Type.Name + ")");
                }
            }

            Assert.That(inert, Is.Empty,
                "These are offered as per-card adjustments and change nothing about the " +
                "composed card. Either wire them through, or mark them Structural or " +
                "Unsupported so the editor stops accepting edits it discards:\n - " +
                string.Join("\n - ", inert));
        }

        /// <summary>
        /// Whether overriding this property changes what either kind of layer
        /// composes to. A layer property may legitimately only bite on one of
        /// the two - a picture's fill means nothing on a label.
        /// </summary>
        private static bool ChangesSomething(
            CardVisualProperty property,
            CardVisualLayerDefinition text,
            CardVisualLayerDefinition picture) =>
            ChangesLayer(property, text) || ChangesLayer(property, picture);

        private static bool ChangesLayer(CardVisualProperty property, CardVisualLayerDefinition layer)
        {
            object authored = property.Owner == CardVisualPropertyOwner.Layer
                ? property.Read(layer)
                : property.Read(Recipe().TextStyleFor(layer));

            if (!TryDifferent(property, authored, out object wanted))
            {
                return false;
            }

            CardVisualOverrides own = new CardVisualOverrides();
            own.Set(layer.LayerId, property.Id, CardVisualValue.Of(wanted));

            CardVisualPlan plain = new CardVisualPlan();
            CardVisualPlan adjusted = new CardVisualPlan();

            Factory().Compose(Minion("Contract"), plain);
            Factory().Compose(Minion("Contract", own), adjusted);

            return Describe(plain, layer.name) != Describe(adjusted, layer.name);
        }

        /// <summary>
        /// What a composed layer would actually draw.
        ///
        /// Outcomes, not metadata. The plan records the slot an override asked
        /// for, but the picture was resolved from the *authored* slot before
        /// adjustments were read - so comparing the recorded slot would let a
        /// misclassified <c>layer.slot</c> pass this test while changing no
        /// pixel. The resolved sprite is compared instead, which is the thing a
        /// person would see.
        /// </summary>
        private static string Describe(CardVisualPlan plan, string layerName)
        {
            for (int index = 0; index < plan.Layers.Count; index++)
            {
                CardVisualPlannedLayer layer = plan.Layers[index];

                if (layer.LayerName != layerName)
                {
                    continue;
                }

                CardTextStyle style = layer.TextStyle;

                StringBuilder text = new StringBuilder();

                text.Append(layer.Sprite == null ? "-" : layer.Sprite.name).Append('|')
                    .Append(layer.Mask == null ? "-" : layer.Mask.name).Append('|')
                    .Append(layer.Text).Append('|').Append(layer.SortingOrder).Append('|')
                    .Append(layer.Rect).Append('|').Append(layer.Rotation).Append('|')
                    .Append(layer.Fill).Append('|').Append(layer.FontSize).Append('|')
                    .Append(layer.FontSizeMin).Append('|').Append(layer.Bold).Append('|')
                    .Append(layer.Wrap).Append('|').Append(layer.Alignment).Append('|')
                    .Append(layer.Tint).Append('|')
                    .Append(style.Role).Append('|').Append(style.RenderMode).Append('|')
                    .Append(style.OutlineColor).Append('|').Append(style.OutlineWidth).Append('|')
                    .Append(style.Tracking).Append('|').Append(style.LineSpacing).Append('|')
                    .Append(style.MinCondense).Append('|').Append(style.Stretch).Append('|')
                    .Append(style.Taper).Append('|').Append(style.CurveControlA).Append('|')
                    .Append(style.CurveControlB).Append('|').Append(style.CurveEnd);

                return text.ToString();
            }

            return "(absent)";
        }

        /// <summary>A value of the property's own type that is not the one it has.</summary>
        private static bool TryDifferent(CardVisualProperty property, object authored, out object wanted)
        {
            Type type = property.Type;

            if (type == typeof(float))
            {
                wanted = (float)authored + 7.25f;
                return true;
            }

            if (type == typeof(int))
            {
                wanted = (int)authored + 5;
                return true;
            }

            if (type == typeof(bool))
            {
                wanted = !(bool)authored;
                return true;
            }

            if (type == typeof(Color))
            {
                Color was = (Color)authored;
                wanted = new Color(1f - was.r, was.g, 1f - was.b, was.a);
                return true;
            }

            if (type == typeof(Vector2))
            {
                wanted = (Vector2)authored + new Vector2(0.13f, 0.29f);
                return true;
            }

            if (type == typeof(Vector3))
            {
                wanted = (Vector3)authored + new Vector3(0.13f, 0.29f, 0.11f);
                return true;
            }

            if (type == typeof(Rect))
            {
                Rect was = (Rect)authored;
                wanted = new Rect(was.x + 3f, was.y + 3f, was.width + 3f, was.height + 3f);
                return true;
            }

            if (type.IsEnum)
            {
                foreach (object candidate in Enum.GetValues(type))
                {
                    if (!Equals(candidate, authored))
                    {
                        wanted = candidate;
                        return true;
                    }
                }
            }

            wanted = null;
            return false;
        }

        /// <summary>
        /// Nothing is left classified by accident.
        ///
        /// Every property either propagates and is offered, or is marked with a
        /// reason. The reason is what the editor shows beside a control it has
        /// greyed out, and a greyed control with no reason is indistinguishable
        /// from a broken one.
        /// </summary>
        [Test]
        public void Every_property_that_is_not_freely_editable_says_why()
        {
            List<string> silent = new List<string>();

            foreach (CardVisualPropertyOwner owner in
                new[] { CardVisualPropertyOwner.Layer, CardVisualPropertyOwner.Style })
            {
                foreach (CardVisualProperty property in CardVisualSchema.For(owner))
                {
                    if (property.Authorability != CardVisualAuthorability.PerCard &&
                        string.IsNullOrEmpty(property.Note))
                    {
                        silent.Add(property.Id + " is " + property.Authorability + " with no note.");
                    }
                }
            }

            Assert.That(silent, Is.Empty, string.Join("\n", silent));
        }

        [Test]
        public void The_schema_has_no_colliding_ids()
        {
            Assert.That(CardVisualSchema.Problems, Is.Empty,
                string.Join("\n", CardVisualSchema.Problems));
        }

        /// <summary>
        /// The colour a style declares is not one of the things a card can be
        /// adjusted on, because nothing reads it.
        ///
        /// Recorded as a test rather than only in a comment: this is a real
        /// duplicate source of truth in the authored data - the rules layer
        /// carries a tint of 0.12 and the rules style a fillColor of 0.1176,
        /// two nearly-equal values for one thing, of which only the tint has
        /// ever reached a renderer. Marking it is the smallest honest fix;
        /// wiring it would change the calibrated appearance of every label.
        /// </summary>
        [Test]
        public void The_colour_a_label_is_drawn_in_has_exactly_one_source()
        {
            CardVisualProperty fill = CardVisualSchema.Find("style.fillColor");
            CardVisualProperty tint = CardVisualSchema.Find("layer.tint");

            Assert.That(fill, Is.Not.Null);
            Assert.That(tint, Is.Not.Null);

            Assert.That(fill.Authorability, Is.EqualTo(CardVisualAuthorability.Unsupported),
                "style.fillColor is offered as editable but reaches no renderer.");

            Assert.That(tint.SupportsCardOverride, Is.True,
                "layer.tint is the one thing that does colour a label, so it must stay editable.");

            // And the struct the painter actually reads carries no fill colour
            // at all, which is why the field could never have worked.
            Assert.That(typeof(CardTextStyle).GetField("FillColor"), Is.Null,
                "CardTextStyle gained a FillColor. If it is now wired through, style.fillColor " +
                "should stop being marked Unsupported.");
        }

        // ==================================================================
        //  5. The authored recipe is the source of truth
        // ==================================================================

        /// <summary>
        /// The maintenance command cannot replace an authored recipe.
        ///
        /// Rebuild used to reconstruct the layer list from the scaffolding in
        /// the setup script every time it ran, which was right while the recipe
        /// *was* scaffolding and became a way to lose an evening's work the
        /// moment the editor made it authored data. Checked by running it.
        /// </summary>
        [Test]
        public void Creating_missing_assets_leaves_an_authored_recipe_alone()
        {
            CardVisualRecipeAsset recipe = Recipe();

            int layers = recipe.Layers.Count;
            CardVisualLayerDefinition title = TitleLayer();

            string id = title.id;
            float y = title.y;
            float fontSize = title.fontSize;

            CoH.Editor.CardVisualSetup.Rebuild();

            Assert.That(recipe.Layers.Count, Is.EqualTo(layers),
                "The maintenance command rewrote the authored layer list.");

            CardVisualLayerDefinition after = TitleLayer();

            Assert.That(after.id, Is.EqualTo(id), "A layer's permanent id was rewritten.");
            Assert.That(after.y, Is.EqualTo(y).Within(0.0001f), "An authored position was lost.");
            Assert.That(after.fontSize, Is.EqualTo(fontSize).Within(0.0001f),
                "An authored font size was lost.");
        }

        // ==================================================================
        //  6. The data as it actually stands
        // ==================================================================

        [Test]
        public void The_projects_own_visual_data_has_nothing_wrong_with_it()
        {
            List<string> problems = new List<string>();
            CardVisualDataValidator.Validate(Factory(), problems);

            Assert.That(problems, Is.Empty,
                "The authored card visual data has problems:\n - " + string.Join("\n - ", problems));
        }
    }
}
