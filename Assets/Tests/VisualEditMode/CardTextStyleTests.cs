using System.Collections.Generic;
using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// How a card's writing is styled, and the one property that matters about
    /// it: that a minion title and a spell title are the same renderer given
    /// different numbers.
    ///
    /// That claim is easy to make and easy to lose. The moment somebody adds
    /// "if this is a spell" to the thing that draws titles, the system has
    /// quietly become two systems that happen to share a file, and every later
    /// card type costs another branch. So these check the shape of the design
    /// rather than the look of the result: that the two styles differ, that
    /// they differ only in data, that nothing about *which* card is being drawn
    /// reaches them, and that the warp moves glyphs without moving the slot
    /// underneath them.
    ///
    /// Nothing here compares pixels. A test that did would break every time
    /// anybody nudged a rectangle, which is exactly what the layout tool is for.
    /// </summary>
    public sealed class CardTextStyleTests
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

        private CardVisualPlan Compose(
            CardType type,
            string name = "Test Soldier",
            string rules = "",
            Tribe tribe = Tribe.None,
            int cost = 2,
            Rarity rarity = Rarity.Common)
        {
            CardVisualFactory factory = Factory();

            factory.Compose(
                new CardVisualDescriptor(
                    type,
                    CardClass.Neutral,
                    rarity,
                    tribe,
                    factory.Library != null ? factory.Library.ArtworkFor(default) : null,
                    name,
                    rules,
                    manaCost: cost,
                    attack: 2,
                    health: 3,
                    showsCost: true,
                    showsStatistics: type == CardType.Minion || type == CardType.Weapon),
                _plan);

            return _plan;
        }

        private CardVisualPlannedLayer TextLayer(CardVisualTextSlot slot)
        {
            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (_plan.Layers[index].TextSlot == slot)
                {
                    return _plan.Layers[index];
                }
            }

            Assert.Fail("The composed card has no " + slot + " layer.");
            return default;
        }

        // ------------------------------------------------------------------
        //  Same renderer, different data
        // ------------------------------------------------------------------

        [Test]
        public void A_minion_and_a_spell_are_titled_in_different_styles()
        {
            Compose(CardType.Minion);
            CardTextStyle minion = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Compose(CardType.Spell);
            CardTextStyle spell = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Assert.That(minion.Name, Is.Not.EqualTo(spell.Name),
                "A minion and a spell are titled in the same style, so the recipe cannot tell " +
                "them apart.");
        }

        /// <summary>
        /// And they differ only in data. Same role, same render mode: whatever
        /// draws one draws the other, with different numbers.
        /// </summary>
        [Test]
        public void Both_titles_are_drawn_the_same_way_and_differ_only_in_their_numbers()
        {
            Compose(CardType.Minion);
            CardTextStyle minion = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Compose(CardType.Spell);
            CardTextStyle spell = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Assert.That(minion.Role, Is.EqualTo(spell.Role),
                "The two titles ask for different fonts by role, which no card set does.");

            // Both bent, and bent by the same code. Which of the two treatments
            // each one asks for is itself data — a banner and a printed path are
            // both things a title can be, and choosing between them per style is
            // the point rather than a violation. What would break the design is a
            // second implementation, and there is no room for one: the source
            // check below finds nothing in the renderer that knows a card type.
            Assert.That(minion.IsWarped && spell.IsWarped, Is.True,
                "One of the two titles is not laid out on a baseline at all.");

            // And they are genuinely two styles rather than one used twice.
            bool differs =
                minion.RenderMode != spell.RenderMode ||
                !Mathf.Approximately(minion.Stretch, spell.Stretch) ||
                minion.CurveControlA != spell.CurveControlA ||
                minion.CurveControlB != spell.CurveControlB;

            Assert.That(differs, Is.True,
                "The minion and spell titles carry identical shapes, so one of them is not " +
                "describing its own card.");
        }

        /// <summary>
        /// Nothing about which card this is reaches its style.
        ///
        /// Two minions with different names, costs and rarities are set
        /// identically. A style that varied with any of those would be a per-card
        /// special case wearing a data costume.
        /// </summary>
        [Test]
        public void No_cards_identity_reaches_its_title_style()
        {
            Compose(CardType.Minion, name: "Test Soldier", cost: 2, rarity: Rarity.Common);
            CardTextStyle plain = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Compose(
                CardType.Minion,
                name: "A Completely Different Minion",
                cost: 9,
                rarity: Rarity.Legendary);

            CardTextStyle other = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Assert.That(other.Name, Is.EqualTo(plain.Name));
            Assert.That(other.RenderMode, Is.EqualTo(plain.RenderMode));
            Assert.That(other.Stretch, Is.EqualTo(plain.Stretch).Within(0.0001f));
            Assert.That(other.CurveControlB, Is.EqualTo(plain.CurveControlB));
        }

        /// <summary>
        /// And there is nowhere to put one. The files that decide how text is
        /// styled and warped never mention a card by name.
        /// </summary>
        [Test]
        public void Nothing_that_styles_text_knows_a_card_by_name()
        {
            string[] deciding =
            {
                "Assets/_Project/Presentation/CardVisuals/CardTextStyle.cs",
                "Assets/_Project/Presentation/CardVisuals/CardTextWarp.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualPainter.cs",
                "Assets/_Project/Presentation/CardVisuals/CardVisualComposer.cs"
            };

            foreach (string path in deciding)
            {
                Assert.That(File.Exists(path), Is.True, path + " is missing.");

                string source = File.ReadAllText(path);

                Assert.That(source.Contains("CardId"), Is.False,
                    Path.GetFileName(path) + " mentions a card id, so how a card is written " +
                    "can depend on which card it is.");

                Assert.That(source.Contains("CardType."), Is.False,
                    Path.GetFileName(path) + " branches on a card type, so a minion and a spell " +
                    "are no longer the same renderer with different data.");
            }
        }

        // ------------------------------------------------------------------
        //  Recomposition
        // ------------------------------------------------------------------

        [Test]
        public void Turning_a_minion_into_a_spell_retitles_it()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Test Soldier");
                painted.Painter.Apply(_plan);

                CardTextStyle before = TextLayer(CardVisualTextSlot.Name).TextStyle;
                int labels = painted.Painter.TextLayerCount;

                Compose(CardType.Spell, name: "Test Soldier");
                painted.Painter.Apply(_plan);

                CardTextStyle after = TextLayer(CardVisualTextSlot.Name).TextStyle;

                Assert.That(after.Name, Is.Not.EqualTo(before.Name),
                    "Recomposing a minion as a spell left it titled as a minion.");

                // A spell has no attack or health, so it prints fewer labels —
                // but it prints them with the objects the minion was using.
                Assert.That(painted.Painter.TextLayerCount, Is.LessThan(labels),
                    "A spell drew as many labels as a minion, so its statistics are still there.");
            }
        }

        // ------------------------------------------------------------------
        //  The warp
        // ------------------------------------------------------------------

        /// <summary>
        /// Bending a title moves its glyphs and not its slot.
        ///
        /// This is the property the layout tool depends on. If the warp moved
        /// the rectangle, every nudge would fight the curve and the numbers in
        /// the recipe would stop meaning where the title is.
        /// </summary>
        [Test]
        public void Warping_a_title_leaves_its_slot_exactly_where_the_recipe_put_it()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Test Soldier");
                CardVisualPlannedLayer title = TextLayer(CardVisualTextSlot.Name);

                Assert.That(title.TextStyle.IsWarped, Is.True,
                    "The minion title is not warped, so this proves nothing. Run " +
                    "Conquest of Hearthstone -> Author Card Text Styles.");

                painted.Painter.Apply(_plan);

                TextMeshPro label = painted.Label(title.Text);

                Assert.That(label, Is.Not.Null, "The title was not drawn.");

                Vector3 expected = CardCanvas.ToLocalPosition(title.Rect, title.SortingOrder);
                Vector2 size = CardCanvas.ToLocalSize(title.Rect);

                Assert.That(Vector3.Distance(label.transform.localPosition, expected),
                    Is.LessThan(0.0001f),
                    "The warp moved the title away from where the recipe put it.");

                Assert.That(label.rectTransform.sizeDelta.y, Is.EqualTo(size.y).Within(0.0001f),
                    "The title's height is not the one the recipe gave it, and its height is " +
                    "what decides how big it is set.");

                // Its width deliberately is not. A title that may be squeezed is
                // laid out in a box wider than it will occupy, so that its
                // height rather than its length decides the size, and the
                // squeeze afterwards brings it back. What must hold is that the
                // drawn glyphs end up inside the width the recipe gave.
                Assert.That(label.rectTransform.sizeDelta.x,
                    Is.EqualTo(size.x / title.TextStyle.MinCondense).Within(0.0001f),
                    "A squeezable title was not laid out in the wider box the squeeze assumes.");

                Extent drawn = Measure(label);
                float width = drawn.Right - drawn.Left;

                // Brought back close to the slot, not exactly to it. The squeeze
                // is measured before the bend, and bending widens what it
                // touches: a tall glyph leaning to follow the baseline reaches
                // further across than it did standing up. Staying on the card is
                // the claim that matters, and A_long_title_still_fits_across_the_card
                // makes it.
                Assert.That(width, Is.LessThanOrEqualTo(size.x * 1.12f),
                    "The title was not squeezed back to anywhere near the width the recipe " +
                    "gave it: " + width.ToString("0.000") + " against " + size.x.ToString("0.000") + ".");

                Assert.That(width, Is.LessThan(label.rectTransform.sizeDelta.x),
                    "The title still fills the wider box it was laid out in, so it was never " +
                    "squeezed at all.");
            }
        }

        [Test]
        public void A_warped_title_actually_bends()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Test Soldier");
                CardVisualPlannedLayer title = TextLayer(CardVisualTextSlot.Name);

                painted.Painter.Apply(_plan);

                TextMeshPro label = painted.Label(title.Text);
                Assert.That(label, Is.Not.Null);

                label.ForceMeshUpdate();
                float straight = BaselineSpread(label);

                Assert.That(
                    CardTextWarp.Apply(
                        label, title.TextStyle, CardCanvas.ToLocalSize(title.Rect).x, false),
                    Is.True,
                    "The warp reported it had nothing to do.");

                float bent = BaselineSpread(label);

                Assert.That(bent, Is.GreaterThan(straight + 0.001f),
                    "The characters all still sit at the same height, so the title is not curved.");
            }
        }

        /// <summary>How far the drawn glyphs actually reach, left and right.</summary>
        private readonly struct Extent
        {
            public Extent(float left, float right)
            {
                Left = left;
                Right = right;
            }

            public float Left { get; }

            public float Right { get; }

            public override string ToString() =>
                Left.ToString("0.0000") + ".." + Right.ToString("0.0000");
        }

        /// <summary>
        /// The horizontal reach of the label's mesh.
        ///
        /// Off the vertices, because that is the only thing that tells the truth
        /// once they have been moved.
        /// </summary>
        private static Extent Measure(TMP_Text label)
        {
            TMP_TextInfo info = label.textInfo;
            float left = float.MaxValue;
            float right = float.MinValue;

            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible)
                {
                    continue;
                }

                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;

                for (int corner = 0; corner < 4; corner++)
                {
                    float x = vertices[character.vertexIndex + corner].x;

                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }

            return new Extent(left, right);
        }

        /// <summary>
        /// How far apart the characters' vertical positions are. Flat text has
        /// almost none of this; a curved baseline has a lot.
        /// </summary>
        private static float BaselineSpread(TMP_Text label)
        {
            TMP_TextInfo info = label.textInfo;
            float lowest = float.MaxValue;
            float highest = float.MinValue;

            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible)
                {
                    continue;
                }

                Vector3 corner = info.meshInfo[character.materialReferenceIndex]
                    .vertices[character.vertexIndex];

                lowest = Mathf.Min(lowest, corner.y);
                highest = Mathf.Max(highest, corner.y);
            }

            return highest - lowest;
        }

        // ------------------------------------------------------------------
        //  How much a warp is allowed to deform a letter
        // ------------------------------------------------------------------

        /// <summary>
        /// The stems of the letters stay upright.
        ///
        /// This is the single property that separates a title from a wave, and
        /// the one the first implementation broke. Laying text over a curved
        /// surface shears it: a letter's top and bottom sit at the same place
        /// across the banner, so they are lifted by the same amount, the letter
        /// slides up or down and its uprights stay upright. Turning each letter
        /// to face along the baseline instead rotates those uprights — by up to
        /// twenty six degrees on the minion banner, which is what made the word
        /// look like spaghetti.
        ///
        /// So: measured on the left edge of every glyph, which is vertical in
        /// the font and must still be vertical on the card.
        /// </summary>
        [Test]
        public void A_banner_warp_shears_the_letters_and_never_turns_them()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Test Soldier");
                CardVisualPlannedLayer title = TextLayer(CardVisualTextSlot.Name);

                Assert.That(title.TextStyle.RenderMode, Is.EqualTo(CardTextRenderMode.WarpedBanner),
                    "The minion title is not set on a banner, so this proves nothing.");

                painted.Painter.Apply(_plan);

                TextMeshPro label = painted.Label(title.Text);
                Assert.That(label, Is.Not.Null);

                float worst = WorstStemTilt(label, out char culprit);

                Assert.That(worst, Is.LessThan(3f),
                    "The letter '" + culprit + "' leans " + worst.ToString("0.0") +
                    " degrees off vertical. A banner lifts letters; it does not turn them.");
            }
        }

        /// <summary>
        /// And the warp does not make the title dramatically taller than the
        /// text it was given.
        ///
        /// The renderer's own stretch of 1.6 is a number inside a texture that
        /// is then mapped onto a surface a third as tall as it is wide; on a
        /// finished card it works out at a shade under one. Read as a card space
        /// number it stretched every title by about seventy per cent, which is
        /// most of the rest of what went wrong.
        /// </summary>
        [Test]
        public void A_warp_does_not_stretch_a_title_out_of_shape()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Test Soldier");
                CardVisualPlannedLayer title = TextLayer(CardVisualTextSlot.Name);

                painted.Painter.Apply(_plan);

                TextMeshPro label = painted.Label(title.Text);
                Assert.That(label, Is.Not.Null);

                float warped = Height(label);

                label.ForceMeshUpdate();
                float flat = Height(label);

                Assert.That(flat, Is.GreaterThan(0f));

                // Taller than flat, because the baseline arcs — but by the arc,
                // not by a stretch on top of it.
                float grown = warped / flat;

                Assert.That(grown, Is.LessThan(2f),
                    "The warp made the title " + grown.ToString("0.00") +
                    " times taller than the text it was handed.");

                Assert.That(title.TextStyle.Stretch, Is.LessThan(1.2f),
                    "A banner title is stretched by " + title.TextStyle.Stretch +
                    " in card space, where the renderer's own surface works out just under one.");
            }
        }

        /// <summary>
        /// The arc itself is bounded. Measured off the renderer's mesh it is
        /// 7.8% of the banner's width on a minion and 7.1% on a spell, so
        /// anything approaching a fifth is a caricature rather than a curve.
        /// </summary>
        [Test]
        public void The_baseline_arc_stays_close_to_the_one_the_renderer_uses()
        {
            CardType[] types = { CardType.Minion, CardType.Spell };

            foreach (CardType type in types)
            {
                Compose(type, name: "Test Soldier");
                CardTextStyle style = TextLayer(CardVisualTextSlot.Name).TextStyle;

                float lowest = 0f;
                float highest = 0f;

                for (int step = 0; step <= 40; step++)
                {
                    style.SampleBaseline(step / 40f, out Vector2 point, out _);

                    lowest = Mathf.Min(lowest, point.y);
                    highest = Mathf.Max(highest, point.y);
                }

                float amplitude = highest - lowest;

                Assert.That(amplitude, Is.GreaterThan(0.03f),
                    type + " has no arc worth the name: " + amplitude.ToString("0.000") + ".");

                Assert.That(amplitude, Is.LessThan(0.13f),
                    type + " arcs by " + amplitude.ToString("0.000") +
                    " of its width, where the renderer's own banner arcs by about 0.078.");
            }
        }

        /// <summary>
        /// A minion and a spell are shaped differently, and both are sane.
        /// </summary>
        [Test]
        public void Both_banners_are_gentle_and_they_are_not_the_same()
        {
            Compose(CardType.Minion);
            CardTextStyle minion = TextLayer(CardVisualTextSlot.Name).TextStyle;

            Compose(CardType.Spell);
            CardTextStyle spell = TextLayer(CardVisualTextSlot.Name).TextStyle;

            foreach (CardTextStyle style in new[] { minion, spell })
            {
                Assert.That(style.Stretch, Is.InRange(0.7f, 1.2f),
                    style.Name + " is stretched by " + style.Stretch + ".");

                Assert.That(style.Taper, Is.LessThan(0.4f),
                    style.Name + " foreshortens by " + style.Taper + ".");
            }

            // Different in some way, without prescribing which. The two banners
            // are measured from two different meshes and are meant to differ,
            // but which numbers carry that difference is an authoring choice:
            // demanding it of the foreshortening specifically would fail a
            // perfectly good pair that expressed it through the curve instead.
            bool differs =
                minion.RenderMode != spell.RenderMode ||
                !Mathf.Approximately(minion.Taper, spell.Taper) ||
                !Mathf.Approximately(minion.Stretch, spell.Stretch) ||
                minion.CurveControlB != spell.CurveControlB;

            Assert.That(differs, Is.True,
                "The two banners are shaped identically, so one of them is not describing " +
                "its own mesh.");
        }

        /// <summary>How far the most tilted glyph's upright is off vertical, in degrees.</summary>
        private static float WorstStemTilt(TMP_Text label, out char culprit)
        {
            TMP_TextInfo info = label.textInfo;

            float worst = 0f;
            culprit = ' ';

            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible)
                {
                    continue;
                }

                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int at = character.vertexIndex;

                // Bottom left to top left: vertical in the font.
                Vector3 stem = vertices[at + 1] - vertices[at];

                if (stem.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                float tilt = Mathf.Abs(90f - Mathf.Atan2(stem.y, stem.x) * Mathf.Rad2Deg);

                if (tilt > worst)
                {
                    worst = tilt;
                    culprit = character.character;
                }
            }

            return worst;
        }

        private static float Height(TMP_Text label)
        {
            TMP_TextInfo info = label.textInfo;

            float low = float.MaxValue;
            float high = float.MinValue;

            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible)
                {
                    continue;
                }

                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int at = character.vertexIndex;

                for (int corner = 0; corner < 4; corner++)
                {
                    low = Mathf.Min(low, vertices[at + corner].y);
                    high = Mathf.Max(high, vertices[at + corner].y);
                }
            }

            return high > low ? high - low : 0f;
        }

        /// <summary>
        /// A short name is not squeezed, and a long one is squeezed without
        /// being shrunk away.
        /// </summary>
        [Test]
        public void A_short_name_is_left_alone_and_a_long_one_is_only_condensed()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Test Soldier");
                painted.Painter.Apply(_plan);

                TextMeshPro shortName = painted.Label("Test Soldier");
                Assert.That(shortName, Is.Not.Null);

                float shortSize = shortName.fontSize;

                // At the size the recipe asked for, rather than driven down
                // towards the floor. Measured against the ceiling and not
                // against the floor: the two are close together now that the
                // ceiling is a deliberate size rather than a guard rail, so
                // "comfortably above the floor" no longer means anything.
                Assert.That(shortSize, Is.GreaterThanOrEqualTo(shortName.fontSizeMax * 0.95f),
                    "A two word name was not set at the size its recipe chose: " + shortSize +
                    " against a ceiling of " + shortName.fontSizeMax + ".");

                Compose(CardType.Minion, name: "Test Deathrattle Draw");
                painted.Painter.Apply(_plan);

                TextMeshPro longName = painted.Label("Test Deathrattle Draw");
                Assert.That(longName, Is.Not.Null);

                // A guard against collapse rather than a target. A name three
                // quarters longer than another cannot be set at the same size
                // in the same banner, and squeezing it the rest of the way would
                // make the letters spindly; what must not happen is the long
                // name being shrunk to a caption while the banner stays empty.
                // It currently sits at about three fifths of the short one.
                Assert.That(longName.fontSize, Is.GreaterThan(shortSize * 0.55f),
                    "A long name is set at " + longName.fontSize + " against " + shortSize +
                    " for a short one, so it is being shrunk rather than condensed.");
            }
        }

        // ------------------------------------------------------------------
        //  Staying where they belong
        // ------------------------------------------------------------------

        /// <summary>
        /// A long name still fits across the card.
        ///
        /// Across, not inside: a title on a banner arcs above and below its own
        /// rectangle on purpose, and asserting otherwise would be asserting that
        /// it is not curved. What must never happen is a name running off the
        /// side of the card, which is what auto sizing is there to prevent and
        /// what a warp could otherwise undo.
        /// </summary>
        [Test]
        public void A_long_title_still_fits_across_the_card()
        {
            using (Painted painted = new Painted())
            {
                Compose(CardType.Minion, name: "Grand Magus Antonidas Of Dalaran");
                CardVisualPlannedLayer title = TextLayer(CardVisualTextSlot.Name);

                painted.Painter.Apply(_plan);

                TextMeshPro label = painted.Label(title.Text);
                Assert.That(label, Is.Not.Null);

                float half = CardCanvas.CardWidth * 0.5f;
                float centre = label.transform.localPosition.x;

                // Measured off the mesh rather than off textBounds, which
                // TextMeshPro works out while it lays the text out and does not
                // revisit — so it reports where the glyphs would have been, not
                // where they are. The whole point here is what the warp did to
                // them.
                //
                // Twice, too: once as laid out and once after, so a failure says
                // which of the two put the name over the edge.
                label.ForceMeshUpdate();
                Extent straight = Measure(label);
                float fitted = label.fontSize;

                // The width the recipe gave it, not the wider box it was laid
                // out in — squeezing it back to the layout box would be no
                // squeeze at all.
                float slot = CardCanvas.ToLocalSize(title.Rect).x;

                CardTextWarp.Apply(label, title.TextStyle, slot, false);
                Extent warped = Measure(label);

                string measured =
                    " rect " + title.Rect.width + "px wide, fitted at " + fitted +
                    ", straight " + straight + ", warped " + warped +
                    ", centred at " + centre.ToString("0.0000") + ".";

                Assert.That(warped.Right, Is.LessThan(straight.Right),
                    "A name too long for its banner was not squeezed at all." + measured);

                Assert.That(centre + warped.Right, Is.LessThanOrEqualTo(half),
                    "A long name runs off the right of the card." + measured);

                Assert.That(centre + warped.Left, Is.GreaterThanOrEqualTo(-half),
                    "A long name runs off the left of the card." + measured);
            }
        }

        [Test]
        public void Rules_text_is_never_warped_and_stays_in_its_panel()
        {
            Compose(
                CardType.Minion,
                rules: "Battlecry: Deal 2 damage to a random enemy minion, then draw a card.");

            CardVisualPlannedLayer rules = TextLayer(CardVisualTextSlot.RulesText);

            Assert.That(rules.TextStyle.IsWarped, Is.False,
                "Rules text is warped, which no card set does and which would make it " +
                "considerably harder to read.");

            AssertInsideTheCard(rules, "rules text");
        }

        [Test]
        public void Stat_numbers_are_never_warped_and_stay_on_their_gems()
        {
            Compose(CardType.Minion);

            CardVisualTextSlot[] numbers =
            {
                CardVisualTextSlot.ManaCost,
                CardVisualTextSlot.Attack,
                CardVisualTextSlot.Health
            };

            foreach (CardVisualTextSlot slot in numbers)
            {
                CardVisualPlannedLayer number = TextLayer(slot);

                // Flat, not straight. The two used to be the same thing, and are
                // no longer: the render mode is what applies the vertical scale
                // as well as the curve, so a number set a little taller than the
                // face draws it has to go through a bending mode with a baseline
                // that does not bend. What matters is that the baseline is
                // level — a number arched like a title would be absurd — and
                // that is now asked directly rather than read off the mode.
                for (int step = 0; step <= 20; step++)
                {
                    number.TextStyle.SampleBaseline(step / 20f, out Vector2 point, out _);

                    Assert.That(Mathf.Abs(point.y), Is.LessThan(0.005f),
                        slot + " sits on a curved baseline: it strays " +
                        point.y.ToString("0.000") + " of its width from level.");
                }

                Assert.That(number.TextStyle.Role, Is.EqualTo(CardTextRole.Stat),
                    slot + " is not set in the numbers face.");

                AssertInsideTheCard(number, slot.ToString());
            }
        }

        private static void AssertInsideTheCard(in CardVisualPlannedLayer layer, string what)
        {
            Assert.That(layer.Rect.xMin, Is.GreaterThanOrEqualTo(0f), what + " starts off the card.");
            Assert.That(layer.Rect.yMin, Is.GreaterThanOrEqualTo(0f), what + " starts above the card.");

            Assert.That(layer.Rect.xMax, Is.LessThanOrEqualTo(CardCanvas.Width),
                what + " runs off the right of the card.");

            Assert.That(layer.Rect.yMax, Is.LessThanOrEqualTo(CardCanvas.Height),
                what + " runs off the bottom of the card.");
        }

        // ------------------------------------------------------------------
        //  Fonts by role
        // ------------------------------------------------------------------

        /// <summary>
        /// Every role resolves to a face, and a project with only one font
        /// assigned still draws every card.
        ///
        /// Which files those are is not this test's business — the project is
        /// deliberately without its final fonts, and a test that demanded them
        /// would be a test that failed until a licensing question was settled.
        /// What is checked is the plumbing: that each role asks separately, and
        /// that the fallback chain is real rather than a comment.
        /// </summary>
        [Test]
        public void Every_role_falls_back_to_a_font_that_is_assigned()
        {
            using (Painted painted = new Painted())
            {
                CardTextRole[] roles =
                {
                    CardTextRole.Title,
                    CardTextRole.Rules,
                    CardTextRole.Stat,
                    CardTextRole.Tribe
                };

                foreach (CardTextRole role in roles)
                {
                    Assert.That(painted.Painter.HasFontFor(role), Is.False,
                        "A painter with no fonts assigned claims to have one for " + role + ".");
                }

                painted.AssignGeneralFont(AnyFont());

                foreach (CardTextRole role in roles)
                {
                    Assert.That(painted.Painter.HasFontFor(role), Is.True,
                        role + " does not fall back to the general font, so a project part way " +
                        "through acquiring its faces would lose that writing entirely.");
                }
            }
        }

        [Test]
        public void Each_kind_of_writing_asks_for_its_own_role()
        {
            Compose(CardType.Minion, rules: "Taunt.");

            Assert.That(TextLayer(CardVisualTextSlot.Name).TextStyle.Role,
                Is.EqualTo(CardTextRole.Title));

            Assert.That(TextLayer(CardVisualTextSlot.RulesText).TextStyle.Role,
                Is.EqualTo(CardTextRole.Rules));

            Assert.That(TextLayer(CardVisualTextSlot.Attack).TextStyle.Role,
                Is.EqualTo(CardTextRole.Stat));
        }

        /// <summary>
        /// And the tribe plate too, asked of the recipe rather than of a card.
        ///
        /// No card can carry a tribe yet — the enum has only None in it — so
        /// composing one would prove nothing. The wiring is still worth
        /// checking now rather than the day tribes arrive, which is exactly
        /// when nobody will be looking at this.
        /// </summary>
        [Test]
        public void The_tribe_plate_asks_for_the_tribe_face()
        {
            CardVisualRecipeAsset recipe = Recipe();

            CardVisualLayerDefinition plate = null;

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                if (recipe.Layers[index] != null &&
                    recipe.Layers[index].text == CardVisualTextSlot.Tribe)
                {
                    plate = recipe.Layers[index];
                    break;
                }
            }

            Assert.That(plate, Is.Not.Null, "The recipe has no tribe label.");

            Assert.That(recipe.ResolveTextStyle(plate).Role, Is.EqualTo(CardTextRole.Tribe));
        }

        // ------------------------------------------------------------------
        //  The recipe itself
        // ------------------------------------------------------------------

        private static CardVisualRecipeAsset Recipe()
        {
            CardVisualRecipeAsset recipe = AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(
                "Assets/_Project/Data/CardVisuals/CardVisualRecipe_Standard.asset");

            Assert.That(recipe, Is.Not.Null, "No standard recipe.");
            return recipe;
        }

        [Test]
        public void Every_label_names_a_style_the_recipe_defines()
        {
            CardVisualRecipeAsset recipe = Recipe();

            List<string> problems = new List<string>();
            recipe.Validate(problems);

            Assert.That(problems, Is.Empty,
                "The recipe does not hold together:\n" + string.Join("\n", problems));

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null || !layer.IsText)
                {
                    continue;
                }

                Assert.That(string.IsNullOrEmpty(layer.textStyle), Is.False,
                    "The label '" + layer.name + "' names no style, so it is drawn plainly. " +
                    "Run Conquest of Hearthstone -> Author Card Text Styles.");
            }
        }

        private static TMP_FontAsset AnyFont()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            if (font != null)
            {
                return font;
            }

            string[] found = AssetDatabase.FindAssets("t:TMP_FontAsset");

            Assert.That(found.Length, Is.GreaterThan(0), "The project has no TextMeshPro fonts.");

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(found[0]));
        }

        /// <summary>
        /// A painter on a throwaway object, cleaned up whatever the test does.
        ///
        /// Painting is the only part of this that needs a scene at all, and it
        /// needs a very small one: one object, and whatever the painter builds
        /// under it.
        /// </summary>
        private sealed class Painted : System.IDisposable
        {
            private readonly GameObject _root;

            public Painted()
            {
                _root = new GameObject("Card under test");
                Painter = _root.AddComponent<CardVisualPainter>();
            }

            public CardVisualPainter Painter { get; }

            /// <summary>The drawn label showing that text, or null.</summary>
            public TextMeshPro Label(string text)
            {
                TextMeshPro[] labels = _root.GetComponentsInChildren<TextMeshPro>(true);

                for (int index = 0; index < labels.Length; index++)
                {
                    if (labels[index].gameObject.activeSelf &&
                        string.Equals(labels[index].text, text, System.StringComparison.Ordinal))
                    {
                        return labels[index];
                    }
                }

                return null;
            }

            public void AssignGeneralFont(TMP_FontAsset font)
            {
                SerializedObject serialized = new SerializedObject(Painter);
                serialized.FindProperty("font").objectReferenceValue = font;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Dispose() => Object.DestroyImmediate(_root);
        }
    }
}
