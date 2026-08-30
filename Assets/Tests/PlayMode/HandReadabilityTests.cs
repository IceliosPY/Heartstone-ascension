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
    /// How big a card actually is on the screen.
    ///
    /// Every measurement so far has been in the card's own units, and every one
    /// of them said the hand and the preview agree — which they do. It is the
    /// wrong question for the thing that is actually wrong: a card is a card
    /// whatever its local geometry says, and a card projected at a fifth of the
    /// height the preview shows it at is a card nobody can read. Local units
    /// cannot see that. Pixels can.
    ///
    /// So this measures the projection: where the card's four corners land on
    /// the screen, and how tall its title and its rules text come out there. The
    /// numbers are normalised to a 1080 line screen so a batch run and a windowed
    /// one report the same thing.
    /// </summary>
    public sealed class HandReadabilityTests : InteractionTestBase
    {
        /// <summary>The height every measurement is reported against.</summary>
        private const float ReferenceHeight = 1080f;

        private float ToReference => ReferenceHeight / Mathf.Max(1, MatchCamera.pixelHeight);

        /// <summary>
        /// A card's footprint on screen, in pixels of a 1080 line view.
        ///
        /// From the corners of the card itself rather than from a renderer's
        /// bounds, because a card in a hand is rotated twice — once by the fan
        /// and once by the table it lies on — and a bounding box of a rotated
        /// thing is bigger than the thing.
        /// </summary>
        private Rect OnScreen(Transform card)
        {
            float halfWidth = CardCanvas.CardWidth * 0.5f;
            float halfHeight = CardCanvas.CardHeight * 0.5f;

            Vector3[] corners =
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f)
            };

            return Project(card, corners);
        }

        private Rect Project(Transform of, IReadOnlyList<Vector3> localPoints)
        {
            float left = float.MaxValue;
            float right = float.MinValue;
            float low = float.MaxValue;
            float high = float.MinValue;

            float scale = ToReference;

            for (int index = 0; index < localPoints.Count; index++)
            {
                Vector3 screen = MatchCamera.WorldToScreenPoint(of.TransformPoint(localPoints[index]));

                left = Mathf.Min(left, screen.x * scale);
                right = Mathf.Max(right, screen.x * scale);
                low = Mathf.Min(low, screen.y * scale);
                high = Mathf.Max(high, screen.y * scale);
            }

            return new Rect(left, low, right - left, high - low);
        }

        /// <summary>A label's ink, projected. Zero if it draws nothing.</summary>
        private Rect InkOnScreen(TMP_Text label)
        {
            if (label == null)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            TMP_TextInfo info = label.textInfo;
            List<Vector3> points = new List<Vector3>();

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
                    points.Add(vertices[character.vertexIndex + corner]);
                }
            }

            return points.Count == 0
                ? new Rect(0f, 0f, 0f, 0f)
                : Project(label.transform, points);
        }

        private static TextMeshPro LabelFor(CardView view, CardVisualTextSlot slot)
        {
            string wanted = view.Plan.TextIn(slot);

            if (string.IsNullOrEmpty(wanted))
            {
                return null;
            }

            TextMeshPro[] labels = view.GetComponentsInChildren<TextMeshPro>(true);

            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index].gameObject.activeInHierarchy &&
                    string.Equals(labels[index].text, wanted, System.StringComparison.Ordinal))
                {
                    return labels[index];
                }
            }

            return null;
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

        private void Line(StringBuilder report, string what, CardView view)
        {
            Rect card = OnScreen(view.transform);
            Rect title = InkOnScreen(LabelFor(view, CardVisualTextSlot.Name));
            Rect rules = InkOnScreen(LabelFor(view, CardVisualTextSlot.RulesText));

            report.AppendLine(
                what.PadRight(22) +
                "card " + card.width.ToString("0").PadLeft(4) + " x " +
                card.height.ToString("0").PadLeft(4) +
                "   title h " + title.height.ToString("0").PadLeft(3) +
                " w " + title.width.ToString("0").PadLeft(4) +
                "   rules h " + rules.height.ToString("0").PadLeft(3) +
                "   at y " + card.yMin.ToString("0").PadLeft(5) + ".." +
                card.yMax.ToString("0").PadLeft(5));
        }

        [UnityTest]
        public IEnumerator Report_how_big_a_card_in_hand_actually_is_on_screen()
        {
            yield return LoadMatch();
            yield return HandAtRest();

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            List<CardView> hand = Hand();

            Assert.That(hand.Count, Is.GreaterThan(2), "The match dealt too few cards.");

            CardView middle = hand[hand.Count / 2];
            CardView edge = hand[hand.Count - 1];

            StringBuilder report = new StringBuilder();

            report.AppendLine("=== the hand, in pixels of a " + ReferenceHeight + " line view ===");
            report.AppendLine("screen " + MatchCamera.pixelWidth + " x " + MatchCamera.pixelHeight +
                ", reported against " + ReferenceHeight + "   (" + hand.Count + " cards)");
            report.AppendLine();

            Line(report, "rest, middle", middle);
            Line(report, "rest, edge", edge);

            // Snapped rather than eased. The pose eases by a fraction of the
            // remaining distance per second, and a batch frame covers almost no
            // time at all, so waiting frames measures how far along an animation
            // happened to be rather than where the card is going.
            middle.SetHovered(true);
            middle.SnapToPose();
            yield return null;

            Line(report, "hovered, middle", middle);

            middle.SetHovered(false);
            middle.SnapToPose();
            yield return null;

            edge.SetHovered(true);
            edge.SnapToPose();
            yield return null;

            Line(report, "hovered, edge", edge);

            edge.SetHovered(false);
            edge.SnapToPose();
            yield return null;

            Line(report, "back at rest, edge", edge);

            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------------
        //  What has to hold on screen
        // ------------------------------------------------------------------

        /// <summary>
        /// Reading a card makes it substantially bigger, and keeps it on screen.
        ///
        /// The two halves are in tension and that is the point of measuring both
        /// at once: a hovered card large enough to read is large enough to fall
        /// off the bottom of the view, and every earlier attempt at one of these
        /// broke the other. The bounds are wide, because the exact size is a
        /// matter of taste; what they catch is a hover that has quietly stopped
        /// being an inspection view, or one that has grown until it is cropped.
        /// </summary>
        [UnityTest]
        public IEnumerator Reading_a_card_makes_it_bigger_without_pushing_it_off_screen()
        {
            yield return LoadMatch();
            yield return HandAtRest();
            yield return null;

            List<CardView> hand = Hand();
            Assert.That(hand.Count, Is.GreaterThan(2));

            foreach (CardView view in new[] { hand[hand.Count / 2], hand[hand.Count - 1] })
            {
                Rect resting = OnScreen(view.transform);

                view.SetHovered(true);
                view.SnapToPose();
                yield return null;

                Rect read = OnScreen(view.transform);

                float grew = read.height / resting.height;

                Assert.That(grew, Is.GreaterThan(1.3f),
                    "Reading a card only made it " + grew.ToString("0.00") +
                    " times taller, which is not an inspection view.");

                Assert.That(grew, Is.LessThan(1.9f),
                    "Reading a card made it " + grew.ToString("0.00") +
                    " times taller, which is more screen than a card needs.");

                Assert.That(read.yMin, Is.GreaterThanOrEqualTo(0f),
                    "A card being read hangs " + (-read.yMin).ToString("0") +
                    " pixels off the bottom of the view.");

                Assert.That(read.yMax, Is.LessThanOrEqualTo(ReferenceHeight),
                    "A card being read runs off the top of the view.");

                Assert.That(read.xMin, Is.GreaterThanOrEqualTo(0f),
                    "A card being read runs off the left of the view.");

                view.SetHovered(false);
                view.SnapToPose();
                yield return null;
            }
        }

        /// <summary>
        /// And putting it down puts it back exactly.
        ///
        /// Exactly, not nearly: a hand that drifts a little every time a card is
        /// read is a hand that is somewhere else entirely after a turn of play.
        /// </summary>
        [UnityTest]
        public IEnumerator A_card_read_and_put_down_returns_to_the_pose_it_left()
        {
            yield return LoadMatch();
            yield return HandAtRest();
            yield return null;

            List<CardView> hand = Hand();
            CardView view = hand[hand.Count / 2];

            Vector3 position = view.transform.localPosition;
            Quaternion rotation = view.transform.localRotation;
            Vector3 scale = view.transform.localScale;

            int[] order = new int[hand.Count];

            for (int index = 0; index < hand.Count; index++)
            {
                order[index] = hand[index].HandOrder;
            }

            view.SetHovered(true);
            view.SnapToPose();
            yield return null;

            // Reading a card is a presentation. It moves nothing else and it
            // changes nobody's place in the hand.
            for (int index = 0; index < hand.Count; index++)
            {
                Assert.That(hand[index].HandOrder, Is.EqualTo(order[index]),
                    "Reading a card renumbered the hand.");
            }

            Assert.That(view.DrawOrder, Is.GreaterThan(hand[0].DrawOrder),
                "The card being read is not drawn in front.");

            view.SetHovered(false);
            view.SnapToPose();
            yield return null;

            Assert.That(Vector3.Distance(view.transform.localPosition, position),
                Is.LessThan(0.0005f), "The card did not come back to where it was.");

            Assert.That(Quaternion.Angle(view.transform.localRotation, rotation),
                Is.LessThan(0.05f), "The card came back at a different angle.");

            Assert.That(Vector3.Distance(view.transform.localScale, scale),
                Is.LessThan(0.0005f), "The card came back a different size.");

            for (int index = 0; index < hand.Count; index++)
            {
                Assert.That(hand[index].HandOrder, Is.EqualTo(order[index]),
                    "Putting a card down renumbered the hand.");
            }
        }
    }
}
