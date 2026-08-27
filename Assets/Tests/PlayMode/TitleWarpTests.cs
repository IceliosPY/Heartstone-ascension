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
    /// Whether a card's title is still curved by the time anybody looks at it.
    ///
    /// It was not, and nothing in the editor said so: the preview composed a
    /// card, bent its title and drew it, and every still looked right. In a
    /// match the card was dimmed a frame later because it could not be played,
    /// TextMeshPro rebuilt the mesh from the font, and the curve was gone. The
    /// composition was correct, the style resolved, the warp ran and reported
    /// success — and the table showed flat text.
    ///
    /// So this asks the only question that matters about a warp, and asks it of
    /// the running game rather than of a plan: after the hand has settled and
    /// the cards have been dimmed, is the mesh actually bent.
    /// </summary>
    public sealed class TitleWarpTests : InteractionTestBase
    {
        /// <summary>
        /// How unevenly the characters sit. Flat text has a little of this,
        /// because a T and an e are not the same height; a curved baseline has
        /// several times as much, and that gap is what is being measured.
        /// </summary>
        private static float Rise(TMP_Text label)
        {
            TMP_TextInfo info = label.textInfo;

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            bool any = false;

            for (int index = 0; index < info.characterCount; index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];

                if (!character.isVisible)
                {
                    continue;
                }

                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                int at = character.vertexIndex;

                float middle =
                    (vertices[at].y + vertices[at + 1].y +
                     vertices[at + 2].y + vertices[at + 3].y) * 0.25f;

                lowest = Mathf.Min(lowest, middle);
                highest = Mathf.Max(highest, middle);
                any = true;
            }

            return any ? highest - lowest : 0f;
        }

        private static TextMeshPro TitleOf(CardView view)
        {
            string title = view.Plan.TextIn(CardVisualTextSlot.Name);

            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            TextMeshPro[] labels = view.GetComponentsInChildren<TextMeshPro>(true);

            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index].gameObject.activeInHierarchy &&
                    string.Equals(labels[index].text, title, System.StringComparison.Ordinal))
                {
                    return labels[index];
                }
            }

            return null;
        }

        private static CardTextStyle StyleOf(CardView view)
        {
            CardVisualPlan plan = view.Plan;

            for (int index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                {
                    return plan.Layers[index].TextStyle;
                }
            }

            return CardTextStyle.For(CardVisualTextSlot.Name);
        }

        private List<CardView> Hand()
        {
            List<CardView> hand = new List<CardView>();

            foreach (CoH.Core.State.CardInstance card in Active.Hand)
            {
                if (Presenter.TryGetCardView(card.Id, out CardView view))
                {
                    hand.Add(view);
                }
            }

            return hand;
        }

        [UnityTest]
        public IEnumerator Every_title_in_hand_is_still_curved_once_the_cards_have_settled()
        {
            yield return LoadMatch();
            yield return HandAtRest();

            // A frame for anything that dirties the text — a card being dimmed
            // because it cannot be played is the usual one — and a frame for the
            // warp to be put back.
            yield return null;
            yield return null;

            List<CardView> hand = Hand();
            Assert.That(hand, Is.Not.Empty, "The match dealt no cards.");

            StringBuilder measured = new StringBuilder();
            int checked_ = 0;

            for (int index = 0; index < hand.Count; index++)
            {
                CardTextStyle style = StyleOf(hand[index]);

                if (!style.IsWarped)
                {
                    continue;
                }

                TextMeshPro label = TitleOf(hand[index]);

                Assert.That(label, Is.Not.Null,
                    "A card in hand draws no title at all.");

                float onTable = Rise(label);

                // What the same label looks like with no warp, so the comparison
                // is against this very name in this very font rather than against
                // a number somebody picked.
                label.ForceMeshUpdate();
                float flat = Rise(label);

                measured.AppendLine("  \"" + label.text + "\" on table " +
                    onTable.ToString("0.0000") + " vs flat " + flat.ToString("0.0000"));

                Assert.That(onTable, Is.GreaterThan(flat * 1.5f),
                    "The title \"" + label.text + "\" is flat on the table, so the warp was " +
                    "undone after it ran.\n" + measured);

                checked_++;
            }

            Assert.That(checked_, Is.GreaterThan(0),
                "No card in hand uses a warped title, so this proved nothing.");

            Debug.Log("Titles curved on the table:\n" + measured);
        }

        /// <summary>
        /// And it survives the thing that broke it: a card changing whether it
        /// can be played, which recolours every label on it.
        /// </summary>
        [UnityTest]
        public IEnumerator A_title_stays_curved_when_its_card_is_dimmed()
        {
            yield return LoadMatch();
            yield return HandAtRest();
            yield return null;
            yield return null;

            CardView card = null;
            List<CardView> hand = Hand();

            for (int index = 0; index < hand.Count; index++)
            {
                if (StyleOf(hand[index]).IsWarped)
                {
                    card = hand[index];
                    break;
                }
            }

            Assert.That(card, Is.Not.Null, "No card in hand uses a warped title.");

            TextMeshPro label = TitleOf(card);
            float before = Rise(label);

            CardVisualPainter painter = card.GetComponent<CardVisualPainter>();
            Assert.That(painter, Is.Not.Null);

            painter.SetDimmed(true);
            yield return null;
            yield return null;

            Assert.That(Rise(label), Is.EqualTo(before).Within(0.0005f),
                "Dimming a card flattened its title.");

            painter.SetDimmed(false);
            yield return null;
            yield return null;

            Assert.That(Rise(label), Is.EqualTo(before).Within(0.0005f),
                "Lighting a card back up flattened its title.");

            // And being read. Hovering moves and scales the whole card, which is
            // a transform on the object and must not reach the mesh inside it.
            card.SetHovered(true);
            yield return null;
            yield return null;

            Assert.That(Rise(label), Is.EqualTo(before).Within(0.0005f),
                "Hovering a card changed the shape of its title.");

            card.SetHovered(false);
            yield return null;
            yield return null;

            Assert.That(Rise(label), Is.EqualTo(before).Within(0.0005f),
                "A card coming back to rest changed the shape of its title.");
        }

        /// <summary>
        /// A short name is not treated like a long one.
        ///
        /// This is the other half of what went wrong. The title was sized to fit
        /// across its box, so every name came out small whatever its length, and
        /// a two word name sat in the middle of an empty banner. Height decides
        /// the size now, and the squeeze only takes over when a name genuinely
        /// will not fit.
        /// </summary>
        [UnityTest]
        public IEnumerator A_short_name_is_set_larger_than_a_long_one_and_neither_is_tiny()
        {
            yield return LoadMatch();
            yield return HandAtRest();

            List<CardView> hand = Hand();

            TextMeshPro shortest = null;
            TextMeshPro longest = null;

            for (int index = 0; index < hand.Count; index++)
            {
                if (!StyleOf(hand[index]).IsWarped)
                {
                    continue;
                }

                TextMeshPro label = TitleOf(hand[index]);

                if (label == null)
                {
                    continue;
                }

                if (shortest == null || label.text.Length < shortest.text.Length)
                {
                    shortest = label;
                }

                if (longest == null || label.text.Length > longest.text.Length)
                {
                    longest = label;
                }
            }

            Assert.That(shortest, Is.Not.Null, "No warped titles in hand.");

            // Sized by the box rather than by the floor. The floor is what the
            // recipe allows before a name is allowed to overflow, and a title
            // sitting on it is a title nobody chose the size of.
            Assert.That(shortest.fontSize, Is.GreaterThan(shortest.fontSizeMin * 1.5f),
                "The title \"" + shortest.text + "\" was shrunk to its floor of " +
                shortest.fontSizeMin + ".");

            if (longest != null && longest != shortest)
            {
                Assert.That(shortest.fontSize, Is.GreaterThanOrEqualTo(longest.fontSize),
                    "The shorter name \"" + shortest.text + "\" is set smaller than the longer " +
                    "\"" + longest.text + "\".");
            }
        }
    }
}
