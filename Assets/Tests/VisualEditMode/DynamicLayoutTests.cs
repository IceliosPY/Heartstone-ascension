using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// Where a card's words and its painting go, and why that is data.
    ///
    /// The pictures were the easy half. A card looks finished or unfinished
    /// depending on whether its name fits its banner, its numbers sit on their
    /// gems and its painting fills its window without being squashed — and none
    /// of that can be a constant in a renderer, because a minion's banner and a
    /// spell's banner are not the same shape or in the same place.
    ///
    /// So these check the geometry rather than the pixels. A screenshot test
    /// would break every time anybody nudged anything; these break only when a
    /// card would actually be wrong.
    /// </summary>
    public sealed class DynamicLayoutTests
    {
        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private static CardVisualFactory Factory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null,
                "No card visual factory. Run Conquest of Hearthstone -> Create Missing Card Visual Assets.");

            return factory;
        }

        private static Sprite AnyArtwork() =>
            Factory().Library != null ? Factory().Library.ArtworkFor(default) : null;

        private CardVisualPlan Compose(
            CardType type,
            Rarity rarity = Rarity.Common,
            string name = "Test Soldier",
            string rules = "",
            Tribe tribe = Tribe.None)
        {
            Factory().Compose(
                new CardVisualDescriptor(
                    type,
                    CardClass.Neutral,
                    rarity,
                    tribe,
                    AnyArtwork(),
                    name,
                    rules,
                    manaCost: 2,
                    attack: 2,
                    health: 3,
                    showsCost: true,
                    showsStatistics: type == CardType.Minion || type == CardType.Weapon),
                _plan);

            return _plan;
        }

        private bool TryFindText(CardVisualTextSlot slot, out CardVisualPlannedLayer found)
        {
            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (_plan.Layers[index].TextSlot == slot)
                {
                    found = _plan.Layers[index];
                    return true;
                }
            }

            found = default;
            return false;
        }

        private CardVisualPlannedLayer Text(CardVisualTextSlot slot)
        {
            Assert.That(TryFindText(slot, out CardVisualPlannedLayer found), Is.True,
                "No " + slot + " label was composed.\n" + _plan.DescribeResolution());

            return found;
        }

        private CardVisualPlannedLayer Picture(CardVisualSlot slot)
        {
            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (!_plan.Layers[index].IsText && _plan.Layers[index].Slot == slot)
                {
                    return _plan.Layers[index];
                }
            }

            Assert.Fail("Nothing was drawn in the " + slot + " slot.\n" + _plan.DescribeResolution());
            return default;
        }

        /// <summary>
        /// Whether one rectangle sits on another, give or take a little.
        ///
        /// The margin is deliberate and is about what these rectangles are. A
        /// number's rectangle is the box its text is laid out in, not the ink:
        /// a single centred digit in a box a hundred and fifty pixels wide draws
        /// perhaps sixty of them, in the middle. So a box that overhangs its gem
        /// by a few pixels has nothing hanging over anything, and holding the
        /// boxes to the pixel would fail a card that looks perfectly right while
        /// still missing the failure worth catching — a number on the wrong gem,
        /// or off the card altogether.
        ///
        /// Two per cent of the canvas, which is sixteen pixels: far less than a
        /// gem, far more than a nudge.
        /// </summary>
        private const float Slack = 16f;

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - Slack &&
            inner.yMin >= outer.yMin - Slack &&
            inner.xMax <= outer.xMax + Slack &&
            inner.yMax <= outer.yMax + Slack;

        // ------------------------------------------------------------------
        //  Both kinds of card have somewhere to put their words
        // ------------------------------------------------------------------

        [Test]
        public void Both_kinds_of_card_have_a_name_and_a_rules_slot()
        {
            foreach (CardType type in new[] { CardType.Minion, CardType.Spell })
            {
                Compose(type, rules: "Something happens.");

                Assert.That(TryFindText(CardVisualTextSlot.Name, out CardVisualPlannedLayer name), Is.True,
                    type + " has nowhere to print its name.");
                Assert.That(name.Rect.width, Is.GreaterThan(0f));

                Assert.That(TryFindText(CardVisualTextSlot.RulesText, out CardVisualPlannedLayer rules), Is.True,
                    type + " has nowhere to print its rules.");
                Assert.That(rules.Rect.width, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void A_minion_prints_an_attack_and_a_health_and_a_spell_prints_neither()
        {
            Compose(CardType.Minion);

            Assert.That(TryFindText(CardVisualTextSlot.Attack, out CardVisualPlannedLayer _), Is.True);
            Assert.That(TryFindText(CardVisualTextSlot.Health, out CardVisualPlannedLayer _), Is.True);

            Compose(CardType.Spell);

            Assert.That(TryFindText(CardVisualTextSlot.Attack, out CardVisualPlannedLayer _), Is.False,
                "A spell printed an attack.");
            Assert.That(TryFindText(CardVisualTextSlot.Health, out CardVisualPlannedLayer _), Is.False);
        }

        // ------------------------------------------------------------------
        //  A label belongs to the thing it is printed on
        // ------------------------------------------------------------------

        /// <summary>
        /// Every number sits inside the gem it belongs to. Which is the point of
        /// the slots being data: moving a gem moves its number, because the two
        /// were measured against each other rather than against the card.
        /// </summary>
        [Test]
        public void Every_number_sits_on_its_own_gem()
        {
            Compose(CardType.Minion);

            Assert.That(Contains(Picture(CardVisualSlot.ManaGem).Rect, Text(CardVisualTextSlot.ManaCost).Rect),
                Is.True, "The cost is not on the mana gem.");

            Assert.That(Contains(Picture(CardVisualSlot.AttackGem).Rect, Text(CardVisualTextSlot.Attack).Rect),
                Is.True, "The attack is not on the attack gem.");

            Assert.That(Contains(Picture(CardVisualSlot.HealthGem).Rect, Text(CardVisualTextSlot.Health).Rect),
                Is.True, "The health is not on the health gem. gem " +
                Picture(CardVisualSlot.HealthGem).Rect + " text " +
                Text(CardVisualTextSlot.Health).Rect + " | " + _plan.DescribeResolution());
        }

        [Test]
        public void The_name_sits_on_its_banner_and_the_rules_on_their_panel()
        {
            foreach (CardType type in new[] { CardType.Minion, CardType.Spell })
            {
                Compose(type, rules: "Something happens.");

                Assert.That(
                    Contains(Picture(CardVisualSlot.NameBanner).Rect, Text(CardVisualTextSlot.Name).Rect),
                    Is.True, type + "'s name is not on its banner.");

                Assert.That(
                    Contains(Picture(CardVisualSlot.RulesPanel).Rect, Text(CardVisualTextSlot.RulesText).Rect),
                    Is.True, type + "'s rules are not on their panel.");
            }
        }

        /// <summary>
        /// A long name does not move or resize its slot. The box is the design;
        /// the text fits itself to the box, and a card with a long name is the
        /// same card with smaller letters rather than a different layout.
        /// </summary>
        [Test]
        public void A_long_name_does_not_change_where_the_name_goes()
        {
            Compose(CardType.Minion, name: "Test Soldier");
            Rect shortName = Text(CardVisualTextSlot.Name).Rect;

            foreach (string name in new[]
            {
                "Test Battlecry Damage",
                "Test Deathrattle Draw",
                "A Name Considerably Longer Than Any Of These"
            })
            {
                Compose(CardType.Minion, name: name);

                Assert.That(Text(CardVisualTextSlot.Name).Rect, Is.EqualTo(shortName),
                    "'" + name + "' moved the name slot.");
                Assert.That(
                    Contains(Picture(CardVisualSlot.NameBanner).Rect, Text(CardVisualTextSlot.Name).Rect),
                    Is.True, "'" + name + "' escaped the banner.");
            }
        }

        /// <summary>
        /// Every label may shrink, and every label has a floor. Without a
        /// ceiling a short word becomes a billboard; without a floor a long one
        /// shrinks until nobody can read it and the card quietly gets worse the
        /// more you write on it.
        /// </summary>
        [Test]
        public void Every_label_can_resize_and_none_can_vanish()
        {
            Compose(CardType.Minion, rules: "Deathrattle: Draw a card.", tribe: Tribe.None);

            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                CardVisualPlannedLayer layer = _plan.Layers[index];

                if (!layer.IsText)
                {
                    continue;
                }

                Assert.That(layer.FontSize, Is.GreaterThan(0f), layer.TextSlot + " has no size at all.");
                Assert.That(layer.FontSizeMin, Is.GreaterThan(0f),
                    layer.TextSlot + " may shrink to nothing.");
                Assert.That(layer.FontSizeMin, Is.LessThanOrEqualTo(layer.FontSize),
                    layer.TextSlot + " can only grow.");
            }
        }

        [Test]
        public void A_number_never_wraps_and_a_sentence_always_does()
        {
            Compose(CardType.Minion, rules: "Deathrattle: Draw a card.");

            Assert.That(Text(CardVisualTextSlot.ManaCost).Wrap, Is.False, "Ten wrapped onto two lines.");
            Assert.That(Text(CardVisualTextSlot.Attack).Wrap, Is.False);
            Assert.That(Text(CardVisualTextSlot.Health).Wrap, Is.False);
            Assert.That(Text(CardVisualTextSlot.Name).Wrap, Is.False);

            Assert.That(Text(CardVisualTextSlot.RulesText).Wrap, Is.True, "Rules text ran off the card.");
        }

        /// <summary>
        /// The rendered words fit inside the box they were given.
        ///
        /// The test above checks that the box does not move, which it never
        /// did — and a long name still ran off both edges of the card, because
        /// a floor it could not shrink past left it nowhere to go. A geometry
        /// test that never looks at the text is a test that agrees with a bug.
        ///
        /// So this one actually lays the text out and measures it.
        /// </summary>
        [Test]
        public void A_long_name_shrinks_until_it_fits_rather_than_running_off_the_card()
        {
            foreach (string name in new[]
            {
                "Test Soldier",
                "Test Battlecry Damage",
                "Test Deathrattle Draw"
            })
            {
                Compose(CardType.Minion, name: name);
                AssertRenderedInside(Text(CardVisualTextSlot.Name), name);
            }
        }

        [Test]
        public void Rules_text_wraps_and_shrinks_until_it_fits_its_parchment()
        {
            foreach (string rules in new[]
            {
                "Draw a card.",
                "Deathrattle: Draw a card.",
                "Battlecry: Deal 2 damage to a chosen enemy character, then draw a card."
            })
            {
                Compose(CardType.Minion, rules: rules);
                AssertRenderedInside(Text(CardVisualTextSlot.RulesText), rules);
            }
        }

        [Test]
        public void Two_digit_numbers_still_fit_their_gems_when_laid_out()
        {
            foreach (int value in new[] { 0, 1, 9, 10, 11, 20, 30, 99 })
            {
                Factory().Compose(
                    new CardVisualDescriptor(
                        CardType.Minion, CardClass.Neutral, Rarity.Common, Tribe.None,
                        AnyArtwork(), "Test", "", value, value, value,
                        showsCost: true, showsStatistics: true),
                    _plan);

                AssertRenderedInside(Text(CardVisualTextSlot.ManaCost), value.ToString());
                AssertRenderedInside(Text(CardVisualTextSlot.Attack), value.ToString());
                AssertRenderedInside(Text(CardVisualTextSlot.Health), value.ToString());
            }
        }

        /// <summary>
        /// Lays a label out for real - the real prefab, the real font, the
        /// real warp - and measures what actually came out of it.
        ///
        /// This used to build a bare TextMeshPro of its own instead of going
        /// through <see cref="CardPreviewCard"/>. Two things were wrong with
        /// that, both silent: it never assigned a font, so it measured
        /// whatever TextMeshPro falls back to project-wide (LiberationSans SDF)
        /// rather than the card's own title or stat face; and it read
        /// <c>textBounds</c>, which reports the *typeset* extent from before
        /// any warp runs, not the mesh a card-specific style like a curved or
        /// condensed number actually ends up with. A style that squeezes a
        /// two-digit value to fit its gem - which is real, on the card, and
        /// the fix this test exists to guard - was consequently invisible to
        /// it in both directions: it could not have failed a style that
        /// condensed too little, and it could not confirm one that condenses
        /// correctly either.
        ///
        /// So this composes onto the real prefab and reads the mesh the way
        /// <see cref="CardTextWarp"/> itself does for the same reason it does:
        /// not the box TextMeshPro laid the text out in, the space the glyphs
        /// actually occupy afterwards.
        ///
        /// Width always: text running off the side of a banner is the failure
        /// that made this test necessary, and it is always visible.
        ///
        /// Height only for text that wraps, and only from <c>textBounds</c> -
        /// wrapped text is never warped in this recipe, so the two agree there,
        /// and a single centred line reports the full height of its font
        /// (ascender to descender, most of which no digit uses) whichever way
        /// it is measured. Asserting on that would be asserting on font
        /// metrics rather than on layout.
        /// </summary>
        private void AssertRenderedInside(CardVisualPlannedLayer layer, string what)
        {
            GameObject stage = new GameObject("Measuring") { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                CardVisualPainter painter = CardPreviewCard.Make(stage.transform, out GameObject card);
                painter.Apply(_plan);

                TMPro.TextMeshPro label = FindPaintedLabel(card, layer);

                Assert.That(label, Is.Not.Null,
                    "No painted label reads \"" + what + "\" for layer '" + layer.LayerName + "'.");

                Vector2 box = CardCanvas.ToLocalSize(layer.Rect);
                float renderedWidth = MeshWidth(label);

                const float slack = 1.06f;

                Assert.That(renderedWidth, Is.LessThanOrEqualTo(box.x * slack),
                    "\"" + what + "\" renders " + renderedWidth.ToString("0.000") +
                    " wide in a box " + box.x.ToString("0.000") + " wide, from layer '" +
                    layer.LayerName + "' rect " + layer.Rect + ".");

                if (layer.Wrap)
                {
                    float renderedHeight = label.textBounds.size.y;

                    Assert.That(renderedHeight, Is.LessThanOrEqualTo(box.y * slack),
                        "\"" + what + "\" renders " + renderedHeight.ToString("0.000") +
                        " tall in a box " + box.y.ToString("0.000") + " tall.");
                }
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }
        }

        /// <summary>The painted label a plan layer describes, found by its text and its place on the card.</summary>
        private static TMPro.TextMeshPro FindPaintedLabel(GameObject card, in CardVisualPlannedLayer layer)
        {
            Vector3 wanted = CardCanvas.ToLocalPosition(layer.Rect, layer.SortingOrder);

            TMPro.TextMeshPro found = null;
            float closest = float.MaxValue;

            foreach (TMPro.TextMeshPro label in card.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                if (label.text != layer.Text)
                {
                    continue;
                }

                float distance = Vector2.Distance(
                    new Vector2(label.transform.localPosition.x, label.transform.localPosition.y),
                    new Vector2(wanted.x, wanted.y));

                if (distance < closest)
                {
                    closest = distance;
                    found = label;
                }
            }

            return found;
        }

        /// <summary>
        /// How wide the glyphs actually are, measured off the mesh - the same
        /// way <see cref="CardTextWarp"/> measures before deciding whether to
        /// condense, and the only measurement a warp or a condense actually
        /// shows up in. <c>textBounds</c> is the typeset extent from before
        /// either runs.
        /// </summary>
        private static float MeshWidth(TMPro.TextMeshPro label)
        {
            TMPro.TMP_TextInfo info = label.textInfo;

            float left = float.MaxValue;
            float right = float.MinValue;

            for (int index = 0; index < info.characterCount; index++)
            {
                TMPro.TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible)
                {
                    continue;
                }

                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int at = character.vertexIndex;

                for (int corner = 0; corner < 4; corner++)
                {
                    left = Mathf.Min(left, vertices[at + corner].x);
                    right = Mathf.Max(right, vertices[at + corner].x);
                }
            }

            return right > left ? right - left : 0f;
        }

        // ------------------------------------------------------------------
        //  Artwork
        // ------------------------------------------------------------------

        /// <summary>
        /// The painting fills its window and is clipped to its shape. Cover
        /// rather than Stretch, because a painting is whatever shape it was
        /// painted and squashing it into a window is never right.
        /// </summary>
        [Test]
        public void The_artwork_covers_its_window_and_is_clipped_to_it()
        {
            foreach (CardType type in new[] { CardType.Minion, CardType.Spell })
            {
                Compose(type);

                CardVisualPlannedLayer artwork = Picture(CardVisualSlot.Artwork);

                Assert.That(artwork.Fill, Is.EqualTo(CardVisualFill.Cover),
                    type + "'s painting is stretched to its window.");
                Assert.That(artwork.Mask, Is.Not.Null,
                    type + "'s painting is not clipped to anything.");
            }
        }

        [Test]
        public void A_minion_and_a_spell_are_clipped_to_different_shapes()
        {
            Compose(CardType.Minion);
            Sprite minion = Picture(CardVisualSlot.Artwork).Mask;

            Compose(CardType.Spell);
            Sprite spell = Picture(CardVisualSlot.Artwork).Mask;

            Assert.That(minion, Is.Not.SameAs(spell),
                "A spell's rectangular window and a minion's oval one are the same shape.");
        }

        [Test]
        public void The_artwork_stays_behind_the_frame()
        {
            foreach (CardType type in new[] { CardType.Minion, CardType.Spell })
            {
                Compose(type);

                Assert.That(
                    Picture(CardVisualSlot.Artwork).SortingOrder,
                    Is.LessThan(Picture(CardVisualSlot.Frame).SortingOrder),
                    type + " draws its painting over its frame.");
            }
        }

        [Test]
        public void Every_label_draws_in_front_of_what_it_is_printed_on()
        {
            Compose(CardType.Minion, rules: "Deathrattle: Draw a card.");

            Assert.That(Text(CardVisualTextSlot.Name).SortingOrder,
                Is.GreaterThan(Picture(CardVisualSlot.NameBanner).SortingOrder));
            Assert.That(Text(CardVisualTextSlot.RulesText).SortingOrder,
                Is.GreaterThan(Picture(CardVisualSlot.RulesPanel).SortingOrder));
            Assert.That(Text(CardVisualTextSlot.ManaCost).SortingOrder,
                Is.GreaterThan(Picture(CardVisualSlot.ManaGem).SortingOrder));
        }

        // ------------------------------------------------------------------
        //  Switching type recomposes the geometry
        // ------------------------------------------------------------------

        [Test]
        public void Becoming_a_spell_moves_every_slot_that_should_move()
        {
            Compose(CardType.Minion, rules: "Text.");

            Rect minionName = Text(CardVisualTextSlot.Name).Rect;
            Rect minionRules = Text(CardVisualTextSlot.RulesText).Rect;
            Rect minionArt = Picture(CardVisualSlot.Artwork).Rect;

            Compose(CardType.Spell, rules: "Text.");

            Assert.That(Text(CardVisualTextSlot.Name).Rect, Is.Not.EqualTo(minionName));
            Assert.That(Text(CardVisualTextSlot.RulesText).Rect, Is.Not.EqualTo(minionRules));
            Assert.That(Picture(CardVisualSlot.Artwork).Rect, Is.Not.EqualTo(minionArt));
        }

        // ------------------------------------------------------------------
        //  The awkward values
        // ------------------------------------------------------------------

        [Test]
        public void An_empty_rules_text_draws_no_label_at_all()
        {
            Compose(CardType.Minion, rules: "");

            Assert.That(TryFindText(CardVisualTextSlot.RulesText, out CardVisualPlannedLayer _), Is.False,
                "An empty label was drawn, which is a blank line waiting to push something out of place.");

            // The parchment is part of the frame's own picture, so it stays.
            Assert.That(_plan.Draws(CardVisualSlot.RulesPanel), Is.False,
                "A card with no rules drew a panel to print them on.");
        }

        [Test]
        public void A_card_with_no_tribe_prints_no_tribe()
        {
            Compose(CardType.Minion, tribe: Tribe.None);

            Assert.That(TryFindText(CardVisualTextSlot.Tribe, out CardVisualPlannedLayer _), Is.False,
                "A card with no tribe printed the word None.");
            Assert.That(_plan.Draws(CardVisualSlot.TribeBanner), Is.False);
        }

        /// <summary>
        /// Two digits fit where one does. The box is wide enough that ten does
        /// not spill over the rim of a gem sized for a single figure.
        /// </summary>
        [Test]
        public void Two_digit_numbers_are_the_same_shape_as_one_digit_numbers()
        {
            foreach (int cost in new[] { 0, 1, 2, 9, 10 })
            {
                Factory().Compose(
                    new CardVisualDescriptor(
                        CardType.Minion, CardClass.Neutral, Rarity.Common, Tribe.None,
                        AnyArtwork(), "Test", "", cost, cost, cost,
                        showsCost: true, showsStatistics: true),
                    _plan);

                Assert.That(Text(CardVisualTextSlot.ManaCost).Text, Is.EqualTo(cost.ToString()));
                Assert.That(
                    Contains(Picture(CardVisualSlot.ManaGem).Rect, Text(CardVisualTextSlot.ManaCost).Rect),
                    Is.True, cost + " does not fit on the gem.");
            }
        }

        // ------------------------------------------------------------------
        //  Nothing about this depends on which card it is
        // ------------------------------------------------------------------

        [Test]
        public void Two_differently_named_cards_of_one_kind_have_identical_layouts()
        {
            Compose(CardType.Minion, name: "Test Soldier", rules: "Deathrattle: Draw a card.");
            List<Rect> first = Geometry();

            Compose(CardType.Minion, name: "The Coin", rules: "Gain one Mana Crystal this turn only.");
            List<Rect> second = Geometry();

            Assert.That(second, Is.EqualTo(first),
                "Two minions laid out differently, which means something read the card rather than its kind.");
        }

        private List<Rect> Geometry()
        {
            List<Rect> rects = new List<Rect>();

            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                rects.Add(_plan.Layers[index].Rect);
            }

            return rects;
        }

        /// <summary>
        /// A card type with no layout of its own still gets a complete one. The
        /// fallback is a stated default, not an absence.
        /// </summary>
        [Test]
        public void A_type_with_no_layout_of_its_own_still_lays_out_completely()
        {
            Compose(CardType.Weapon, rules: "Something.");

            Assert.That(_plan.IsComplete, Is.True, _plan.DescribeResolution());
            Assert.That(_plan.Draws(CardVisualSlot.Artwork), Is.True);
            Assert.That(TryFindText(CardVisualTextSlot.Name, out CardVisualPlannedLayer _), Is.True);
            Assert.That(TryFindText(CardVisualTextSlot.RulesText, out CardVisualPlannedLayer _), Is.True);
        }
    }
}
