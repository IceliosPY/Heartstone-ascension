using System.Text;
using CoH.Core.Cards;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// Everything about a title, as the editor's own path produces it.
    ///
    /// The companion to the report the running game prints, and written so the
    /// two can be laid side by side. Comparing screenshots of a preview window
    /// against screenshots of a game answers "they look different" and nothing
    /// more; comparing these answers which number differs.
    ///
    /// It asserts nothing about the values. It is a measurement.
    /// </summary>
    public sealed class PreviewTitleReportTests
    {
        [Test]
        public void Report_the_title_as_the_editor_draws_it()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null);

            GameObject stage = new GameObject("Preview report stage");

            try
            {
                CardVisualPainter painter = CardPreviewCard.Make(stage.transform, out GameObject card);

                StringBuilder report = new StringBuilder();
                report.AppendLine("=== the title, as the editor draws it ===");

                foreach (string name in new[] { "Test Soldier", "Test Sharpshooter" })
                {
                    CardVisualPlan plan = new CardVisualPlan();

                    factory.Compose(
                        new CardVisualDescriptor(
                            CardType.Minion,
                            CardClass.Neutral,
                            Rarity.Common,
                            Tribe.None,
                            artwork: null,
                            name: name,
                            rulesText: "Battlecry: Deal 2 damage to an enemy character.",
                            manaCost: 3,
                            attack: 2,
                            health: 2,
                            showsCost: true,
                            showsStatistics: true),
                        plan);

                    painter.Apply(plan);

                    Describe(name, card, plan, report);
                    Check(name, card, plan);
                }

                Debug.Log(report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// The handful of those measurements that must hold, whatever the recipe
        /// currently says.
        ///
        /// Each one is a way the editor's picture has silently stopped being a
        /// picture of the game. The font is the one that caught it last time: a
        /// painter built by hand has none, TextMeshPro falls back, and the still
        /// comes out in a face nobody chose. The others are the same class of
        /// failure one step further along — the right font at the wrong size, or
        /// laid out in a box that does not follow from the slot.
        /// </summary>
        private static void Check(string name, GameObject card, CardVisualPlan plan)
        {
            CardVisualPlannedLayer title = default;

            for (int index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                {
                    title = plan.Layers[index];
                    break;
                }
            }

            TextMeshPro label = null;

            foreach (TextMeshPro candidate in card.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (candidate.gameObject.activeInHierarchy &&
                    string.Equals(candidate.text, name, System.StringComparison.Ordinal))
                {
                    label = candidate;
                    break;
                }
            }

            Assert.That(label, Is.Not.Null, "The editor drew no title for " + name + ".");

            Assert.That(label.font, Is.Not.Null, name + " is set in no font at all.");

            Assert.That(label.font.name, Does.Not.Contain("LiberationSans"),
                name + " is set in TextMeshPro's fallback face, which means the card being " +
                "drawn is not the project's card.");

            Assert.That(label.fontSize, Is.EqualTo(title.FontSize).Within(0.005f),
                name + " is not set at the size the recipe chose.");

            // The box is wider than the slot by exactly the squeeze the style
            // allows: height decides the size, and the squeeze brings the width
            // back. A box equal to the slot would mean that arrangement had been
            // undone somewhere.
            Vector2 slot = CardCanvas.ToLocalSize(title.Rect);
            float expected = title.TextStyle.CanCondense
                ? slot.x / title.TextStyle.MinCondense
                : slot.x;

            Assert.That(label.rectTransform.sizeDelta.x, Is.EqualTo(expected).Within(0.0005f),
                name + " was laid out in a box that does not follow from its slot.");

            Assert.That(label.rectTransform.sizeDelta.y, Is.EqualTo(slot.y).Within(0.0005f),
                name + "'s box is not the height of its slot.");

            Material material = label.fontSharedMaterial;

            Assert.That(material, Is.Not.Null);

            if (material.HasProperty("_OutlineWidth"))
            {
                Assert.That(material.GetFloat("_OutlineWidth"),
                    Is.EqualTo(title.TextStyle.OutlineWidth).Within(0.001f),
                    name + " is outlined differently from what its style asks for.");
            }
        }

        /// <summary>
        /// The same list of measurements the running game prints, in the same
        /// order and the same units, so the two can be read against each other.
        /// </summary>
        internal static void Describe(
            string name, GameObject card, CardVisualPlan plan, StringBuilder report)
        {
            CardVisualPlannedLayer title = default;
            bool found = false;

            for (int index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                {
                    title = plan.Layers[index];
                    found = true;
                    break;
                }
            }

            report.AppendLine();
            report.AppendLine("--- \"" + name + "\" ---");

            if (!found)
            {
                report.AppendLine("  no title layer");
                return;
            }

            TextMeshPro label = null;
            TextMeshPro[] labels = card.GetComponentsInChildren<TextMeshPro>(true);

            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index].gameObject.activeInHierarchy &&
                    string.Equals(labels[index].text, name, System.StringComparison.Ordinal))
                {
                    label = labels[index];
                    break;
                }
            }

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
                return;
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
            report.AppendLine("  card scale     " + card.transform.localScale);

            Rect ink = InkOf(label);

            report.AppendLine("  ink            " + ink +
                "   (in canvas px: w " + (ink.width * CardCanvas.Width).ToString("0") +
                ", h " + (ink.height * CardCanvas.Width).ToString("0") + ")");
        }

        internal static Rect InkOf(TMP_Text label)
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
    }
}
