using TMPro;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Bends a laid out label onto a curved baseline.
    ///
    /// It moves vertices and nothing else. The label's rectangle, its position
    /// on the card and the size the text was fitted at are all decided before
    /// this runs and are left exactly as they were, which is what makes the warp
    /// safe to apply to something a layout tool is editing: the rectangle stays
    /// the honest answer to "where is the title", and the curve is only what the
    /// glyphs do inside it.
    ///
    /// There are two ways to put text on a curve and they do not look alike.
    ///
    /// Setting it *along a path* turns each character to face the way the path
    /// is heading, the way a word follows a road on a map. Laying it over a
    /// *surface* turns nothing: the whole title is one sheet, and a letter is
    /// lifted by however much the sheet is lifted underneath it. The first is
    /// what an SVG text path does; the second is what mapping a texture onto a
    /// curved mesh does.
    ///
    /// Which of the two a style wants is a real difference and not a detail.
    /// Using the first where the second belonged produced a row of letters each
    /// leaning a different way — a wave rather than a title — because on a
    /// banner whose midline reaches twenty six degrees, turning a glyph by its
    /// local heading turns it by twenty six degrees. The surface lifts that same
    /// glyph and leaves it standing.
    ///
    /// Nothing here knows what it is drawing. It is handed a shape and a mesh.
    /// </summary>
    public static class CardTextWarp
    {
        /// <summary>
        /// Bends the label's mesh to the style, and reports whether it did.
        ///
        /// False means there was nothing to do — the style draws straight, the
        /// shape is the identity, or the label is empty — not that anything went
        /// wrong. The caller does not need to care; it is returned because the
        /// tests do.
        /// </summary>
        public static bool Apply(TMP_Text label, in CardTextStyle style) =>
            Apply(label, style, label == null ? 0f : label.rectTransform.rect.width, true);

        /// <summary>
        /// Bends the label onto the style's baseline, squeezing it into
        /// <paramref name="targetWidth"/> first.
        ///
        /// The target width is given rather than read off the rectangle because
        /// the two are deliberately not the same for a title. TextMeshPro sizes
        /// text to fit its box, so a box the width of the banner makes a long
        /// name small — and a small name in a large banner is the thing this is
        /// all trying to avoid. The label is therefore laid out in a wider box
        /// than it will occupy, which lets the height decide the size, and the
        /// squeeze below brings it back to the width the recipe actually gave
        /// it. Size by height, condense to width, in that order.
        ///
        /// <paramref name="regenerate"/> is false when the caller already knows
        /// the mesh is fresh — which is the case when this is being run in
        /// answer to TextMeshPro having just rebuilt it, where forcing another
        /// rebuild would undo the work and ask for it again forever.
        /// </summary>
        public static bool Apply(
            TMP_Text label,
            in CardTextStyle style,
            float targetWidth,
            bool regenerate)
        {
            if (label == null || (!style.IsWarped && !style.CanCondense))
            {
                return false;
            }

            // The mesh has to exist before it can be bent, and TextMeshPro only
            // builds it lazily. Forcing it here rather than waiting a frame is
            // what lets a card be composed and captured in one go, which every
            // editor tool and every test does.
            if (regenerate)
            {
                label.ForceMeshUpdate();
            }

            TMP_TextInfo info = label.textInfo;

            if (info == null || info.characterCount == 0)
            {
                return false;
            }

            // The width the title is meant to occupy, which is the recipe's and
            // not the box TextMeshPro was given to lay out in.
            float width = targetWidth > 0f ? targetWidth : label.rectTransform.rect.width;

            if (width <= 0f)
            {
                return false;
            }

            // Both boxes are centred on the same point, so the true edges follow
            // from the width alone.
            float middleX = label.rectTransform.rect.center.x;
            float left = middleX - width * 0.5f;

            float condense = Condensation(label, style, width);
            bool changed = !Mathf.Approximately(condense, 1f);

            if (!style.IsWarped && !changed)
            {
                return false;
            }

            // The whole title has one middle, and the vertical scale works
            // around it. Around each glyph's own middle instead, a tall style
            // would push the letters apart rather than making the word taller.
            float middleY = MiddleOfTheLine(info);

            bool alongPath = style.RenderMode == CardTextRenderMode.CurvedPath;

            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];

                // Spaces and line breaks carry no geometry. Asking for their
                // vertices returns whatever was last in that slot of the buffer.
                if (!character.isVisible)
                {
                    continue;
                }

                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;

                Vector3[] vertices = info.meshInfo[material].vertices;

                if (alongPath)
                {
                    SetAlongPath(vertices, vertex, style, middleX, left, width, condense);
                }
                else
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        vertices[vertex + corner] = OverSurface(
                            vertices[vertex + corner],
                            style, middleX, middleY, left, width, condense);
                    }
                }

                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            return true;
        }

        /// <summary>
        /// Where one point of the title ends up when the title is a sheet laid
        /// over the banner.
        ///
        /// Every vertex on its own, from its own position, and nothing is turned.
        /// A letter's top and bottom sit at the same place across the banner, so
        /// the surface lifts them by the same amount and the letter is moved
        /// rather than rotated. What deformation it does pick up comes from its
        /// left and right edges sitting at slightly different places along the
        /// curve — a gentle shear, which is the whole of the effect wanted.
        ///
        /// This is the default, and it is what the reference renderer does: the
        /// title is drawn flat into a texture and that texture is mapped over a
        /// curved mesh.
        /// </summary>
        private static Vector3 OverSurface(
            Vector3 position,
            in CardTextStyle style,
            float middleX,
            float middleY,
            float left,
            float width,
            float condense)
        {
            float across = (position.x - middleX) * condense;

            float u = Mathf.Clamp01((middleX + across - left) / width);

            u = Foreshortened(u, style.Taper);
            across = (u - 0.5f) * width;

            style.SampleBaseline(u, out Vector2 point, out _);

            // The style measures y downward, the way the card canvas does, and
            // the mesh measures it upward.
            float rise = -point.y * width;

            return new Vector3(
                middleX + across,
                middleY + (position.y - middleY) * style.Stretch + rise,
                position.z);
        }

        /// <summary>
        /// Sets one character along the baseline, turned to face the way it is
        /// heading.
        ///
        /// The other treatment, and a real one: this is what an SVG text path
        /// does, and it is how the reference renderer draws a title when it is
        /// asked for the flat printed look rather than the game one. Whole
        /// characters are moved and turned as tiles, which is right here and
        /// wrong for a banner.
        /// </summary>
        private static void SetAlongPath(
            Vector3[] vertices,
            int vertex,
            in CardTextStyle style,
            float middleX,
            float left,
            float width,
            float condense)
        {
            Vector3 laidOut = (vertices[vertex] + vertices[vertex + 2]) * 0.5f;

            Vector3 middle = new Vector3(
                middleX + (laidOut.x - middleX) * condense, laidOut.y, laidOut.z);

            float u = Mathf.Clamp01((middle.x - left) / width);

            style.SampleBaseline(u, out Vector2 point, out Vector2 heading);

            float rise = -point.y * width;
            float angle = Mathf.Atan2(-heading.y, heading.x);

            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);

            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 position = vertices[vertex + corner];

                float x = (position.x - laidOut.x) * condense;
                float y = (position.y - laidOut.y) * style.Stretch;

                vertices[vertex + corner] = new Vector3(
                    middle.x + x * cos - y * sin,
                    middle.y + rise + x * sin + y * cos,
                    position.z);
            }
        }

        /// <summary>
        /// Redistributes a position across the banner so the ends are tighter
        /// than the middle.
        ///
        /// Toward the ends the surface turns away from the viewer and the same
        /// span of title covers less of the banner: across the minion banner the
        /// ends are twenty four per cent tighter than the middle, measured off
        /// the renderer's own mesh. Horizontal only — the surface's height
        /// varies by under four per cent from end to end, so shrinking the
        /// letters there as well would be perspective the mesh does not have.
        ///
        /// The taper is a *density*, and the position is its integral. Scaling
        /// the position by the density instead pulls the far ends in by the full
        /// twenty four per cent rather than by the eight the mesh does, which
        /// narrows the whole title and leaves the banner half empty. So the
        /// quadratic below is integrated and renormalised: the ends still stay
        /// where the banner ends, and only the spacing in between changes.
        /// </summary>
        private static float Foreshortened(float u, float taper)
        {
            if (taper <= 0f)
            {
                return u;
            }

            // The integral of 1 - taper * (2u - 1)^2, normalised so that nought
            // maps to nought and one maps to one.
            float centred = 2f * u - 1f;
            float travelled = u - taper * (centred * centred * centred + 1f) / 6f;

            return travelled / (1f - taper / 3f);
        }

        /// <summary>The middle of the text's own vertical extent.</summary>
        private static float MiddleOfTheLine(TMP_TextInfo info)
        {
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

            return high > low ? (low + high) * 0.5f : 0f;
        }

        /// <summary>
        /// How much the line has to be squeezed to fit, between one and the
        /// style's floor.
        ///
        /// One when it already fits, which is almost always. The floor is what
        /// stops the answer to a very long name being an unreadable one: past
        /// it the text is allowed to overflow after all, and somebody has to
        /// shorten the name.
        /// </summary>
        private static float Condensation(TMP_Text label, in CardTextStyle style, float width)
        {
            if (!style.CanCondense)
            {
                return 1f;
            }

            float drawn = DrawnWidth(label);

            if (drawn <= width || drawn <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(style.MinCondense, width / drawn);
        }

        /// <summary>
        /// How wide the glyphs actually are, measured off the mesh.
        ///
        /// Not <c>textBounds</c>, which is the extent of the typeset text and
        /// stops at the glyphs themselves. A title carries a heavy outline, and
        /// an outline is drawn outside the glyph on quads that TextMeshPro pads
        /// for exactly that reason — so the ink is wider than the text, and
        /// squeezing to fit the text still left the outline over the edge.
        /// </summary>
        private static float DrawnWidth(TMP_Text label)
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
                int at = character.vertexIndex;

                for (int corner = 0; corner < 4; corner++)
                {
                    left = Mathf.Min(left, vertices[at + corner].x);
                    right = Mathf.Max(right, vertices[at + corner].x);
                }
            }

            return right > left ? right - left : 0f;
        }
    }
}
