using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The promises the composer makes, tested rather than asserted in a
    /// comment.
    ///
    /// The claim of this whole design is that a card's appearance is decided by
    /// what a card is and never by which card it is. That is a claim about the
    /// shape of the code, so it is checked the way the Core's independence from
    /// Unity is checked: by looking.
    /// </summary>
    public sealed class CardVisualArchitectureTests
    {
        private const string ComposerFolder = "Assets/_Project/Presentation/CardVisuals";

        /// <summary>
        /// The files that decide what a card looks like. The artwork library is
        /// not among them: mapping an id to a painting is exactly its job, and
        /// it makes no other decision.
        /// </summary>
        private static readonly string[] DecidingFiles =
        {
            "CardVisualComposer.cs",
            "CardVisualCatalogAsset.cs",
            "CardVisualRecipeAsset.cs",
            "CardVisualCondition.cs",
            "CardVisualDescriptor.cs",
            "CardVisualPlan.cs",
            "CardVisualPainter.cs"
        };

        [Test]
        public void Nothing_that_decides_an_appearance_knows_which_card_it_is_drawing()
        {
            List<string> offenders = new List<string>();

            for (int index = 0; index < DecidingFiles.Length; index++)
            {
                string path = Path.Combine(ComposerFolder, DecidingFiles[index]);

                Assert.That(File.Exists(path), Is.True, "Missing file: " + path);

                string source = File.ReadAllText(path);

                if (source.Contains("CardId"))
                {
                    offenders.Add(DecidingFiles[index] + " mentions CardId.");
                }
            }

            Assert.That(offenders, Is.Empty,
                "A card's appearance must follow from what it is, never from which card it is:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void The_description_of_a_card_carries_no_identity()
        {
            PropertyInfo[] properties = typeof(CardVisualDescriptor)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int index = 0; index < properties.Length; index++)
            {
                Assert.That(properties[index].Name, Does.Not.Contain("Id").IgnoreCase,
                    "The composer would be able to tell one card from another.");
            }
        }

        /// <summary>
        /// The composer is a function. Nothing about it can depend on when it
        /// was called or on what was drawn last, which is what lets the editor
        /// preview and the game reach the same picture.
        /// </summary>
        [Test]
        public void Composing_is_a_pure_function_of_its_arguments()
        {
            Type composer = typeof(CardVisualComposer);

            Assert.That(composer.IsAbstract && composer.IsSealed, Is.True,
                "The composer should be static.");

            FieldInfo[] fields = composer.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(fields, Is.Empty,
                "The composer keeps state, so two identical calls could disagree.");
        }

        // ------------------------------------------------------------------
        //  The project's own assets
        // ------------------------------------------------------------------

        private static CardVisualFactory LoadFactory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null,
                "The card visual factory is missing. Run Conquest of Hearthstone -> Rebuild Card Visuals.");

            return factory;
        }

        [Test]
        public void The_authored_visuals_have_nothing_wrong_with_them()
        {
            List<string> problems = new List<string>();
            LoadFactory().Validate(problems);

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// Every card the project can currently express composes without a gap.
        ///
        /// This is the test that will fail the day somebody adds a card type
        /// nothing has a frame for, and it will name it rather than leaving a
        /// hole to be noticed in a match.
        /// </summary>
        [Test]
        public void Every_card_the_project_can_express_composes_completely()
        {
            CardVisualFactory factory = LoadFactory();
            CardVisualPlan plan = new CardVisualPlan();
            List<string> gaps = new List<string>();

            foreach (CardType type in Enum.GetValues(typeof(CardType)))
            {
                if (type == CardType.None)
                {
                    continue;
                }

                foreach (CardClass cardClass in Enum.GetValues(typeof(CardClass)))
                {
                    foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                    {
                        CardVisualDescriptor card = new CardVisualDescriptor(
                            type, cardClass, rarity,
                            showsStatistics: type == CardType.Minion || type == CardType.Weapon);

                        factory.Compose(card, plan);

                        for (int index = 0; index < plan.Gaps.Count; index++)
                        {
                            gaps.Add(plan.Gaps[index].Describe());
                        }
                    }
                }
            }

            Assert.That(gaps, Is.Empty,
                "Some cards cannot be drawn:\n" + string.Join("\n", gaps));
        }

        [Test]
        public void A_face_down_card_composes_completely_too()
        {
            CardVisualFactory factory = LoadFactory();
            CardVisualPlan plan = new CardVisualPlan();

            factory.Compose(
                new CardVisualDescriptor(CardType.None, CardClass.Neutral, faceDown: true), plan);

            Assert.That(plan.IsComplete, Is.True, plan.Describe());
            Assert.That(plan.Draws(CardVisualSlot.CardBack), Is.True);
        }

        /// <summary>
        /// A card type with no frame of its own still draws, from the default
        /// entry. The fallback is a stated default rather than a lucky pick, and
        /// this pins that down for the types no art exists for yet.
        /// </summary>
        [Test]
        public void A_type_nobody_has_drawn_a_frame_for_still_gets_one()
        {
            CardVisualFactory factory = LoadFactory();

            CardVisualResolution resolution = factory.Catalog.Resolve(
                CardVisualSlot.Frame,
                new CardVisualDescriptor(CardType.Hero, CardClass.Neutral));

            Assert.That(resolution.Found, Is.True, "A hero card would have no frame at all.");
            Assert.That(resolution.IsExact, Is.False, "It should be reported as a fallback.");
        }
    }
}
