using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// One card's own adjustments, and the two properties that keep them from
    /// becoming a pile of special cases.
    ///
    /// The first is that they are optional all the way down: a card that asks
    /// for nothing must compose byte for byte as though the whole mechanism did
    /// not exist, because the moment a card without overrides draws differently
    /// from a card without overrides *before* this was added, the recipe has
    /// stopped being the source of the style.
    ///
    /// The second is that they reach the composer as data. A card's identity is
    /// used once, to fetch a set of numbers; nothing downstream is told which
    /// card it is drawing, and there is therefore nowhere to write "if this is
    /// The Coin".
    /// </summary>
    public sealed class CardOverrideTests
    {
        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private static CardVisualFactory Factory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null,
                "No card visual factory. Run Conquest of Hearthstone -> Rebuild Card Visuals.");

            return factory;
        }

        private CardVisualPlannedLayer Compose(
            CardVisualOverrides overrides,
            string name = "Test Soldier",
            CardType type = CardType.Minion)
        {
            CardVisualFactory factory = Factory();

            factory.Compose(
                new CardVisualDescriptor(
                    type,
                    CardClass.Neutral,
                    Rarity.Common,
                    Tribe.None,
                    artwork: null,
                    name: name,
                    rulesText: string.Empty,
                    manaCost: 2,
                    attack: 2,
                    health: 3,
                    showsCost: true,
                    showsStatistics: type == CardType.Minion || type == CardType.Weapon,
                    style: default,
                    secondaryClass: CardClass.Neutral,
                    expansion: "",
                    faceDown: false,
                    overrides: overrides),
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

        /// <summary>An empty set of adjustments, and one asking for one thing.</summary>
        private static CardVisualOverrides Asking(System.Action<CardTextOverride> what)
        {
            CardVisualOverrides overrides = new CardVisualOverrides();
            what(overrides.Establish(CardVisualTextSlot.Name));
            return overrides;
        }

        // ------------------------------------------------------------------
        //  Asking for nothing
        // ------------------------------------------------------------------

        [Test]
        public void A_card_with_no_overrides_composes_exactly_as_its_recipe_says()
        {
            CardVisualPlannedLayer plain = Compose(null);

            Rect rect = plain.Rect;
            float fontSize = plain.FontSize;
            CardTextStyle style = plain.TextStyle;

            // An empty set, which is not the same thing as none at all: a card
            // whose entry exists but asks for nothing must still be untouched.
            CardVisualPlannedLayer empty = Compose(new CardVisualOverrides());

            Assert.That(empty.Rect, Is.EqualTo(rect));
            Assert.That(empty.FontSize, Is.EqualTo(fontSize).Within(0.0001f));
            Assert.That(empty.TextStyle.Tracking, Is.EqualTo(style.Tracking).Within(0.0001f));
            Assert.That(empty.TextStyle.MinCondense, Is.EqualTo(style.MinCondense).Within(0.0001f));
            Assert.That(empty.TextStyle.CurveControlB, Is.EqualTo(style.CurveControlB));
        }

        /// <summary>
        /// And an entry that exists but has every field switched off is the same
        /// again. This is the case a tool leaves behind after somebody ticks an
        /// override on and then off, and it must not linger as a change.
        /// </summary>
        [Test]
        public void An_override_switched_off_leaves_no_trace()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualOverrides overrides = Asking(polish =>
            {
                polish.offsetX = new OptionalNumber(40f);
                polish.fontSizeMultiplier = new OptionalNumber(1.5f);
            });

            Assert.That(Compose(overrides).Rect, Is.Not.EqualTo(plain.Rect));

            overrides.For(CardVisualTextSlot.Name).Clear();

            CardVisualPlannedLayer after = Compose(overrides);

            Assert.That(after.Rect, Is.EqualTo(plain.Rect));
            Assert.That(after.FontSize, Is.EqualTo(plain.FontSize).Within(0.0001f));
        }

        // ------------------------------------------------------------------
        //  Asking for something
        // ------------------------------------------------------------------

        [Test]
        public void An_offset_moves_the_title_and_changes_nothing_else()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualPlannedLayer moved = Compose(Asking(polish =>
            {
                polish.offsetX = new OptionalNumber(12f);
                polish.offsetY = new OptionalNumber(-7f);
            }));

            Assert.That(moved.Rect.x, Is.EqualTo(plain.Rect.x + 12f).Within(0.0001f));
            Assert.That(moved.Rect.y, Is.EqualTo(plain.Rect.y - 7f).Within(0.0001f));
            Assert.That(moved.Rect.width, Is.EqualTo(plain.Rect.width).Within(0.0001f));
            Assert.That(moved.Rect.height, Is.EqualTo(plain.Rect.height).Within(0.0001f));
            Assert.That(moved.FontSize, Is.EqualTo(plain.FontSize).Within(0.0001f));
        }

        /// <summary>
        /// Widening a title widens it in place. Scaling from the left edge
        /// instead would drag the word sideways every time somebody asked for a
        /// little more room, which is not what anybody means by wider.
        /// </summary>
        [Test]
        public void A_width_multiplier_grows_the_title_about_its_own_middle()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualPlannedLayer wider = Compose(Asking(polish =>
                polish.widthMultiplier = new OptionalNumber(1.2f)));

            Assert.That(wider.Rect.width, Is.EqualTo(plain.Rect.width * 1.2f).Within(0.0001f));

            Assert.That(wider.Rect.center.x, Is.EqualTo(plain.Rect.center.x).Within(0.0001f),
                "Widening the title moved it.");
        }

        [Test]
        public void A_font_size_multiplier_scales_the_ceiling_the_recipe_set()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualPlannedLayer bigger = Compose(Asking(polish =>
                polish.fontSizeMultiplier = new OptionalNumber(1.25f)));

            Assert.That(bigger.FontSize, Is.EqualTo(plain.FontSize * 1.25f).Within(0.0001f));

            // And the floor never ends up above the ceiling, whatever is asked
            // for: a label that may not shrink below a size it may not reach
            // cannot be laid out at all.
            CardVisualPlannedLayer tiny = Compose(Asking(polish =>
                polish.fontSizeMultiplier = new OptionalNumber(0.01f)));

            Assert.That(tiny.FontSizeMin, Is.LessThanOrEqualTo(tiny.FontSize));
        }

        [Test]
        public void Warp_strength_scales_the_arc_without_replacing_its_shape()
        {
            CardVisualPlannedLayer plain = Compose(null);

            Assert.That(plain.TextStyle.IsWarped, Is.True,
                "The minion title is not warped, so this proves nothing.");

            CardVisualPlannedLayer gentler = Compose(Asking(polish =>
                polish.warpStrength = new OptionalNumber(0.5f)));

            Assert.That(gentler.TextStyle.CurveControlB.y,
                Is.EqualTo(plain.TextStyle.CurveControlB.y * 0.5f).Within(0.0001f));

            // Across, not up: the shape of the curve is the recipe's business
            // and only its depth is the card's.
            Assert.That(gentler.TextStyle.CurveControlB.x,
                Is.EqualTo(plain.TextStyle.CurveControlB.x).Within(0.0001f));

            // Flattened, not switched off. The vertical scale and the
            // foreshortening are still the style's and still apply.
            CardVisualPlannedLayer flat = Compose(Asking(polish =>
                polish.warpStrength = new OptionalNumber(0f)));

            Assert.That(flat.TextStyle.CurveControlB.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(flat.TextStyle.IsWarped, Is.True);
            Assert.That(flat.TextStyle.Stretch,
                Is.EqualTo(plain.TextStyle.Stretch).Within(0.0001f));
        }

        [Test]
        public void A_condense_multiplier_moves_the_floor_and_is_kept_sane()
        {
            CardVisualPlannedLayer plain = Compose(null);

            CardVisualPlannedLayer looser = Compose(Asking(polish =>
                polish.condenseMultiplier = new OptionalNumber(0.8f)));

            Assert.That(looser.TextStyle.MinCondense,
                Is.EqualTo(plain.TextStyle.MinCondense * 0.8f).Within(0.0001f));

            // Nobody gets to squeeze a title out of existence, however hard they
            // ask.
            CardVisualPlannedLayer absurd = Compose(Asking(polish =>
                polish.condenseMultiplier = new OptionalNumber(0.001f)));

            Assert.That(absurd.TextStyle.MinCondense, Is.GreaterThanOrEqualTo(0.2f));
        }

        // ------------------------------------------------------------------
        //  Reshaping one card's baseline
        // ------------------------------------------------------------------

        /// <summary>
        /// A card can ask for a shallower arch without saying anything about
        /// where its top sits or which way it leans.
        ///
        /// Whichever of the three it leaves alone comes from the style, which is
        /// what makes these adjustments rather than a replacement: a card that
        /// wants a gentler curve should not silently lose the off centre top its
        /// recipe gave it.
        /// </summary>
        [Test]
        public void A_card_can_reshape_part_of_its_baseline_and_inherit_the_rest()
        {
            // A spell, because its banner is a plain arch. The minion's is an S
            // and cannot be described by these three at all, so touching any of
            // them replaces its shape — which is the documented cost of doing
            // so and the wrong thing to measure "inherits the rest" against.
            CardVisualPlannedLayer plain = Compose(null, "Test Volley", CardType.Spell);

            CardTextCurve inherited = CardTextCurve.From(
                plain.TextStyle.CurveControlA,
                plain.TextStyle.CurveControlB,
                plain.TextStyle.CurveEnd);

            Assert.That(
                CardTextCurve.Fits(
                    plain.TextStyle.CurveControlA,
                    plain.TextStyle.CurveControlB,
                    plain.TextStyle.CurveEnd),
                Is.True,
                "The spell banner is meant to be a plain arch.");

            CardVisualPlannedLayer gentler = Compose(
                Asking(polish => polish.curveAmount = new OptionalNumber(inherited.Amount * 0.5f)),
                "Test Volley",
                CardType.Spell);

            CardTextCurve after = CardTextCurve.From(
                gentler.TextStyle.CurveControlA,
                gentler.TextStyle.CurveControlB,
                gentler.TextStyle.CurveEnd);

            Assert.That(after.Amount, Is.EqualTo(inherited.Amount * 0.5f).Within(0.002f),
                "The card's arch is not the depth it asked for.");

            Assert.That(after.Tilt, Is.EqualTo(inherited.Tilt).Within(0.002f),
                "Asking for a shallower arch also changed the lean of the baseline.");

            Assert.That(after.Centre, Is.EqualTo(inherited.Centre).Within(0.02f),
                "Asking for a shallower arch also moved the top of it.");
        }

        [Test]
        public void A_card_can_tilt_and_recentre_its_baseline()
        {
            CardVisualPlannedLayer leaning = Compose(Asking(polish =>
            {
                polish.curveTilt = new OptionalNumber(-0.05f);
                polish.curveCentre = new OptionalNumber(0.4f);
            }));

            CardTextCurve curve = CardTextCurve.From(
                leaning.TextStyle.CurveControlA,
                leaning.TextStyle.CurveControlB,
                leaning.TextStyle.CurveEnd);

            Assert.That(curve.Tilt, Is.EqualTo(-0.05f).Within(0.002f));
            Assert.That(curve.Centre, Is.EqualTo(0.4f).Within(0.02f));
        }

        /// <summary>
        /// And a card that says nothing about its baseline keeps the recipe's,
        /// control point for control point.
        /// </summary>
        [Test]
        public void A_card_that_says_nothing_about_its_baseline_keeps_the_recipes()
        {
            CardVisualPlannedLayer plain = Compose(null);

            // Something else entirely, so the curve is only left alone because
            // nothing asked about it.
            CardVisualPlannedLayer moved = Compose(Asking(polish =>
                polish.offsetX = new OptionalNumber(9f)));

            Assert.That(moved.TextStyle.CurveControlA, Is.EqualTo(plain.TextStyle.CurveControlA));
            Assert.That(moved.TextStyle.CurveControlB, Is.EqualTo(plain.TextStyle.CurveControlB));
            Assert.That(moved.TextStyle.CurveEnd, Is.EqualTo(plain.TextStyle.CurveEnd));
        }

        /// <summary>
        /// Reshaping the baseline and scaling it are two different knobs, and
        /// they apply in that order: the shape is settled first, then how
        /// strongly it is drawn.
        /// </summary>
        [Test]
        public void Warp_strength_scales_whatever_shape_the_card_ended_up_with()
        {
            CardVisualPlannedLayer both = Compose(Asking(polish =>
            {
                polish.curveAmount = new OptionalNumber(0.1f);
                polish.warpStrength = new OptionalNumber(0.5f);
            }));

            CardTextCurve curve = CardTextCurve.From(
                both.TextStyle.CurveControlA,
                both.TextStyle.CurveControlB,
                both.TextStyle.CurveEnd);

            Assert.That(curve.Amount, Is.EqualTo(0.05f).Within(0.002f),
                "An arch of 0.1 drawn at half strength should read as 0.05.");
        }

        // ------------------------------------------------------------------
        //  One card at a time
        // ------------------------------------------------------------------

        /// <summary>
        /// Polishing one card leaves every other card alone.
        ///
        /// The point of the whole mechanism, and easy to lose: adjustments held
        /// anywhere shared — on the recipe, on the style, in a static — would
        /// reach every card set the same way.
        /// </summary>
        [Test]
        public void Polishing_one_card_does_not_touch_another()
        {
            CardVisualPlannedLayer soldier = Compose(null, "Test Soldier");
            Rect before = soldier.Rect;

            Compose(Asking(polish => polish.offsetY = new OptionalNumber(30f)), "Test Quartermaster");

            CardVisualPlannedLayer again = Compose(null, "Test Soldier");

            Assert.That(again.Rect, Is.EqualTo(before),
                "Adjusting one card changed another, so the adjustment is not the card's own.");
        }

        /// <summary>
        /// And the slot is respected: a title's adjustments do not reach the
        /// rules text sitting under it.
        /// </summary>
        [Test]
        public void An_adjustment_reaches_only_the_slot_it_names()
        {
            CardVisualFactory factory = Factory();

            CardVisualOverrides overrides = Asking(polish =>
                polish.offsetY = new OptionalNumber(25f));

            factory.Compose(
                new CardVisualDescriptor(
                    CardType.Minion, CardClass.Neutral, Rarity.Common, Tribe.None,
                    artwork: null, name: "Test Soldier", rulesText: "Taunt.",
                    manaCost: 2, attack: 2, health: 3,
                    showsCost: true, showsStatistics: true,
                    style: default, secondaryClass: CardClass.Neutral, expansion: "",
                    faceDown: false, overrides: overrides),
                _plan);

            Rect title = default;
            Rect rules = default;

            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (_plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                {
                    title = _plan.Layers[index].Rect;
                }

                if (_plan.Layers[index].TextSlot == CardVisualTextSlot.RulesText)
                {
                    rules = _plan.Layers[index].Rect;
                }
            }

            CardVisualPlannedLayer plain = Compose(null);

            Assert.That(title.y, Is.EqualTo(plain.Rect.y + 25f).Within(0.0001f),
                "The title was not moved.");

            CardVisualRecipeAsset recipe = AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(
                "Assets/_Project/Data/CardVisuals/CardVisualRecipe_Standard.asset");

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer != null && layer.text == CardVisualTextSlot.RulesText &&
                    Mathf.Approximately(layer.y, rules.y))
                {
                    return;
                }
            }

            Assert.Fail("The rules text moved with the title.");
        }

        // ------------------------------------------------------------------
        //  Where they live
        // ------------------------------------------------------------------

        /// <summary>
        /// The composer still knows nothing about which card it is drawing.
        ///
        /// The identity is used exactly once, to look a set of numbers up, and
        /// what travels on is those numbers. This is the property the whole
        /// design turns on, so it is checked at the source rather than trusted.
        /// </summary>
        [Test]
        public void Nothing_that_composes_a_card_can_ask_which_card_it_is()
        {
            string[] deciding =
            {
                "Assets/_Project/Presentation/CardVisuals/CardVisualComposer.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualOverrides.cs",
                "Assets/_Project/Presentation/CardVisuals/CardTextWarp.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualPainter.cs"
            };

            foreach (string path in deciding)
            {
                string source = System.IO.File.ReadAllText(path);

                Assert.That(source.Contains("CardId"), Is.False,
                    System.IO.Path.GetFileName(path) + " mentions a card id.");
            }
        }
    }
}
