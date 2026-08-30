using System.Collections.Generic;
using System.Text;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Says what the card composer can and cannot draw yet.
    ///
    /// It composes every combination the project can currently express — every
    /// card type, class and rarity — and reports three things: what resolved
    /// exactly, what fell back to something more general, and what was not
    /// found at all. The last of those is the list of files somebody still has
    /// to supply, generated rather than written down, so it cannot go stale.
    ///
    /// This is the answer to "which images do you need". Run it, and it tells
    /// you, slot by slot, with the path each file is expected at.
    /// </summary>
    public static class CardVisualReport
    {
        private const string MenuPath = "Conquest of Hearthstone/Report Card Visual Coverage";

        /// <summary>
        /// Where imported card component images live.
        ///
        /// Under ThirdParty because they did not come from this project, and in
        /// a Raw folder because what is imported should be the untouched file:
        /// converting or recolouring it on the way in makes it impossible to
        /// tell later what was downloaded and what was done to it.
        /// </summary>
        public const string ImportFolder = "Assets/ThirdParty/HearthCards/Raw";

        [MenuItem(MenuPath)]
        public static void Report()
        {
            CardVisualFactory factory =
                AssetDatabase.LoadAssetAtPath<CardVisualFactory>(CardVisualSetup.FactoryAssetPath);

            if (factory == null)
            {
                Debug.LogError(
                    "There is no card visual factory. Run Conquest of Hearthstone → Create Missing Card Visual Assets.");
                return;
            }

            List<string> problems = new List<string>();
            factory.Validate(problems);

            CardVisualPlan plan = new CardVisualPlan();

            // Slot, then the cards that wanted it, so the report reads as a
            // shopping list rather than as a log.
            SortedDictionary<string, List<string>> missing = new SortedDictionary<string, List<string>>();
            SortedDictionary<string, List<string>> fallbacks = new SortedDictionary<string, List<string>>();

            int composed = 0;

            foreach (CardType type in System.Enum.GetValues(typeof(CardType)))
            {
                if (type == CardType.None)
                {
                    continue;
                }

                foreach (CardClass cardClass in System.Enum.GetValues(typeof(CardClass)))
                {
                    foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
                    {
                        CardVisualDescriptor card = new CardVisualDescriptor(
                            type,
                            cardClass,
                            rarity,
                            showsStatistics: type == CardType.Minion || type == CardType.Weapon);

                        factory.Compose(card, plan);
                        composed++;

                        for (int index = 0; index < plan.Gaps.Count; index++)
                        {
                            Record(missing, plan.Gaps[index].Slot.ToString(), Describe(type, cardClass, rarity));
                        }

                        RecordFallbacks(factory, card, plan, fallbacks);
                    }
                }
            }

            Debug.Log(Compose(composed, missing, fallbacks, problems));
        }

        private static void RecordFallbacks(
            CardVisualFactory factory,
            in CardVisualDescriptor card,
            CardVisualPlan plan,
            SortedDictionary<string, List<string>> fallbacks)
        {
            if (factory.Catalog == null)
            {
                return;
            }

            for (int index = 0; index < plan.Layers.Count; index++)
            {
                CardVisualPlannedLayer layer = plan.Layers[index];

                if (layer.IsText || layer.Slot == CardVisualSlot.Artwork)
                {
                    continue;
                }

                CardVisualResolution resolution = factory.Catalog.Resolve(layer.Slot, card);

                if (resolution.Found && !resolution.IsExact)
                {
                    Record(
                        fallbacks,
                        layer.Slot + "  ←  " + resolution.Entry.match.Describe(),
                        Describe(card.Type, card.Class, card.Rarity));
                }
            }
        }

        private static void Record(SortedDictionary<string, List<string>> into, string key, string value)
        {
            if (!into.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                into[key] = list;
            }

            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        private static string Describe(CardType type, CardClass cardClass, Rarity rarity) =>
            type + " / " + cardClass + " / " + rarity;

        private static string Compose(
            int composed,
            SortedDictionary<string, List<string>> missing,
            SortedDictionary<string, List<string>> fallbacks,
            List<string> problems)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine("Card visual coverage: " + composed + " combination(s) composed.");
            text.AppendLine();

            if (problems.Count > 0)
            {
                text.AppendLine("PROBLEMS");

                for (int index = 0; index < problems.Count; index++)
                {
                    text.AppendLine("  " + problems[index]);
                }

                text.AppendLine();
            }

            if (missing.Count == 0)
            {
                text.AppendLine("Nothing required is missing.");
            }
            else
            {
                text.AppendLine("MISSING — required layers that found no picture");

                foreach (KeyValuePair<string, List<string>> pair in missing)
                {
                    text.AppendLine("  " + pair.Key + "  wanted by:");

                    for (int index = 0; index < pair.Value.Count; index++)
                    {
                        text.AppendLine("      " + pair.Value[index]);
                    }

                    text.AppendLine("      expected at: " + ImportFolder + "/<file>.png");
                }
            }

            text.AppendLine();

            if (fallbacks.Count > 0)
            {
                text.AppendLine(
                    "FELL BACK — drawn, but from a more general entry. Author a more " +
                    "specific one to override.");

                foreach (KeyValuePair<string, List<string>> pair in fallbacks)
                {
                    text.AppendLine("  " + pair.Key);
                }
            }

            return text.ToString();
        }
    }
}
