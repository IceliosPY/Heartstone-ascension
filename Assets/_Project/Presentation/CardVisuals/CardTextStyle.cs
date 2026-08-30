using System;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// What job a piece of writing on a card does.
    ///
    /// A role, not a font. Real card sets set their titles in a display face,
    /// their rules in a text face and their numbers in whichever of the two
    /// reads at a glance, and which file that is belongs to whoever assembles
    /// the project rather than to the recipe. So a style asks for "the title
    /// face" and the painter answers with whatever is currently assigned.
    ///
    /// Four roles rather than one per text slot, because attack, health and
    /// mana are the same writing in three places and would otherwise have to be
    /// kept in step by hand.
    /// </summary>
    public enum CardTextRole
    {
        Title = 0,
        Rules = 1,
        Stat = 2,
        Tribe = 3
    }

    /// <summary>
    /// How a label is laid out, once it has been typeset.
    ///
    /// These are the three treatments a Hearthstone-shaped card actually uses,
    /// and they are a property of the writing rather than of the card: a
    /// minion's name and a spell's name are both banner titles, and a minion's
    /// attack and a spell's cost are both plain numbers in a gem.
    /// </summary>
    public enum CardTextRenderMode
    {
        /// <summary>
        /// Laid out in its rectangle and left alone. Numbers, tribes and rules
        /// text: nothing about them is curved.
        /// </summary>
        Straight = 0,

        /// <summary>
        /// Set along a curved baseline, upright characters rotating to follow
        /// it. The flat, printed treatment.
        /// </summary>
        CurvedPath = 1,

        /// <summary>
        /// The same curved baseline, plus the vertical stretch and the
        /// narrowing toward the ends that make a title look like a name plate
        /// seen slightly from below.
        /// </summary>
        WarpedBanner = 2
    }

    /// <summary>
    /// How one kind of writing on a card looks: its face, its colour, its
    /// outline, and the shape of the line it sits on.
    ///
    /// Authored per recipe and named, so that a minion title and a spell title
    /// are two rows of data rather than two code paths. That is the whole point
    /// of the type: every difference the two have — a shallower arc, a little
    /// more stretch, a slightly smaller face — is a number here, and the
    /// renderer that draws them cannot tell them apart.
    ///
    /// The baseline is a cubic curve in the label's own rectangle: x runs from
    /// zero at the left edge to one at the right, and y is measured in widths
    /// of that rectangle, downward. Expressing it that way rather than in canvas
    /// pixels is what lets the rectangle be moved and resized — which is exactly
    /// what the layout tool does — without the curve coming loose from it.
    /// </summary>
    [Serializable]
    public sealed class CardTextStyleDefinition
    {
        [Tooltip("What a layer names to select this style. Unique within the recipe.")]
        [CardVisualProperty(CardVisualAuthorability.Identity,
            Note = "Layers select this style by this name; changing it detaches them.")]
        public string name = "Style";

        [Tooltip("Which font this style asks the painter for.")]
        [CardVisualProperty(CardVisualAuthorability.ProfileOnly,
            Note = "Which font family a card is set in is a project-wide invariant, not a " +
                   "per-card choice: every title resolves through the Title role and every " +
                   "rules block through Rules, whatever the card type.")]
        public CardTextRole role = CardTextRole.Rules;

        [Tooltip("How the laid out text is shaped afterwards.")]
        public CardTextRenderMode renderMode = CardTextRenderMode.Straight;

        [Header("Colour")]
        [Tooltip(
            "Not read by anything. A label's colour comes from its layer's tint, which is set " +
            "per layer rather than per style and is what the painter actually applies.")]
        [CardVisualProperty(CardVisualAuthorability.Unsupported,
            Note = "Reaches no renderer. A label's colour is its layer's tint; this is a " +
                   "second, dead source of truth for the same thing and is kept visible only " +
                   "so its presence in the asset has an explanation.")]
        public Color fillColor = Color.white;

        public Color outlineColor = new Color(0.004f, 0.004f, 0.004f, 1f);

        [Tooltip(
            "Thickness of the outline, as a fraction of the face. Zero draws none. " +
            "A title carries a heavy one; rules text carries none at all.")]
        [Range(0f, 1f)]
        public float outlineWidth;

        [Header("Spacing")]
        [Tooltip("Extra space between characters, as a percentage of the font size.")]
        public float tracking;

        [Tooltip("Space between lines, as a fraction of the normal one. Only wrapped text uses it.")]
        public float lineSpacing;

        [Header("Shape")]
        [Tooltip(
            "How far the text may be squeezed horizontally when it still does not fit its " +
            "rectangle at its smallest size. One forbids it, and a very long name is then " +
            "allowed to run past the edge instead.")]
        [Range(0.25f, 1f)]
        public float minCondense = 1f;

        [Tooltip("Vertical scale. Above one makes a title taller than the face draws it.")]
        [Range(0.5f, 3f)]
        public float stretch = 1f;

        [Tooltip(
            "How much the outer characters shrink, as a fraction. Stands in for the " +
            "perspective of a name plate seen from slightly below.")]
        [Range(0f, 0.9f)]
        public float taper;

        [Tooltip("Second control point of the baseline. x across the rectangle, y in widths of it.")]
        public Vector2 curveControlA = new Vector2(0.333f, 0f);

        [Tooltip("Third control point of the baseline.")]
        public Vector2 curveControlB = new Vector2(0.667f, 0f);

        [Tooltip("Where the baseline ends, relative to where it starts.")]
        public Vector2 curveEnd = new Vector2(1f, 0f);

        /// <summary>Whether this style bends the text at all.</summary>
        public bool IsWarped => renderMode != CardTextRenderMode.Straight;

        /// <summary>Whether an overlong line may be squeezed to fit.</summary>
        public bool CanCondense => minCondense < 1f;

        /// <summary>
        /// Whether the shape is the identity — a flat baseline, no stretch and
        /// no taper — in which case warping it would be work for nothing.
        /// </summary>
        public bool IsFlat =>
            Mathf.Approximately(stretch, 1f) &&
            Mathf.Approximately(taper, 0f) &&
            Mathf.Approximately(curveControlA.y, 0f) &&
            Mathf.Approximately(curveControlB.y, 0f) &&
            Mathf.Approximately(curveEnd.y, 0f);

        /// <summary>
        /// Where the baseline is at a given fraction across the rectangle, and
        /// which way it is heading.
        ///
        /// A cubic through (0,0) and <see cref="curveEnd"/>. The tangent is
        /// returned alongside the point because a character riding a curve has
        /// to lean with it: without that the letters stay upright over a sloping
        /// line and the word reads as a staircase.
        /// </summary>
        public void SampleBaseline(float t, out Vector2 point, out Vector2 tangent)
        {
            t = Mathf.Clamp01(t);

            Vector2 p0 = Vector2.zero;
            Vector2 p1 = curveControlA;
            Vector2 p2 = curveControlB;
            Vector2 p3 = curveEnd;

            float u = 1f - t;

            point =
                u * u * u * p0 +
                3f * u * u * t * p1 +
                3f * u * t * t * p2 +
                t * t * t * p3;

            Vector2 derivative =
                3f * u * u * (p1 - p0) +
                6f * u * t * (p2 - p1) +
                3f * t * t * (p3 - p2);

            tangent = derivative.sqrMagnitude > 1e-8f ? derivative.normalized : Vector2.right;
        }

        /// <summary>Checks the style on its own. Problems are appended, never thrown.</summary>
        public void Validate(string where, System.Collections.Generic.List<string> problems)
        {
            if (problems == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                problems.Add(where + ": a text style has no name, so no layer can select it.");
            }

            if (stretch <= 0f)
            {
                problems.Add(where + ", style '" + name + "': a stretch of " + stretch +
                    " would collapse the text to nothing.");
            }

            if (renderMode == CardTextRenderMode.Straight && !IsFlat)
            {
                problems.Add(where + ", style '" + name + "': carries a curve, a stretch or a " +
                    "taper but is set to draw straight, so none of them do anything.");
            }

            if (minCondense <= 0f)
            {
                problems.Add(where + ", style '" + name + "': may be squeezed to nothing.");
            }

            if (curveEnd.x <= 0f)
            {
                problems.Add(where + ", style '" + name + "': the baseline ends at or before " +
                    "where it starts, so the text has nowhere to go.");
            }
        }
    }

    /// <summary>
    /// A style once a layer has been matched to one: the same values, resolved
    /// and copied into the plan.
    ///
    /// A copy rather than a reference so that a plan stays a description of a
    /// card rather than a set of pointers into an asset that may be edited
    /// while it is being drawn — which is precisely what the layout tool does.
    /// </summary>
    public struct CardTextStyle
    {
        public string Name;
        public CardTextRole Role;
        public CardTextRenderMode RenderMode;
        public Color OutlineColor;
        public float OutlineWidth;
        public float Tracking;
        public float LineSpacing;
        public float MinCondense;
        public float Stretch;
        public float Taper;
        public Vector2 CurveControlA;
        public Vector2 CurveControlB;
        public Vector2 CurveEnd;

        public bool IsWarped => RenderMode != CardTextRenderMode.Straight;

        /// <summary>Whether an overlong line may be squeezed to fit.</summary>
        public bool CanCondense => MinCondense < 1f;

        /// <summary>
        /// The style a text slot gets when its layer names none.
        ///
        /// Deliberately explicit rather than blank: an unstyled label still has
        /// to pick a font, and picking one from the slot's own meaning is both
        /// the obvious answer and the one that keeps an older recipe drawing
        /// correctly after this was added to it. Nothing here bends anything —
        /// a card whose recipe says nothing about curves gets no curves.
        /// </summary>
        public static CardTextStyle For(CardVisualTextSlot slot)
        {
            return new CardTextStyle
            {
                Name = string.Empty,
                Role = RoleOf(slot),
                RenderMode = CardTextRenderMode.Straight,
                OutlineColor = Color.black,
                OutlineWidth = 0f,
                Tracking = 0f,
                LineSpacing = 0f,
                MinCondense = 1f,
                Stretch = 1f,
                Taper = 0f,
                CurveControlA = new Vector2(0.333f, 0f),
                CurveControlB = new Vector2(0.667f, 0f),
                CurveEnd = new Vector2(1f, 0f)
            };
        }

        /// <summary>Which font a slot asks for when nothing has been authored.</summary>
        public static CardTextRole RoleOf(CardVisualTextSlot slot)
        {
            switch (slot)
            {
                case CardVisualTextSlot.Name:
                    return CardTextRole.Title;

                case CardVisualTextSlot.ManaCost:
                case CardVisualTextSlot.Attack:
                case CardVisualTextSlot.Health:
                    return CardTextRole.Stat;

                case CardVisualTextSlot.Tribe:
                    return CardTextRole.Tribe;

                default:
                    return CardTextRole.Rules;
            }
        }

        public static CardTextStyle From(CardTextStyleDefinition definition, CardVisualTextSlot slot)
        {
            if (definition == null)
            {
                return For(slot);
            }

            return new CardTextStyle
            {
                Name = definition.name,
                Role = definition.role,
                RenderMode = definition.renderMode,
                OutlineColor = definition.outlineColor,
                OutlineWidth = definition.outlineWidth,
                Tracking = definition.tracking,
                LineSpacing = definition.lineSpacing,
                MinCondense = definition.minCondense,
                Stretch = definition.stretch,
                Taper = definition.taper,
                CurveControlA = definition.curveControlA,
                CurveControlB = definition.curveControlB,
                CurveEnd = definition.curveEnd
            };
        }

        /// <summary>Where the baseline is at a fraction across, and its heading.</summary>
        public void SampleBaseline(float t, out Vector2 point, out Vector2 tangent)
        {
            t = Mathf.Clamp01(t);

            Vector2 p1 = CurveControlA;
            Vector2 p2 = CurveControlB;
            Vector2 p3 = CurveEnd;

            float u = 1f - t;

            point =
                3f * u * u * t * p1 +
                3f * u * t * t * p2 +
                t * t * t * p3;

            Vector2 derivative =
                3f * u * u * p1 +
                6f * u * t * (p2 - p1) +
                3f * t * t * (p3 - p2);

            tangent = derivative.sqrMagnitude > 1e-8f ? derivative.normalized : Vector2.right;
        }
    }
}
