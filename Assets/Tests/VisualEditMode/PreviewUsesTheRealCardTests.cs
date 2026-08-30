using System.IO;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// That the editor's pictures are pictures of the real card.
    ///
    /// Both the preview and the capture tool used to build their card by hand —
    /// a new object with a painter added to it — and a painter made that way has
    /// every serialized field at its default. No title font, no rules font. So
    /// the stills came out in whatever face TextMeshPro falls back to, while the
    /// game drew Belwe, and nothing failed, logged or looked obviously wrong.
    /// Hours of tuning went into a picture of a card that did not exist.
    ///
    /// It is a quiet bug and an easy one to reintroduce, because building the
    /// object by hand is the shorter code. These make it loud.
    /// </summary>
    public sealed class PreviewUsesTheRealCardTests
    {
        private static readonly string[] ToolsThatDrawCards =
        {
            "Assets/_Project/Editor/CardVisualPreviewWindow.cs",
            "Assets/_Project/Editor/CardVisualCapture.cs"
        };

        [Test]
        public void The_card_prefab_the_editor_draws_on_exists()
        {
            Assert.That(CardPreviewCard.Load(), Is.Not.Null,
                "No P_Card prefab at " + CardPreviewCard.PrefabPath +
                ", so every preview and capture falls back to a blank painter.");
        }

        /// <summary>
        /// And the card it makes is fully dressed: the fonts a card is set in
        /// are serialized on the prefab, so a card made any other way is set in
        /// the wrong ones.
        /// </summary>
        [Test]
        public void A_card_made_for_a_preview_has_the_projects_fonts_on_it()
        {
            GameObject stage = new GameObject("Preview card under test");

            try
            {
                CardVisualPainter painter = CardPreviewCard.Make(stage.transform, out GameObject card);

                Assert.That(painter, Is.Not.Null);
                Assert.That(card, Is.Not.Null);

                Assert.That(painter.HasFontFor(CardTextRole.Title), Is.True,
                    "A card made for a preview has no title font, so every still of it is in " +
                    "the wrong face.");

                Assert.That(painter.HasFontFor(CardTextRole.Rules), Is.True,
                    "A card made for a preview has no rules font.");

                // Numbers and tribes have no face of their own and fall back to
                // the title's, which is what the reference renderer does: its
                // templates set the cost, attack, health and tribe in Belwe,
                // the same family as the title. So they must resolve too.
                Assert.That(painter.HasFontFor(CardTextRole.Stat), Is.True,
                    "The numbers resolve to no font at all.");

                Assert.That(painter.HasFontFor(CardTextRole.Tribe), Is.True,
                    "The tribe plate resolves to no font at all.");
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// And every tool that draws a card asks for one rather than making its
        /// own. Checked at the source, because the failure this prevents is
        /// invisible in the result.
        /// </summary>
        [Test]
        public void Every_tool_that_draws_a_card_asks_for_the_real_one()
        {
            foreach (string path in ToolsThatDrawCards)
            {
                Assert.That(File.Exists(path), Is.True, path + " is missing.");

                string source = File.ReadAllText(path);

                Assert.That(source.Contains("CardPreviewCard"), Is.True,
                    Path.GetFileName(path) + " does not go through CardPreviewCard, so it is " +
                    "building its own card and will draw it in the wrong fonts.");
            }
        }
    }
}
