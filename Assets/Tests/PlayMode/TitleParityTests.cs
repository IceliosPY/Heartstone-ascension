using System.Collections;
using System.Collections.Generic;
using System.Text;
using CoH.Presentation;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CoH.Tests.PlayMode
{
    /// <summary>
    /// Whether a card drawn in a hand is the same card the preview drew.
    ///
    /// The two are meant to be one composition rendered twice, and the only
    /// thing allowed to differ between them is the transform the hand puts the
    /// card under: a card at three quarters size is the same card, smaller, and
    /// every proportion inside it has to survive that untouched. A title that
    /// fills its banner in a still and sits small in it on the table is not a
    /// scaling difference, it is two different compositions, and no amount of
    /// adjusting the hand would fix it.
    ///
    /// So this takes one card off the table and asks the question twice, at the
    /// two places the answer could diverge:
    ///
    ///   composed again from the same description — does the recipe, the style,
    ///       the card's own polish and the sizes come out the same;
    ///   painted again onto a bare object, the way the preview paints — does
    ///       TextMeshPro choose the same size and lay out the same mesh.
    ///
    /// It asserts on the card's own local units throughout. Comparing world
    /// space would compare the hand's scale, which is exactly the difference
    /// that is allowed.
    /// </summary>
    public sealed class TitleParityTests : InteractionTestBase
    {
        private readonly CardVisualPlan _reference = new CardVisualPlan();

        private static bool TryTitle(CardVisualPlan plan, out CardVisualPlannedLayer found)
        {
            for (int index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                {
                    found = plan.Layers[index];
                    return true;
                }
            }

            found = default;
            return false;
        }

        private static TextMeshPro LabelShowing(GameObject root, string text)
        {
            TextMeshPro[] labels = root.GetComponentsInChildren<TextMeshPro>(true);

            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index].gameObject.activeInHierarchy &&
                    string.Equals(labels[index].text, text, System.StringComparison.Ordinal))
                {
                    return labels[index];
                }
            }

            return null;
        }

        /// <summary>The glyphs' reach, in the label's own units.</summary>
        private static Rect Ink(TMP_Text label)
        {
            TMP_TextInfo info = label.textInfo;

            float left = float.MaxValue;
            float right = float.MinValue;
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
                    Vector3 position = vertices[at + corner];

                    left = Mathf.Min(left, position.x);
                    right = Mathf.Max(right, position.x);
                    low = Mathf.Min(low, position.y);
                    high = Mathf.Max(high, position.y);
                }
            }

            return right > left
                ? new Rect(left, low, right - left, high - low)
                : new Rect(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// The same measurements the editor's own report prints, taken off a
        /// card on the table, so the two can be read side by side.
        ///
        /// Screenshots of a window against screenshots of a game answer "they
        /// look different" and nothing more. These answer which number differs.
        /// </summary>
        [UnityTest]
        public IEnumerator Report_the_title_as_the_game_draws_it()
        {
            yield return LoadMatch();
            yield return HandAtRest();

            for (int frame = 0; frame < 6; frame++)
            {
                yield return null;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("=== the title, as the game draws it ===");

            foreach (CoH.Core.State.CardInstance card in Active.Hand)
            {
                if (!Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    continue;
                }

                if (!TryTitle(view.Plan, out CardVisualPlannedLayer title))
                {
                    continue;
                }

                TextMeshPro label = LabelShowing(view.gameObject, title.Text);

                report.AppendLine();
                report.AppendLine("--- \"" + title.Text + "\" ---");
                report.AppendLine("  slot           " + title.Rect);
                report.AppendLine("  size range     " + title.FontSize + " .. " + title.FontSizeMin);
                report.AppendLine("  style          " + title.TextStyle.Name +
                    " / " + title.TextStyle.RenderMode);
                report.AppendLine("  outline        " + title.TextStyle.OutlineWidth);
                report.AppendLine("  tracking       " + title.TextStyle.Tracking);
                report.AppendLine("  condense floor " + title.TextStyle.MinCondense);
                report.AppendLine("  stretch/taper  " + title.TextStyle.Stretch +
                    " / " + title.TextStyle.Taper);
                report.AppendLine("  curve          A " + title.TextStyle.CurveControlA +
                    " B " + title.TextStyle.CurveControlB + " end " + title.TextStyle.CurveEnd);

                if (label == null)
                {
                    report.AppendLine("  NO LABEL DRAWS THIS");
                    continue;
                }

                Material material = label.fontSharedMaterial;

                report.AppendLine("  font           " +
                    (label.font != null ? label.font.name : "(none)"));
                report.AppendLine("  material       " +
                    (material != null ? material.name : "(none)") +
                    (material != null && material.HasProperty("_OutlineWidth")
                        ? "  outline " + material.GetFloat("_OutlineWidth")
                        : ""));
                report.AppendLine("  point size     " + label.fontSize +
                    "   (auto " + label.enableAutoSizing + ")");
                report.AppendLine("  alignment      " + label.alignment);
                report.AppendLine("  box            " + label.rectTransform.sizeDelta);
                report.AppendLine("  label position " + label.transform.localPosition);
                report.AppendLine("  label scale    " + label.transform.localScale);
                report.AppendLine("  colour         " + label.color);
                report.AppendLine("  card scale     " + view.transform.localScale);

                Rect ink = Ink(label);

                report.AppendLine("  ink            " + ink +
                    "   (in canvas px: w " + (ink.width * CardCanvas.Width).ToString("0") +
                    ", h " + (ink.height * CardCanvas.Width).ToString("0") + ")");
            }

            Debug.Log(report.ToString());
        }

        [UnityTest]
        public IEnumerator A_card_in_hand_is_composed_exactly_as_the_preview_composes_it()
        {
            yield return LoadMatch();
            yield return HandAtRest();
            yield return null;
            yield return null;

            int compared = 0;
            StringBuilder report = new StringBuilder();

            foreach (CoH.Core.State.CardInstance card in Active.Hand)
            {
                if (!Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    continue;
                }

                if (!TryTitle(view.Plan, out CardVisualPlannedLayer onTable))
                {
                    continue;
                }

                // The very same description, composed a second time through the
                // very same factory — which is all the preview ever does.
                view.Visuals.Compose(view.Shown, _reference);

                Assert.That(TryTitle(_reference, out CardVisualPlannedLayer fresh), Is.True,
                    "Composing the same card again produced no title.");

                report.AppendLine("\"" + onTable.Text + "\"");
                report.AppendLine("  layer      " + onTable.LayerName + " vs " + fresh.LayerName);
                report.AppendLine("  slot       " + onTable.Rect + " vs " + fresh.Rect);
                report.AppendLine("  font size  " + onTable.FontSize + ".." + onTable.FontSizeMin +
                    " vs " + fresh.FontSize + ".." + fresh.FontSizeMin);
                report.AppendLine("  style      " + onTable.TextStyle.Name + "/" +
                    onTable.TextStyle.RenderMode + " vs " + fresh.TextStyle.Name + "/" +
                    fresh.TextStyle.RenderMode);
                report.AppendLine("  condense   " + onTable.TextStyle.MinCondense +
                    " vs " + fresh.TextStyle.MinCondense);
                report.AppendLine("  card scale " + view.transform.localScale);

                Assert.That(onTable.LayerName, Is.EqualTo(fresh.LayerName),
                    "The table and a fresh composition chose different layers.\n" + report);

                Assert.That(onTable.Rect, Is.EqualTo(fresh.Rect),
                    "The title's slot differs between the table and a fresh composition.\n" + report);

                Assert.That(onTable.FontSize, Is.EqualTo(fresh.FontSize).Within(0.0001f),
                    "The title's size ceiling differs.\n" + report);

                Assert.That(onTable.FontSizeMin, Is.EqualTo(fresh.FontSizeMin).Within(0.0001f),
                    "The title's size floor differs.\n" + report);

                Assert.That(onTable.TextStyle.Name, Is.EqualTo(fresh.TextStyle.Name),
                    "The title is set in a different style.\n" + report);

                Assert.That(onTable.TextStyle.RenderMode, Is.EqualTo(fresh.TextStyle.RenderMode),
                    "The title is laid out by a different mode.\n" + report);

                Assert.That(onTable.TextStyle.MinCondense,
                    Is.EqualTo(fresh.TextStyle.MinCondense).Within(0.0001f),
                    "The title may be squeezed by a different amount.\n" + report);

                Assert.That(onTable.TextStyle.Stretch,
                    Is.EqualTo(fresh.TextStyle.Stretch).Within(0.0001f), report.ToString());

                Assert.That(onTable.TextStyle.Tracking,
                    Is.EqualTo(fresh.TextStyle.Tracking).Within(0.0001f), report.ToString());

                Assert.That(onTable.TextStyle.CurveControlB,
                    Is.EqualTo(fresh.TextStyle.CurveControlB), report.ToString());

                compared++;
            }

            Assert.That(compared, Is.GreaterThan(0), "No card in hand drew a title.");

            Debug.Log("Composition matches for " + compared + " card(s):\n" + report);
        }

        /// <summary>
        /// And painted the same. Same plan, one painter on the table and one on
        /// a bare object of its own — which is exactly how the preview and the
        /// capture tools paint.
        ///
        /// This is where a difference in the hand could hide: the card in a hand
        /// is scaled, and if anything about laying the text out went through the
        /// world rather than through the card's own units, the title would come
        /// out at a different size relative to the card it is printed on.
        /// </summary>
        [UnityTest]
        public IEnumerator A_card_in_hand_is_painted_exactly_as_the_preview_paints_it()
        {
            yield return LoadMatch();
            yield return HandAtRest();
            yield return null;
            yield return null;

            List<CardView> hand = new List<CardView>();

            foreach (CoH.Core.State.CardInstance card in Active.Hand)
            {
                if (Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    hand.Add(view);
                }
            }

            Assert.That(hand, Is.Not.Empty, "The match dealt no cards.");

            // A second copy of a card already on the table, rather than a bare
            // object with a painter added to it. A painter made that way has
            // every serialized field at its default — no title font, no rules
            // font — so the reference would be drawn in the fallback face and
            // this would compare two fonts instead of two pipelines. Which it
            // did, and the difference it reported was real but was not the one
            // being looked for.
            GameObject bare = Object.Instantiate(hand[0].gameObject);
            bare.name = "Reference card";

            // Off the table, unscaled, and not driving itself: what is wanted
            // from it is its painter and the settings on it.
            CardView clone = bare.GetComponent<CardView>();

            if (clone != null)
            {
                clone.enabled = false;
            }

            bare.transform.SetParent(null, false);
            bare.transform.localPosition = new Vector3(500f, 500f, 500f);
            bare.transform.localRotation = Quaternion.identity;
            bare.transform.localScale = Vector3.one;

            // The layers the original had are copied along with it, and the
            // painter's own list of them is not: it is a runtime field, so the
            // clone's painter wakes up believing it has drawn nothing and builds
            // a second set beside the first. Both are live, both show a title,
            // and the search below would as happily find the stale one — which
            // is what it did, and which made a perfectly warped card look flat.
            Transform stale = bare.transform.Find("Layers");

            if (stale != null)
            {
                Object.DestroyImmediate(stale.gameObject);
            }

            try
            {
                CardVisualPainter reference = bare.GetComponent<CardVisualPainter>();

                Assert.That(reference, Is.Not.Null, "The card prefab has no painter.");

                int compared = 0;
                StringBuilder report = new StringBuilder();

                for (int index = 0; index < hand.Count; index++)
                {
                    CardView view = hand[index];

                    if (!TryTitle(view.Plan, out CardVisualPlannedLayer title))
                    {
                        continue;
                    }

                    TextMeshPro onTable = LabelShowing(view.gameObject, title.Text);

                    if (onTable == null)
                    {
                        continue;
                    }

                    // The same plan, painted onto an object nothing has scaled.
                    //
                    // Then several frames, because a freshly painted card is not
                    // finished with itself: colouring its labels marks them
                    // dirty, TextMeshPro rebuilds them on the next render, and
                    // the bend is put back the frame after that. The card on the
                    // table settled all of this long ago; this one has to be
                    // given the same chance before the two are compared.
                    reference.Apply(view.Plan);

                    for (int frame = 0; frame < 6; frame++)
                    {
                        yield return null;
                    }

                    TextMeshPro fresh = LabelShowing(bare, title.Text);

                    Assert.That(fresh, Is.Not.Null, "The reference painter drew no title.");

                    Rect tableInk = Ink(onTable);
                    Rect freshInk = Ink(fresh);

                    report.AppendLine("\"" + title.Text + "\"");
                    report.AppendLine("  card scale  " + view.transform.localScale +
                        "  vs " + bare.transform.localScale);
                    report.AppendLine("  title font  " +
                        (onTable.font != null ? onTable.font.name : "(none)") + " vs " +
                        (fresh.font != null ? fresh.font.name : "(none)"));
                    report.AppendLine("  label scale " + onTable.transform.localScale +
                        " vs " + fresh.transform.localScale);
                    report.AppendLine("  box         " + onTable.rectTransform.sizeDelta +
                        " vs " + fresh.rectTransform.sizeDelta);
                    report.AppendLine("  point size  " + onTable.fontSize +
                        " vs " + fresh.fontSize +
                        "   (range " + onTable.fontSizeMin + ".." + onTable.fontSizeMax + ")");
                    report.AppendLine("  ink         " + tableInk + " vs " + freshInk);

                    Assert.That(onTable.rectTransform.sizeDelta,
                        Is.EqualTo(fresh.rectTransform.sizeDelta),
                        "The title was laid out in a different box.\n" + report);

                    Assert.That(onTable.transform.localScale,
                        Is.EqualTo(fresh.transform.localScale),
                        "The title's own transform is scaled differently.\n" + report);

                    Assert.That(onTable.font, Is.EqualTo(fresh.font),
                        "The two are set in different faces, so nothing below compares " +
                        "pipelines.\n" + report);

                    Assert.That(onTable.fontSize, Is.EqualTo(fresh.fontSize).Within(0.005f),
                        "TextMeshPro chose a different size for the same title.\n" + report);

                    Assert.That(tableInk.width, Is.EqualTo(freshInk.width).Within(0.002f),
                        "The title came out a different width in the card's own units.\n" + report);

                    Assert.That(tableInk.height, Is.EqualTo(freshInk.height).Within(0.002f),
                        "The title came out a different height in the card's own units.\n" + report);

                    compared++;
                }

                Assert.That(compared, Is.GreaterThan(0), "No card in hand drew a title.");

                Debug.Log("Painting matches for " + compared + " card(s):\n" + report);
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }
    }
}
