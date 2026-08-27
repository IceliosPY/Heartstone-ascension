using System.Collections.Generic;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Writes the card's text styles into the recipe, and points the labels at
    /// them.
    ///
    /// Deliberately not part of Rebuild Card Visuals. Rebuild authors the whole
    /// layer list from scratch, which is the right thing when the components
    /// change and the wrong thing entirely once somebody has spent an evening
    /// nudging rectangles into place by hand. This touches the styles and the
    /// one field on each label that names one, and never a rectangle, a font
    /// size or a sorting order — so it can be run on a recipe that has been
    /// hand tuned without undoing any of it.
    ///
    /// The values below are the renderer's, read out of its public template and
    /// bundle rather than matched by eye. Where a number could be carried over
    /// exactly it was; where the two systems do not measure the same thing —
    /// a canvas stroke width against a signed distance field outline — the
    /// source ratio is written down beside the value that replaces it, so the
    /// difference is visible rather than lost.
    /// </summary>
    public static class CardTextStyleSetup
    {
        private const string RecipePath =
            "Assets/_Project/Data/CardVisuals/CardVisualRecipe_Standard.asset";

        public const string AllyTitle = "AllyTitle";
        public const string SpellTitle = "SpellTitle";
        public const string RulesBody = "RulesBody";
        public const string StatNumber = "StatNumber";
        public const string TribePlate = "TribePlate";

        /// <summary>
        /// How tall a title's box is, on the 800 by 1100 canvas.
        ///
        /// The name banner itself is about a hundred and sixty pixels tall, and
        /// a title wants most of that: the reference renderer gives its own name
        /// plate two hundred and ninety.
        ///
        /// Raised again once the vertical stretch was corrected. TextMeshPro
        /// fits the text to this box and the style then scales it by a shade
        /// under one, where it used to scale it by 1.6 — so the same box now
        /// yields a visibly smaller title, and the box has to make that back.
        /// </summary>
        private const float TitleHeight = 132f;

        /// <summary>
        /// The colour a stat or title is outlined in: rgb(1,1,1) for a title and
        /// #0a0805 for a number, both from the renderer.
        /// </summary>
        private static readonly Color TitleOutline = new Color(1f / 255f, 1f / 255f, 1f / 255f, 1f);

        private static readonly Color StatOutline = new Color(10f / 255f, 8f / 255f, 5f / 255f, 1f);

        /// <summary>The rules text colour, which the template gives as [30, 23, 16].</summary>
        private static readonly Color RulesInk =
            new Color(30f / 255f, 23f / 255f, 16f / 255f, 1f);

        [MenuItem("Conquest of Hearthstone/Author Card Text Styles")]
        public static void Author()
        {
            CardVisualRecipeAsset recipe =
                AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(RecipePath);

            if (recipe == null)
            {
                Debug.LogError("No card visual recipe at " + RecipePath + ".");
                return;
            }

            recipe.AuthorTextStyles(Styles());

            // Every label the recipe already has, pointed at the style that
            // matches what it prints. Named rather than matched by slot, because
            // a minion title and a spell title print the same slot and are the
            // whole reason styles exist.
            recipe.AssignTextStyle("NameText (other)", AllyTitle);
            recipe.AssignTextStyle("NameText (spell)", SpellTitle);
            recipe.AssignTextStyle("RulesText (other)", RulesBody);
            recipe.AssignTextStyle("RulesText (spell)", RulesBody);
            recipe.AssignTextStyle("ManaText", StatNumber);
            recipe.AssignTextStyle("AttackText", StatNumber);
            recipe.AssignTextStyle("HealthText", StatNumber);
            recipe.AssignTextStyle("TribeText", TribePlate);

            // How tall a title's box is decides how big the title is, because
            // TextMeshPro fits text to its box and the squeeze afterwards takes
            // care of the width. At sixty-eight pixels the box was a third of
            // the banner it sits on, so every name came out sized like a caption
            // whatever its length. This is the one piece of geometry the command
            // touches, it touches only the two title labels, and it keeps them
            // centred exactly where they were.
            recipe.SetTextHeight("NameText (other)", TitleHeight);
            recipe.SetTextHeight("NameText (spell)", TitleHeight);

            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();

            List<string> problems = new List<string>();
            recipe.Validate(problems);

            if (problems.Count > 0)
            {
                Debug.LogWarning("Card text styles authored, with " + problems.Count +
                    " problem(s):\n" + string.Join("\n", problems));
                return;
            }

            Debug.Log("Card text styles authored into " + recipe.name +
                ". No rectangle, font size or sorting order was touched.");
        }

        /// <summary>
        /// The five styles a card is set in.
        ///
        /// Two titles rather than one, and that is the point: they differ only
        /// in the numbers below, so nothing downstream has to know that a spell
        /// is a spell.
        /// </summary>
        public static IEnumerable<CardTextStyleDefinition> Styles()
        {
            yield return new CardTextStyleDefinition
            {
                name = AllyTitle,
                role = CardTextRole.Title,
                renderMode = CardTextRenderMode.WarpedBanner,

                fillColor = Color.white,
                outlineColor = TitleOutline,

                // The renderer strokes a title at max(8, 0.17 x size) on a
                // canvas. The ratio carries over; the unit does not, because an
                // outline here is spread through a distance field rather than
                // painted around a glyph. A little heavier than the ratio, so a
                // white title still separates from a pale banner at the size a
                // card is drawn in a hand.
                outlineWidth = 0.2f,

                // A name that will not fit is squeezed rather than shrunk, which
                // is what the reference renderer does with the same problem: it
                // draws its title to a texture of whatever width it needs and
                // then maps that onto a name plate of fixed size. This is also
                // how far the label is allowed to be laid out beyond its box
                // before the squeeze brings it back, so a lower number is a
                // larger title rather than a narrower one. Two thirds keeps a
                // long name nearly as tall as a short one without the letters
                // going spindly.
                minCondense = 0.62f,

                // Measured off the renderer's own mesh rather than taken from
                // the template.
                //
                // The template says the title is stretched by 1.6, and it is —
                // but inside a 2048 by 512 texture, which is then mapped onto a
                // surface that is 601 card pixels wide and 86 tall. That mapping
                // is 0.293 across and 0.168 down, a ratio of 0.573, and 1.6 x
                // 0.573 is 0.918. So a title on a finished card is very slightly
                // *shorter* than the face draws it, not two thirds taller.
                // Reading the 1.6 as a card space number stretched every title
                // by about 1.74 times more than the renderer does.
                stretch = 0.918f,

                // The perspective, also measured: across the minion banner the
                // same span of title covers 24% less at the ends than in the
                // middle, while the surface's height varies by under 4%. So the
                // ends are narrowed and nothing is shortened.
                taper = 0.24f,

                // The minion title baseline, from the template's own path:
                //   m 103.84,678.19 c 69.23,53.58 423.42,-109.05 587.48,0.95
                // Each offset divided by the 587.48 the curve spans, so the
                // shape survives the rectangle being moved or resized.
                curveControlA = new Vector2(0.1179f, 0.0912f),
                curveControlB = new Vector2(0.7208f, -0.1856f),
                curveEnd = new Vector2(1f, 0.0016f)
            };

            yield return new CardTextStyleDefinition
            {
                name = SpellTitle,
                role = CardTextRole.Title,
                renderMode = CardTextRenderMode.WarpedBanner,

                fillColor = Color.white,
                outlineColor = TitleOutline,
                outlineWidth = 0.2f,

                minCondense = 0.62f,

                // The spell mesh works out almost the same once its own aspect
                // is taken into account — 1.7 x 0.551 — but it is a flatter,
                // more even surface: its ends are only 4% narrower where the
                // minion's are 24%, which is most of why the two banners read
                // differently.
                stretch = 0.936f,
                taper = 0.04f,

                // And its baseline is a symmetric arch rather than the minion's
                // lopsided one:
                //   m 107.00,682.00 c 0,0 290.37,-118.96 598.64,0
                curveControlA = new Vector2(0f, 0f),
                curveControlB = new Vector2(0.4851f, -0.1987f),
                curveEnd = new Vector2(1f, 0f)
            };

            yield return new CardTextStyleDefinition
            {
                name = RulesBody,
                role = CardTextRole.Rules,
                renderMode = CardTextRenderMode.Straight,

                // Confirmed: [30, 23, 16], and no outline at all. Rules text is
                // printed on a light panel and needs none.
                fillColor = RulesInk,
                outlineColor = RulesInk,
                outlineWidth = 0f,

                stretch = 1f,
                taper = 0f,
                curveControlA = new Vector2(0.333f, 0f),
                curveControlB = new Vector2(0.667f, 0f),
                curveEnd = new Vector2(1f, 0f)
            };

            yield return new CardTextStyleDefinition
            {
                name = StatNumber,
                role = CardTextRole.Stat,
                renderMode = CardTextRenderMode.Straight,

                fillColor = Color.white,
                outlineColor = StatOutline,

                // The renderer strokes a number at 10 against a size of 173.3,
                // a ratio of 0.058, and strokes before it fills so half of that
                // is hidden under the glyph.
                outlineWidth = 0.14f,

                stretch = 1f,
                taper = 0f,
                curveControlA = new Vector2(0.333f, 0f),
                curveControlB = new Vector2(0.667f, 0f),
                curveEnd = new Vector2(1f, 0f)
            };

            yield return new CardTextStyleDefinition
            {
                name = TribePlate,
                role = CardTextRole.Tribe,
                renderMode = CardTextRenderMode.Straight,

                fillColor = Color.white,
                outlineColor = StatOutline,

                // 7 against a size of 50 on a minion, 6 against 48 on a spell.
                outlineWidth = 0.12f,

                stretch = 1f,
                taper = 0f,
                curveControlA = new Vector2(0.333f, 0f),
                curveControlB = new Vector2(0.667f, 0f),
                curveEnd = new Vector2(1f, 0f)
            };
        }
    }
}
