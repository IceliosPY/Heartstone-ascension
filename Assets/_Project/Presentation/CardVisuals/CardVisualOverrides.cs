using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// A number that may or may not have been set.
    ///
    /// A flag beside the value rather than a nullable or a sentinel, because
    /// this is authored in an inspector and has to survive being serialised:
    /// zero is a perfectly good offset and one is a perfectly good multiplier,
    /// so no value can stand for "not set". The flag is what the tool draws a
    /// tick box for, and what tells the difference between a card that asks for
    /// a font a size larger and a card that asks for nothing.
    /// </summary>
    [Serializable]
    public struct OptionalNumber
    {
        [Tooltip("Whether this card overrides the recipe here at all.")]
        public bool overridden;

        public float value;

        public OptionalNumber(float value)
        {
            overridden = true;
            this.value = value;
        }

        /// <summary>This card's value, or the one it inherits.</summary>
        public float Or(float inherited) => overridden ? value : inherited;

        /// <summary>The inherited value scaled by this, or left alone.</summary>
        public float Scaling(float inherited) => overridden ? inherited * value : inherited;

        /// <summary>The inherited value shifted by this, or left alone.</summary>
        public float Shifting(float inherited) => overridden ? inherited + value : inherited;

        public static OptionalNumber None => default;
    }

    /// <summary>
    /// One card's adjustments to one piece of writing on it.
    ///
    /// The recipe decides how every card of a kind is set, and that is where
    /// almost all of the work belongs: a change there fixes every card at once,
    /// and a change here fixes exactly one. This exists for the cards that need
    /// the last five per cent — a name that sits a hair too high on its banner,
    /// a word that wants a little more room — and it is deliberately made of
    /// multipliers and offsets rather than absolute values, so that retuning the
    /// recipe still moves the cards that were polished on top of it.
    ///
    /// Every field is optional. A card with none of them set composes exactly as
    /// though this did not exist, which is the property that keeps the recipe
    /// the real source of the style.
    /// </summary>
    [Serializable]
    public sealed class CardTextOverride
    {
        [Tooltip("Which piece of writing on the card this adjusts.")]
        public CardVisualTextSlot slot = CardVisualTextSlot.Name;

        [Header("Where it sits")]
        [Tooltip("Moved right, in pixels of the 800 by 1100 card canvas.")]
        public OptionalNumber offsetX;

        [Tooltip("Moved down, in pixels of the card canvas.")]
        public OptionalNumber offsetY;

        [Tooltip("Wider or narrower, about its own middle. One is unchanged.")]
        public OptionalNumber widthMultiplier;

        [Tooltip("Taller or shorter, about its own middle. One is unchanged.")]
        public OptionalNumber heightMultiplier;

        [Header("How it is set")]
        [Tooltip("Bigger or smaller than the recipe's ceiling. One is unchanged.")]
        public OptionalNumber fontSizeMultiplier;

        [Tooltip("Space between characters, replacing the style's. Not a multiplier.")]
        public OptionalNumber tracking;

        [Tooltip(
            "How much further this card may be squeezed. Below one lets a long name " +
            "condense more and so be set larger; above one holds it wider.")]
        public OptionalNumber condenseMultiplier;

        [Tooltip("How strongly the baseline curves. Zero is flat, one is the recipe's.")]
        public OptionalNumber warpStrength;

        [Header("The shape of the baseline")]
        [Tooltip("How deep the arch is, as a fraction of the title's width. Replaces the style's.")]
        public OptionalNumber curveAmount;

        [Tooltip("How much higher one end of the baseline sits than the other.")]
        public OptionalNumber curveTilt;

        [Tooltip("Where the top of the arch sits across the title. Half is centred.")]
        public OptionalNumber curveCentre;

        /// <summary>Whether this asks for anything at all.</summary>
        public bool IsEmpty =>
            !offsetX.overridden &&
            !offsetY.overridden &&
            !widthMultiplier.overridden &&
            !heightMultiplier.overridden &&
            !fontSizeMultiplier.overridden &&
            !tracking.overridden &&
            !condenseMultiplier.overridden &&
            !warpStrength.overridden &&
            !curveAmount.overridden &&
            !curveTilt.overridden &&
            !curveCentre.overridden;

        /// <summary>Forgets every adjustment, leaving the slot it belongs to.</summary>
        public void Clear()
        {
            offsetX = OptionalNumber.None;
            offsetY = OptionalNumber.None;
            widthMultiplier = OptionalNumber.None;
            heightMultiplier = OptionalNumber.None;
            fontSizeMultiplier = OptionalNumber.None;
            tracking = OptionalNumber.None;
            condenseMultiplier = OptionalNumber.None;
            warpStrength = OptionalNumber.None;
            curveAmount = OptionalNumber.None;
            curveTilt = OptionalNumber.None;
            curveCentre = OptionalNumber.None;
        }

        /// <summary>Whether this card reshapes the baseline at all.</summary>
        public bool ReshapesTheCurve =>
            curveAmount.overridden || curveTilt.overridden || curveCentre.overridden;

        /// <summary>
        /// Where the writing goes, once this card has had its say.
        ///
        /// Width and height scale about the rectangle's own middle, so making a
        /// banner's title a little wider does not also drag it sideways. Moving
        /// it is what the offsets are for, and they are applied afterwards so
        /// the two do not interfere.
        /// </summary>
        public Rect Placed(Rect rect)
        {
            float width = widthMultiplier.Scaling(rect.width);
            float height = heightMultiplier.Scaling(rect.height);

            float middleX = rect.x + rect.width * 0.5f;
            float middleY = rect.y + rect.height * 0.5f;

            return new Rect(
                offsetX.Shifting(middleX - width * 0.5f),
                offsetY.Shifting(middleY - height * 0.5f),
                Mathf.Max(1f, width),
                Mathf.Max(1f, height));
        }

        /// <summary>How the writing is set, once this card has had its say.</summary>
        public CardTextStyle Styled(CardTextStyle style)
        {
            style.Tracking = tracking.Or(style.Tracking);

            // Below one lets the text be squeezed further, which is what makes a
            // long name larger rather than narrower. Clamped because a card that
            // asked to be squeezed to nothing would simply disappear.
            style.MinCondense = Mathf.Clamp(
                condenseMultiplier.Scaling(style.MinCondense), 0.2f, 1f);

            // The shape first, then how strongly it is applied.
            //
            // Rebuilt from whichever of the three this card sets, with the
            // style's own values standing in for the rest — so a card that
            // only wants a shallower arch keeps the lean and the off centre top
            // its recipe gave it. Anything the recipe drew that these three
            // cannot express is replaced, which is the price of touching them at
            // all and is why the tool says so before it happens.
            if (ReshapesTheCurve)
            {
                CardTextCurve inherited = CardTextCurve.From(
                    style.CurveControlA, style.CurveControlB, style.CurveEnd);

                new CardTextCurve(
                        curveAmount.Or(inherited.Amount),
                        curveTilt.Or(inherited.Tilt),
                        curveCentre.Or(inherited.Centre))
                    .ToControls(
                        out Vector2 controlA, out Vector2 controlB, out Vector2 end);

                style.CurveControlA = controlA;
                style.CurveControlB = controlB;
                style.CurveEnd = end;
            }

            if (warpStrength.overridden)
            {
                // The curve is scaled rather than replaced, so the shape stays
                // the recipe's and only its depth is this card's business. Zero
                // flattens the baseline without turning the warp off, which
                // keeps the vertical scale and the foreshortening working.
                float strength = Mathf.Max(0f, warpStrength.value);

                style.CurveControlA = Scaled(style.CurveControlA, strength);
                style.CurveControlB = Scaled(style.CurveControlB, strength);
                style.CurveEnd = Scaled(style.CurveEnd, strength);
            }

            return style;
        }

        private static Vector2 Scaled(Vector2 control, float strength) =>
            new Vector2(control.x, control.y * strength);

        /// <summary>The size ceiling, once this card has had its say.</summary>
        public float Sized(float fontSize) => fontSizeMultiplier.Scaling(fontSize);
    }

    /// <summary>
    /// Everything one card wants done differently from its recipe.
    ///
    /// Reached by identity and then handed on as data: whatever looks a card up
    /// does so once, by its id, and passes this along. Nothing downstream is
    /// told which card it is drawing, so there is still nowhere in the composer
    /// or the painter to write "if this is The Coin" — the only thing they
    /// receive is a set of numbers that may or may not be set.
    /// </summary>
    [Serializable]
    public sealed class CardVisualOverrides
    {
        [SerializeField] private List<CardTextOverride> text = new List<CardTextOverride>();

        public IReadOnlyList<CardTextOverride> Text => text;

        /// <summary>Whether this card asks for anything at all.</summary>
        public bool IsEmpty
        {
            get
            {
                for (int index = 0; index < text.Count; index++)
                {
                    if (text[index] != null && !text[index].IsEmpty)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>What this card wants done to one piece of writing, or null.</summary>
        public CardTextOverride For(CardVisualTextSlot slot)
        {
            for (int index = 0; index < text.Count; index++)
            {
                if (text[index] != null && text[index].slot == slot)
                {
                    return text[index];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>The entry for a slot, made if it was not there. Tooling only.</summary>
        internal CardTextOverride Establish(CardVisualTextSlot slot)
        {
            CardTextOverride found = For(slot);

            if (found != null)
            {
                return found;
            }

            found = new CardTextOverride { slot = slot };
            text.Add(found);

            return found;
        }

        /// <summary>Forgets everything this card asked for.</summary>
        internal void Clear() => text.Clear();

        /// <summary>Forgets what this card asked for in one slot.</summary>
        internal void Clear(CardVisualTextSlot slot)
        {
            for (int index = text.Count - 1; index >= 0; index--)
            {
                if (text[index] != null && text[index].slot == slot)
                {
                    text.RemoveAt(index);
                }
            }
        }
#endif
    }
}
