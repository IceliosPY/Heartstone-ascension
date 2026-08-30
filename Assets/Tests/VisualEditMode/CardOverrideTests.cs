using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// One card's own adjustments, and the properties that keep a roster of a
    /// thousand cards maintainable.
    ///
    /// The whole design turns on adjustments being *sparse*. A card that wants a
    /// wider title stores one row saying so, and keeps inheriting everything
    /// else — so retuning the kind of card still moves it. The alternative, a
    /// copy of the profile on every card, works beautifully for ten cards and
    /// becomes unmaintainable at two hundred, because a change to the type then
    /// has to be applied by hand a hundred times.
    ///
    /// The second property is that the adjustments reach the composer as data.
    /// A card's identity is used once, to look a set of values up; nothing
    /// downstream is told which card it is drawing.
    /// </summary>
    public sealed class CardOverrideTests
    {
        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private static CardVisualFactory Factory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null, "No card visual factory.");
            return factory;
        }

        private static CardVisualRecipeAsset Recipe()
        {
            CardVisualRecipeAsset recipe = AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(
                "Assets/_Project/Data/CardVisuals/CardVisualRecipe_Standard.asset");

            Assert.That(recipe, Is.Not.Null, "No standard recipe.");
            return recipe;
        }

        private CardVisualPlannedLayer Compose(
            CardVisualOverrides overrides,
            CardType type = CardType.Minion,
            string name = "Test Soldier")
        {
            Factory().Compose(
                new CardVisualDescriptor(
                    type, CardClass.Neutral, Rarity.Common, Tribe.None,
                    artwork: null, name: name, rulesText: "Taunt.",
                    manaCost: 2, attack: 2, health: 3,
                    showsCost: true,
                    showsStatistics: type == CardType.Minion || type == CardType.Weapon,
                    style: default, secondaryClass: CardClass.Neutral, expansion: "",
                    faceDown: false, overrides: overrides),
                _plan);

            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (_plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                {
                    return _plan.Layers[index];
                }
            }

            Assert.Fail("The composed card has no title.");
            return default;
        }

        /// <summary>The layer a minion's title is drawn by, whatever it is called.</summary>
        private static CardVisualLayerDefinition TitleLayer()
        {
            CardVisualDescriptor minion = new CardVisualDescriptor(
                CardType.Minion, CardClass.Neutral, Rarity.Common, Tribe.None,
                artwork: null, name: "x", rulesText: "x",
                manaCost: 1, attack: 1, health: 1,
                showsCost: true, showsStatistics: true);

            CardVisualRecipeAsset recipe = Recipe();

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer != null &&
                    layer.text == CardVisualTextSlot.Name &&
                    layer.AppliesTo(minion))
                {
                    return layer;
                }
            }

            Assert.Fail("The recipe has no title layer for a minion.");
            return null;
        }

        /// <summary>A set of adjustments asking for one property of one layer.</summary>
        private static CardVisualOverrides Asking(string layer, string property, object value)
        {
            CardVisualOverrides overrides = new CardVisualOverrides();
            overrides.Set(layer, property, CardVisualValue.Of(value));
            return overrides;
        }

        // ------------------------------------------------------------------
        //  Asking for nothing
        // ------------------------------------------------------------------

        [Test]
        public void A_card_with_no_adjustments_composes_exactly_as_its_profile_says()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualPlannedLayer empty = Compose(new CardVisualOverrides());

            Assert.That(empty.Rect, Is.EqualTo(plain.Rect));
            Assert.That(empty.FontSize, Is.EqualTo(plain.FontSize).Within(0.0001f));
            Assert.That(empty.TextStyle.MinCondense,
                Is.EqualTo(plain.TextStyle.MinCondense).Within(0.0001f));
            Assert.That(empty.TextStyle.CurveControlB, Is.EqualTo(plain.TextStyle.CurveControlB));
        }

        // ------------------------------------------------------------------
        //  Sparseness
        // ------------------------------------------------------------------

        /// <summary>
        /// One adjustment is one row, and everything else keeps coming from the
        /// profile. This is the property the whole design exists for.
        /// </summary>
        [Test]
        public void An_adjustment_changes_one_property_and_inherits_every_other()
        {
            CardVisualPlannedLayer plain = Compose(null);
            CardVisualLayerDefinition layer = TitleLayer();

            CardVisualOverrides overrides = Asking(layer.LayerId, "layer.width", plain.Rect.width + 25f);

            Assert.That(overrides.Count, Is.EqualTo(1),
                "One adjustment stored more than one row.");

            CardVisualPlannedLayer adjusted = Compose(overrides);

            Assert.That(adjusted.Rect.width, Is.EqualTo(plain.Rect.width + 25f).Within(0.0001f));

            // Everything else, untouched.
            Assert.That(adjusted.Rect.x, Is.EqualTo(plain.Rect.x).Within(0.0001f));
            Assert.That(adjusted.Rect.y, Is.EqualTo(plain.Rect.y).Within(0.0001f));
            Assert.That(adjusted.Rect.height, Is.EqualTo(plain.Rect.height).Within(0.0001f));
            Assert.That(adjusted.FontSize, Is.EqualTo(plain.FontSize).Within(0.0001f));
            Assert.That(adjusted.TextStyle.Stretch,
                Is.EqualTo(plain.TextStyle.Stretch).Within(0.0001f));
            Assert.That(adjusted.TextStyle.CurveControlB, Is.EqualTo(plain.TextStyle.CurveControlB));
        }

        /// <summary>
        /// And when the profile changes, a card that said nothing about that
        /// property follows — while the one thing it did say stays said.
        ///
        /// Restored afterwards, because this edits the project's own recipe.
        /// </summary>
        [Test]
        public void Retuning_the_profile_moves_the_cards_that_did_not_object()
        {
            CardVisualLayerDefinition layer = TitleLayer();

            float wasY = layer.y;
            float wasWidth = layer.width;

            try
            {
                CardVisualOverrides overrides =
                    Asking(layer.LayerId, "layer.width", wasWidth + 40f);

                CardVisualPlannedLayer before = Compose(overrides);

                Assert.That(before.Rect.y, Is.EqualTo(wasY).Within(0.0001f));
                Assert.That(before.Rect.width, Is.EqualTo(wasWidth + 40f).Within(0.0001f));

                // The profile moves.
                layer.y = wasY + 17f;

                CardVisualPlannedLayer after = Compose(overrides);

                Assert.That(after.Rect.y, Is.EqualTo(wasY + 17f).Within(0.0001f),
                    "A card that never mentioned its title's height did not follow its profile.");

                Assert.That(after.Rect.width, Is.EqualTo(wasWidth + 40f).Within(0.0001f),
                    "Retuning the profile overwrote something the card had asked for.");
            }
            finally
            {
                layer.y = wasY;
                layer.width = wasWidth;
            }
        }

        [Test]
        public void Forgetting_an_adjustment_restores_what_was_inherited()
        {
            CardVisualPlannedLayer plain = Compose(null);
            CardVisualLayerDefinition layer = TitleLayer();

            CardVisualOverrides overrides = Asking(layer.LayerId, "layer.width", plain.Rect.width + 30f);

            Assert.That(Compose(overrides).Rect.width, Is.Not.EqualTo(plain.Rect.width));

            overrides.Clear(layer.LayerId, "layer.width");

            Assert.That(Compose(overrides).Rect, Is.EqualTo(plain.Rect));
        }

        /// <summary>An adjustment reaches the layer it names and no other.</summary>
        [Test]
        public void An_adjustment_reaches_only_the_layer_it_names()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualOverrides overrides =
                Asking("a layer by no such name", "layer.width", 999f);

            Assert.That(Compose(overrides).Rect, Is.EqualTo(plain.Rect),
                "An adjustment aimed at a layer that does not exist changed one that does.");
        }

        // ------------------------------------------------------------------
        //  Anything the schema knows about
        // ------------------------------------------------------------------

        /// <summary>
        /// A card can adjust a style property as readily as a layer one, and
        /// without the style itself changing for anybody else.
        /// </summary>
        [Test]
        public void A_card_can_adjust_its_style_without_moving_the_style()
        {
            CardVisualPlannedLayer plain = Compose(null);
            CardVisualLayerDefinition layer = TitleLayer();

            CardTextStyleDefinition style = Recipe().TextStyleFor(layer);
            Assert.That(style, Is.Not.Null, "The title layer names no style.");

            float wasOutline = style.outlineWidth;

            CardVisualPlannedLayer adjusted =
                Compose(Asking(layer.LayerId, "style.outlineWidth", wasOutline * 0.5f));

            Assert.That(adjusted.TextStyle.OutlineWidth,
                Is.EqualTo(wasOutline * 0.5f).Within(0.0001f));

            Assert.That(style.outlineWidth, Is.EqualTo(wasOutline).Within(0.0001f),
                "Adjusting one card edited the style every card of its kind shares.");

            Assert.That(Compose(null).TextStyle.OutlineWidth,
                Is.EqualTo(wasOutline).Within(0.0001f),
                "A card with no adjustments picked up another card's.");
        }

        /// <summary>
        /// And a property nobody wrote a control for works too, because the
        /// schema is read off the data rather than listed anywhere.
        /// </summary>
        [Test]
        public void Every_property_the_schema_admits_can_actually_be_adjusted()
        {
            CardVisualLayerDefinition layer = TitleLayer();

            int reachable = 0;

            foreach (CardVisualProperty property in CardVisualSchema.LayerProperties)
            {
                if (!property.SupportsCardOverride || property.Type != typeof(float))
                {
                    continue;
                }

                object plain = property.Read(layer);
                float wanted = (float)plain + 3f;

                CardVisualLayerDefinition adjusted = CardVisualInheritance.WithOverrides(
                    layer, layer.LayerId, Asking(layer.LayerId, property.Id, wanted));

                Assert.That(property.Read(adjusted), Is.EqualTo(wanted).Within(0.0001f),
                    property.Id + " could not be adjusted.");

                Assert.That(property.Read(layer), Is.EqualTo(plain),
                    property.Id + " was written back onto the profile.");

                reachable++;
            }

            Assert.That(reachable, Is.GreaterThan(4),
                "The schema found almost nothing adjustable, which cannot be right.");
        }

        // ------------------------------------------------------------------
        //  Provenance
        // ------------------------------------------------------------------

        [Test]
        public void Every_value_can_say_where_it_came_from()
        {
            CardVisualLayerDefinition layer = TitleLayer();
            CardVisualProperty width = CardVisualSchema.Find("layer.width");

            Assert.That(width, Is.Not.Null);

            CardVisualResolved fromProfile = CardVisualInheritance.Resolve(
                width, layer, layer.LayerId, "Standard", null);

            Assert.That(fromProfile.Source, Is.EqualTo(CardVisualSource.TypeProfile),
                "A width the recipe set was not reported as coming from it.");

            CardVisualResolved fromCard = CardVisualInheritance.Resolve(
                width, layer, layer.LayerId, "Standard",
                Asking(layer.LayerId, "layer.width", 515f));

            Assert.That(fromCard.Source, Is.EqualTo(CardVisualSource.CardOverride));
            Assert.That(fromCard.Value, Is.EqualTo(515f));
            Assert.That(fromCard.Describe(), Is.EqualTo("This card"));

            // A property whose authored value happens to equal the C# field's
            // initialiser still came from the recipe, and says so.
            //
            // This used to assert the opposite - that a rotation of zero was
            // reported as a global default because zero is what the field is
            // written with. That answered "is this value different from the
            // initialiser", which is not the question provenance is asked. A
            // recipe that sets a rotation to zero and a recipe that says
            // nothing at all are different facts, and the one time anybody
            // reads provenance is when they need to tell them apart.
            CardVisualProperty rotation = CardVisualSchema.Find("layer.rotation");

            Assert.That(rotation.Read(layer), Is.EqualTo(CardVisualInheritance.Default(rotation)),
                "This case is only interesting while the authored value equals the default.");

            Assert.That(
                CardVisualInheritance.Resolve(rotation, layer, layer.LayerId, "Standard", null).Source,
                Is.EqualTo(CardVisualSource.TypeProfile),
                "An authored layer's value was reported as nobody's decision because it " +
                "happened to equal the field's initialiser.");

            // Nothing authored at all is the only global default there is.
            Assert.That(
                CardVisualInheritance.Resolve(rotation, null, "anything", "Standard", null).Source,
                Is.EqualTo(CardVisualSource.GlobalDefault));
        }

        // ------------------------------------------------------------------
        //  The schema itself
        // ------------------------------------------------------------------

        /// <summary>
        /// The schema finds the fields, with the tooltips and ranges the data
        /// already carries. This is what lets a property added to the data show
        /// up in the editor without the editor being touched.
        /// </summary>
        [Test]
        public void The_schema_reads_what_the_data_already_says()
        {
            Assert.That(CardVisualSchema.LayerProperties.Count, Is.GreaterThan(10));
            Assert.That(CardVisualSchema.StyleProperties.Count, Is.GreaterThan(8));

            CardVisualProperty stretch = CardVisualSchema.Find("style.stretch");

            Assert.That(stretch, Is.Not.Null);
            Assert.That(stretch.DisplayName, Is.EqualTo("Stretch"));
            Assert.That(stretch.HasRange, Is.True, "The range on the field was not picked up.");
            Assert.That(stretch.Tooltip, Is.Not.Empty, "The tooltip on the field was not picked up.");

            // What identifies a layer is not something one card may differ on:
            // a card with its own layer name is a card with its own recipe.
            Assert.That(CardVisualSchema.Find("layer.name").SupportsCardOverride, Is.False);
            Assert.That(CardVisualSchema.Find("layer.textStyle").SupportsCardOverride, Is.False);
            Assert.That(CardVisualSchema.Find("layer.width").SupportsCardOverride, Is.True);
        }

        // ------------------------------------------------------------------
        //  Where the identity is allowed to be
        // ------------------------------------------------------------------

        [Test]
        public void Nothing_that_composes_a_card_can_ask_which_card_it_is()
        {
            string[] deciding =
            {
                "Assets/_Project/Presentation/CardVisuals/CardVisualComposer.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualOverrides.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualProperty.cs",
                "Assets/_Project/Presentation/CardVisuals/CardTextWarp.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualPainter.cs",
                "Assets/_Project/Editor/CardVisualEditorWindow.cs"
            };

            foreach (string path in deciding)
            {
                Assert.That(File.Exists(path), Is.True, path + " is missing.");

                string source = File.ReadAllText(path);

                Assert.That(source.Contains("test_soldier"), Is.False,
                    Path.GetFileName(path) + " names a card.");

                Assert.That(source.Contains("the_coin"), Is.False,
                    Path.GetFileName(path) + " names a card.");
            }
        }
    }
}
