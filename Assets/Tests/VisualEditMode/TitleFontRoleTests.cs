using System;
using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Data;
using CoH.Editor;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// That a card's type never decides its typeface - for a title, for rules
    /// text, or for anything else a card writes on itself.
    ///
    /// A role is a promise, not a proof: a style declaring
    /// <see cref="CardTextRole.Title"/> only matters if the painter actually
    /// resolves that role to the same <see cref="TMP_FontAsset"/> everywhere
    /// it is used. Three kinds of guard here, deliberately not the same kind:
    ///
    ///   the named
    ///   proof        - Title and Rules have a stated font each; this
    ///                  composes real cards of both kinds and reads the
    ///                  font that ended up on the mesh against that name;
    ///   the audit    - reads the recipe's own data and asserts that every
    ///                  layer serving one semantic text slot - whichever
    ///                  CardType selects it - names a style of one role,
    ///                  by construction rather than by naming any style;
    ///   the cross-type
    ///   proof        - composes and paints a real card of every CardType
    ///                  the enum has, for every text slot at once, and
    ///                  asserts each slot lands on one font shared by every
    ///                  type that prints it. Stats and Tribe have no stated
    ///                  font of their own - the invariant for them is only
    ///                  that no CardType ever differs from another.
    ///
    /// None of the three names a card type. A CardType added six months from
    /// now is covered by all three the moment its layer's condition exists
    /// and its style names a role - which is the whole point of asking for
    /// the rule this way rather than as a per-type assignment.
    /// </summary>
    public sealed class TitleFontRoleTests
    {
        private const string TitleFontPath = "Assets/ThirdParty/HearthCards/UserProvided/Fonts/Belwe_en SDF.asset";

        private const string RulesFontPath =
            "Assets/ThirdParty/HearthCards/UserProvided/Fonts/FranklinGothic-dehinted SDF.asset";

        private static CardVisualFactory Factory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null, "No card visual factory.");
            return factory;
        }

        private static CardVisualRecipeAsset Recipe()
        {
            CardVisualRecipeAsset recipe = AssetDatabase.LoadAssetAtPath<CardVisualRecipeAsset>(
                "Assets/_Project/Data/CardVisuals/CardVisualRecipe_Standard.asset");

            Assert.That(recipe, Is.Not.Null, "No standard recipe.");
            return recipe;
        }

        private static CardDefinitionAsset Fixture(string path)
        {
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(path);
            Assert.That(asset, Is.Not.Null, "Fixture card missing: " + path);
            return asset;
        }

        /// <summary>
        /// Composes a real card onto the real prefab and reads back the font
        /// actually on the label for one text slot.
        ///
        /// Found by the text a composed plan layer says that slot carries,
        /// rather than by position or index, so this cannot accidentally read
        /// the wrong label if the layer order ever changes.
        /// </summary>
        private static TMP_FontAsset FontOfSlot(
            CardVisualFactory factory, CardDefinitionAsset card, CardVisualTextSlot slot, Transform stage)
        {
            CardVisualDescriptor described = CardVisualSelection.Describe(card, null);

            CardVisualPlan plan = new CardVisualPlan();
            factory.Compose(described, plan);

            string wanted = null;

            for (int index = 0; index < plan.Layers.Count; index++)
            {
                if (plan.Layers[index].TextSlot == slot)
                {
                    wanted = plan.Layers[index].Text;
                    break;
                }
            }

            Assert.That(wanted, Is.Not.Null.And.Not.Empty,
                slot + " was not composed for " + card.DisplayName + ".");

            CardVisualPainter painter = CardPreviewCard.Make(stage, out GameObject cardObject);
            painter.Apply(plan);

            foreach (TextMeshPro label in cardObject.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (label.text == wanted)
                {
                    return label.font;
                }
            }

            Assert.Fail("No painted label reads '" + wanted + "' for " + card.DisplayName + ".");
            return null;
        }

        [Test]
        public void A_minions_title_is_set_in_the_projects_title_font()
        {
            TMP_FontAsset expected = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            Assert.That(expected, Is.Not.Null, "No title font asset at " + TitleFontPath + ".");

            GameObject stage = new GameObject("Title font check (minion)");

            try
            {
                TMP_FontAsset used = FontOfSlot(
                    Factory(), Fixture("Assets/_Project/Data/Cards/Card_TestSoldier.asset"),
                    CardVisualTextSlot.Name, stage.transform);

                Assert.That(used, Is.SameAs(expected),
                    "Test Soldier's title is not set in the project's configured title font.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void A_spells_title_is_set_in_the_same_title_font_as_a_minions()
        {
            TMP_FontAsset expected = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            Assert.That(expected, Is.Not.Null, "No title font asset at " + TitleFontPath + ".");

            GameObject stage = new GameObject("Title font check (spell)");

            try
            {
                // Test Volley: an ordinary spell.
                TMP_FontAsset volley = FontOfSlot(
                    Factory(), Fixture("Assets/_Project/Data/Cards/Card_TestAoe.asset"),
                    CardVisualTextSlot.Name, stage.transform);

                Assert.That(volley, Is.SameAs(expected),
                    "Test Volley's title does not resolve through the project's title font.");

                // The Coin: the spell every hand actually draws, and the one a
                // player would notice first if this regressed.
                TMP_FontAsset coin = FontOfSlot(
                    Factory(), Fixture("Assets/_Project/Data/Cards/Card_TheCoin.asset"),
                    CardVisualTextSlot.Name, stage.transform);

                Assert.That(coin, Is.SameAs(expected),
                    "The Coin's title does not resolve through the project's title font.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void Rules_text_still_resolves_through_the_rules_font_on_both_kinds_of_card()
        {
            TMP_FontAsset expected = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RulesFontPath);
            Assert.That(expected, Is.Not.Null, "No rules font asset at " + RulesFontPath + ".");

            GameObject stage = new GameObject("Rules font check");

            try
            {
                // Test Soldier has no rules text of its own, so this checks a
                // minion that actually carries some.
                TMP_FontAsset minion = FontOfSlot(
                    Factory(), Fixture("Assets/_Project/Data/Cards/Card_TestBattlecryDamage.asset"),
                    CardVisualTextSlot.RulesText, stage.transform);

                Assert.That(minion, Is.SameAs(expected),
                    "A minion's rules text no longer resolves through the rules font.");

                TMP_FontAsset spell = FontOfSlot(
                    Factory(), Fixture("Assets/_Project/Data/Cards/Card_TestAoe.asset"),
                    CardVisualTextSlot.RulesText, stage.transform);

                Assert.That(spell, Is.SameAs(expected),
                    "A spell's rules text no longer resolves through the rules font.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        // ------------------------------------------------------------------
        //  The generic invariant: not "minion and spell agree", but "nothing
        //  that draws a title can disagree", proved without naming a style.
        // ------------------------------------------------------------------

        /// <summary>
        /// The recipe's own data, read the way the composer reads it: every
        /// layer that draws a card's name names a text style, and that style
        /// is marked <see cref="CardTextRole.Title"/>.
        ///
        /// This names no style and no card type. A third title-bearing layer
        /// added for a future kind of card is covered the moment it exists,
        /// and a style that forgets to declare the Title role fails here
        /// before anything ever gets composed.
        /// </summary>
        [Test]
        public void Every_layer_that_draws_a_title_uses_a_style_marked_as_the_title_role()
        {
            CardVisualRecipeAsset recipe = Recipe();
            int titleLayers = 0;

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null || layer.text != CardVisualTextSlot.Name)
                {
                    continue;
                }

                titleLayers++;

                CardTextStyleDefinition style = recipe.FindTextStyle(layer.textStyle);

                Assert.That(style, Is.Not.Null,
                    layer.name + " draws a card title but names no text style ('" +
                    layer.textStyle + "').");

                Assert.That(style.role, Is.EqualTo(CardTextRole.Title),
                    layer.name + " draws a card title through '" + style.name +
                    "', which is not marked CardTextRole.Title - so a card drawn through it " +
                    "would not follow the project's title font.");
            }

            Assert.That(titleLayers, Is.GreaterThan(0),
                "No layer in the recipe draws a title - there is nothing for this invariant to guard.");
        }

        /// <summary>
        /// Every <see cref="CardType"/> the engine knows, composed and
        /// painted for real. Whichever of them print a title print it in the
        /// project's title font - proved on the rendered mesh, not on the
        /// style's stated intent.
        ///
        /// Iterating the enum rather than a hand-picked list of types is the
        /// point: a card type added later needs no new line here to be
        /// covered, only a recipe layer whose condition admits it.
        /// </summary>
        [Test]
        public void Every_card_type_that_prints_a_title_prints_it_in_the_projects_title_font()
        {
            CardVisualFactory factory = Factory();

            TMP_FontAsset expected = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            Assert.That(expected, Is.Not.Null, "No title font asset at " + TitleFontPath + ".");

            GameObject stage = new GameObject("Title font audit (every card type)");
            int typesWithATitle = 0;

            try
            {
                foreach (CardType type in Enum.GetValues(typeof(CardType)))
                {
                    if (type == CardType.None)
                    {
                        // The face-down back and nothing else. It has no name to title.
                        continue;
                    }

                    CardVisualDescriptor described = new CardVisualDescriptor(
                        type,
                        CardClass.Neutral,
                        Rarity.Common,
                        Tribe.None,
                        artwork: null,
                        name: "Title Font Audit (" + type + ")",
                        rulesText: "Audit.",
                        manaCost: 1,
                        attack: 1,
                        health: 1,
                        showsCost: true,
                        showsStatistics: type == CardType.Minion || type == CardType.Weapon);

                    CardVisualPlan plan = new CardVisualPlan();
                    factory.Compose(described, plan);

                    string titleText = null;

                    for (int index = 0; index < plan.Layers.Count; index++)
                    {
                        if (plan.Layers[index].TextSlot == CardVisualTextSlot.Name)
                        {
                            titleText = plan.Layers[index].Text;
                            break;
                        }
                    }

                    if (titleText == null)
                    {
                        // Nothing composed a name for this type - not a title-bearing
                        // kind of card today, and nothing here to check.
                        continue;
                    }

                    typesWithATitle++;

                    CardVisualPainter painter = CardPreviewCard.Make(stage.transform, out GameObject cardObject);
                    painter.Apply(plan);

                    TMP_FontAsset used = null;

                    foreach (TextMeshPro label in cardObject.GetComponentsInChildren<TextMeshPro>(true))
                    {
                        if (label.text == titleText)
                        {
                            used = label.font;
                            break;
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(cardObject);

                    Assert.That(used, Is.SameAs(expected),
                        "CardType." + type + "'s title does not resolve through the project's title font.");
                }

                Assert.That(typesWithATitle, Is.GreaterThan(1),
                    "Fewer than two card types print a title - this invariant would be trivially satisfied.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        // ------------------------------------------------------------------
        //  Beyond the title: the same rule for every semantic text slot, and
        //  the general form of it - a CardType must never be the reason two
        //  labels disagree on font family.
        // ------------------------------------------------------------------

        /// <summary>
        /// Every text-drawing layer in the recipe, grouped by the semantic
        /// slot it serves (a title, rules, a cost, an attack, a health, a
        /// tribe). Two layers can serve the same slot for different
        /// <see cref="CardType"/>s - a title has one for spells and one for
        /// everything else - and that is fine. What is not fine is two such
        /// layers naming styles of two different <see cref="CardTextRole"/>s,
        /// because that is exactly the shape a "cast this slot in a different
        /// face for this card type" mistake would take.
        ///
        /// Names no slot and no style. A future slot, or a future layer
        /// splitting an existing one further by CardType, is audited by the
        /// same loop without a line added here.
        /// </summary>
        [Test]
        public void No_semantic_text_slot_is_served_by_layers_that_disagree_on_role()
        {
            CardVisualRecipeAsset recipe = Recipe();

            Dictionary<CardVisualTextSlot, HashSet<CardTextRole>> rolesBySlot =
                new Dictionary<CardVisualTextSlot, HashSet<CardTextRole>>();

            Dictionary<CardVisualTextSlot, List<string>> layersBySlot =
                new Dictionary<CardVisualTextSlot, List<string>>();

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null || layer.text == CardVisualTextSlot.None)
                {
                    continue;
                }

                CardTextStyleDefinition style = recipe.FindTextStyle(layer.textStyle);

                Assert.That(style, Is.Not.Null,
                    layer.name + " draws " + layer.text + " but names no text style ('" +
                    layer.textStyle + "').");

                if (!rolesBySlot.TryGetValue(layer.text, out HashSet<CardTextRole> roles))
                {
                    roles = new HashSet<CardTextRole>();
                    rolesBySlot[layer.text] = roles;
                    layersBySlot[layer.text] = new List<string>();
                }

                roles.Add(style.role);
                layersBySlot[layer.text].Add(layer.name + " (" + style.name + ")");
            }

            Assert.That(rolesBySlot.Count, Is.GreaterThanOrEqualTo(4),
                "Fewer than four semantic text slots are drawn at all - unexpectedly little to audit.");

            foreach (KeyValuePair<CardVisualTextSlot, HashSet<CardTextRole>> pair in rolesBySlot)
            {
                Assert.That(pair.Value.Count, Is.EqualTo(1),
                    "The " + pair.Key + " slot is drawn by layers that disagree on role (" +
                    string.Join(", ", layersBySlot[pair.Key]) + "), which means which CardType a " +
                    "card is could change its font family.");
            }
        }

        /// <summary>
        /// Every <see cref="CardType"/>, composed with every optional slot
        /// forced on, so a title, rules, a cost, an attack and a health all
        /// print for every type at once. For each slot, every type that
        /// prints it must land on the same font as the first type that did.
        ///
        /// Tribe is not among them: the <see cref="Tribe"/> enum has no
        /// member but <c>None</c> in this project yet, so nothing ever
        /// actually prints one, and there is no font to read off a mesh that
        /// is never drawn. The tribe layer's role is still audited above, on
        /// the recipe's own data, which does not need a value to exist.
        ///
        /// Title and Rules also have a name on file (<see cref="TitleFontPath"/>,
        /// <see cref="RulesFontPath"/>), checked elsewhere against that name.
        /// Stats do not - the project has not stated which font they must be,
        /// only that a card's type must never be the reason two of them
        /// differ - so this checks consistency across types rather than
        /// against a fixed asset, which is the weaker and more honest claim
        /// for that one.
        /// </summary>
        [Test]
        public void Every_card_type_agrees_with_every_other_on_the_font_for_each_text_slot()
        {
            CardVisualFactory factory = Factory();

            Dictionary<CardVisualTextSlot, TMP_FontAsset> fontOfSlot =
                new Dictionary<CardVisualTextSlot, TMP_FontAsset>();

            Dictionary<CardVisualTextSlot, CardType> firstTypeOfSlot =
                new Dictionary<CardVisualTextSlot, CardType>();

            GameObject stage = new GameObject("Cross-type font consistency");

            try
            {
                foreach (CardType type in Enum.GetValues(typeof(CardType)))
                {
                    if (type == CardType.None)
                    {
                        // The face-down back. Nothing on it is card-type text.
                        continue;
                    }

                    // Tribe.None: the enum has no other member in this project yet, so
                    // a tribe never actually prints. That slot is still covered - by
                    // the data audit above, which reads the recipe's layer regardless
                    // of whether anything is old enough to exercise it at runtime.
                    CardVisualDescriptor described = new CardVisualDescriptor(
                        type,
                        CardClass.Neutral,
                        Rarity.Common,
                        Tribe.None,
                        artwork: null,
                        name: "Font Consistency Audit (" + type + ")",
                        rulesText: "Audit.",
                        manaCost: 3,
                        attack: 2,
                        health: 2,
                        showsCost: true,
                        showsStatistics: true);

                    CardVisualPlan plan = new CardVisualPlan();
                    factory.Compose(described, plan);

                    CardVisualPainter painter = CardPreviewCard.Make(stage.transform, out GameObject cardObject);
                    painter.Apply(plan);

                    for (int index = 0; index < plan.Layers.Count; index++)
                    {
                        CardVisualPlannedLayer layer = plan.Layers[index];

                        if (!layer.IsText || layer.TextSlot == CardVisualTextSlot.None)
                        {
                            continue;
                        }

                        TMP_FontAsset used = null;

                        foreach (TextMeshPro label in cardObject.GetComponentsInChildren<TextMeshPro>(true))
                        {
                            if (label.text == layer.Text)
                            {
                                used = label.font;
                                break;
                            }
                        }

                        if (fontOfSlot.TryGetValue(layer.TextSlot, out TMP_FontAsset expected))
                        {
                            Assert.That(used, Is.SameAs(expected),
                                "CardType." + type + "'s " + layer.TextSlot + " resolves to a " +
                                "different font (" + (used != null ? used.name : "none") +
                                ") than CardType." + firstTypeOfSlot[layer.TextSlot] + "'s did (" +
                                expected.name + ").");
                        }
                        else
                        {
                            Assert.That(used, Is.Not.Null,
                                "CardType." + type + "'s " + layer.TextSlot + " resolves to no font at all.");

                            fontOfSlot[layer.TextSlot] = used;
                            firstTypeOfSlot[layer.TextSlot] = type;
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(cardObject);
                }

                Assert.That(fontOfSlot.Count, Is.GreaterThanOrEqualTo(4),
                    "Fewer than four text slots were exercised across all card types.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }
    }
}
