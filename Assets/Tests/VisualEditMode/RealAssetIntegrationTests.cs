using System;
using System.Collections.Generic;
using System.IO;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The downloaded components, composing real cards.
    ///
    /// These are the tests that would have caught the whole class of mistake
    /// this phase kept making: a card that draws *something* is not a card that
    /// draws the *right* thing. So each one names the file it expects, and a
    /// card falling back to scaffolding fails rather than looking plausible.
    ///
    /// They read the project's own catalog on purpose — unlike the composer
    /// tests, which build their own. What is under test here is the wiring
    /// between the manifest, the catalog and the recipe, and a test that built
    /// its own would be testing nothing.
    /// </summary>
    public sealed class RealAssetIntegrationTests
    {
        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private static CardVisualFactory Factory()
        {
            CardVisualFactory factory = AssetDatabase.LoadAssetAtPath<CardVisualFactory>(
                "Assets/_Project/Data/CardVisuals/CardVisualFactory.asset");

            Assert.That(factory, Is.Not.Null,
                "No card visual factory. Run Conquest of Hearthstone -> Create Missing Card Visual Assets.");

            return factory;
        }

        private static CardVisualDescriptor Card(
            CardType type,
            Rarity rarity = Rarity.Common,
            string rules = "",
            Tribe tribe = Tribe.None) =>
            new CardVisualDescriptor(
                type,
                CardClass.Neutral,
                rarity,
                tribe,
                artwork: null,
                name: "Test",
                rulesText: rules,
                manaCost: 2,
                attack: 2,
                health: 3,
                showsCost: true,
                showsStatistics: type == CardType.Minion || type == CardType.Weapon);

        private CardVisualPlan Compose(in CardVisualDescriptor card)
        {
            Factory().Compose(card, _plan);
            return _plan;
        }

        /// <summary>The file a slot resolved to, by name, or a readable failure.</summary>
        private string DrawnIn(CardVisualSlot slot)
        {
            Sprite sprite = _plan.SpriteIn(slot);

            Assert.That(sprite, Is.Not.Null,
                "Nothing was drawn in the " + slot + " slot.\n" + _plan.DescribeResolution());

            return sprite.name;
        }

        // ------------------------------------------------------------------
        //  Frames
        // ------------------------------------------------------------------

        [Test]
        public void A_neutral_minion_draws_the_real_minion_frame()
        {
            Compose(Card(CardType.Minion));

            Assert.That(DrawnIn(CardVisualSlot.Frame), Is.EqualTo("Card_Inhand_Minion_Neutral"));
            Assert.That(_plan.IsComplete, Is.True, _plan.DescribeResolution());
        }

        /// <summary>
        /// The renderer calls a spell frame an Ability. Our engine calls the
        /// card a Spell, and neither had to change: the manifest is where the
        /// two words meet.
        /// </summary>
        [Test]
        public void A_neutral_spell_draws_the_real_spell_frame()
        {
            Compose(Card(CardType.Spell));

            Assert.That(DrawnIn(CardVisualSlot.Frame), Is.EqualTo("Card_Inhand_Ability_Neutral"));
            Assert.That(_plan.IsComplete, Is.True, _plan.DescribeResolution());
        }

        [Test]
        public void A_spell_draws_no_attack_or_health_gem()
        {
            Compose(Card(CardType.Spell));

            Assert.That(_plan.Draws(CardVisualSlot.AttackGem), Is.False);
            Assert.That(_plan.Draws(CardVisualSlot.HealthGem), Is.False);
            Assert.That(_plan.TextIn(CardVisualTextSlot.Attack), Is.Null);
            Assert.That(_plan.TextIn(CardVisualTextSlot.Health), Is.Null);
        }

        // ------------------------------------------------------------------
        //  Rarity
        // ------------------------------------------------------------------

        [Test]
        public void Each_minion_rarity_draws_its_own_stone()
        {
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                Compose(Card(CardType.Minion, rarity));

                Assert.That(DrawnIn(CardVisualSlot.RarityGem),
                    Is.EqualTo("Card_Inhand_Minion_Gem_" + rarity));
            }
        }

        [Test]
        public void Each_spell_rarity_draws_its_own_stone()
        {
            foreach (Rarity rarity in new[] { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legendary })
            {
                Compose(Card(CardType.Spell, rarity));

                Assert.That(DrawnIn(CardVisualSlot.RarityGem),
                    Is.EqualTo("Card_Inhand_Spell_Gem_" + rarity));
            }
        }

        [Test]
        public void A_basic_card_wears_no_stone_at_all()
        {
            Compose(Card(CardType.Minion, Rarity.Free));
            Assert.That(_plan.Draws(CardVisualSlot.RarityGem), Is.False);

            Compose(Card(CardType.Spell, Rarity.Free));
            Assert.That(_plan.Draws(CardVisualSlot.RarityGem), Is.False);
        }

        // ------------------------------------------------------------------
        //  Legendary
        // ------------------------------------------------------------------

        [Test]
        public void A_legendary_minion_gets_the_minion_dragon_over_the_minion_frame()
        {
            Compose(Card(CardType.Minion, Rarity.Legendary));

            Assert.That(DrawnIn(CardVisualSlot.Frame), Is.EqualTo("Card_Inhand_Minion_Neutral"),
                "A legendary minion is still a minion.");
            Assert.That(DrawnIn(CardVisualSlot.EliteFrame), Is.EqualTo("Card_Inhand_Minion_LegendaryDragon"));
            Assert.That(DrawnIn(CardVisualSlot.RarityGem), Is.EqualTo("Card_Inhand_Minion_Gem_Legendary"));
        }

        [Test]
        public void A_legendary_spell_gets_the_spell_dragon()
        {
            Compose(Card(CardType.Spell, Rarity.Legendary));

            Assert.That(DrawnIn(CardVisualSlot.Frame), Is.EqualTo("Card_Inhand_Ability_Neutral"));
            Assert.That(DrawnIn(CardVisualSlot.EliteFrame), Is.EqualTo("Card_Inhand_Ability_LegendaryDragon"));
        }

        [Test]
        public void Nothing_below_legendary_gets_a_dragon()
        {
            foreach (Rarity rarity in new[] { Rarity.Free, Rarity.Common, Rarity.Rare, Rarity.Epic })
            {
                Compose(Card(CardType.Minion, rarity));

                Assert.That(_plan.Draws(CardVisualSlot.EliteFrame), Is.False,
                    rarity + " was given the legendary treatment.");
            }
        }

        // ------------------------------------------------------------------
        //  The components that differ by type
        // ------------------------------------------------------------------

        [Test]
        public void Minions_and_spells_draw_different_name_banners()
        {
            Compose(Card(CardType.Minion));
            string minion = DrawnIn(CardVisualSlot.NameBanner);

            Compose(Card(CardType.Spell));
            string spell = DrawnIn(CardVisualSlot.NameBanner);

            Assert.That(minion, Is.EqualTo("Card_Inhand_BannerAtlas_Minion_Title"));
            Assert.That(spell, Is.EqualTo("Card_Inhand_BannerAtlas_Spell_Title"));
        }

        [Test]
        public void Minions_and_spells_draw_different_rules_panels()
        {
            Compose(Card(CardType.Minion, rules: "Deathrattle: Draw a card."));
            string minion = DrawnIn(CardVisualSlot.RulesPanel);

            Compose(Card(CardType.Spell, rules: "Deal 1 damage."));
            string spell = DrawnIn(CardVisualSlot.RulesPanel);

            Assert.That(minion, Is.EqualTo("Card_Inhand_BannerAtlas_Minion_Text"));
            Assert.That(spell, Is.EqualTo("Card_Inhand_BannerAtlas_Spell_Text"));
        }

        [Test]
        public void Minions_and_spells_draw_different_drop_shadows()
        {
            Compose(Card(CardType.Minion));
            string minion = DrawnIn(CardVisualSlot.Backdrop);

            Compose(Card(CardType.Spell));
            string spell = DrawnIn(CardVisualSlot.Backdrop);

            Assert.That(minion, Is.EqualTo("Card_Inhand_Minion_DropShadow"));
            Assert.That(spell, Is.EqualTo("Card_Inhand_Spell_DropShadow"));
        }

        /// <summary>
        /// The point that made the recipe grow a layer per type: a minion frame
        /// and a spell frame are not the same shape and do not sit in the same
        /// place. One rectangle could never have been right for both, and the
        /// choice belongs in the data rather than in the painter.
        /// </summary>
        [Test]
        public void The_rectangles_differ_wherever_the_renderer_says_they_do()
        {
            Compose(Card(CardType.Minion, Rarity.Legendary, rules: "Text."));

            Rect minionFrame = RectOf(CardVisualSlot.Frame);
            Rect minionRarity = RectOf(CardVisualSlot.RarityGem);
            Rect minionElite = RectOf(CardVisualSlot.EliteFrame);
            Rect minionShadow = RectOf(CardVisualSlot.Backdrop);

            Compose(Card(CardType.Spell, Rarity.Legendary, rules: "Text."));

            Assert.That(RectOf(CardVisualSlot.Frame), Is.Not.EqualTo(minionFrame));
            Assert.That(RectOf(CardVisualSlot.RarityGem), Is.Not.EqualTo(minionRarity));
            Assert.That(RectOf(CardVisualSlot.EliteFrame), Is.Not.EqualTo(minionElite));
            Assert.That(RectOf(CardVisualSlot.Backdrop), Is.Not.EqualTo(minionShadow));

            // And the frames are the renderer's own numbers, not approximations.
            Assert.That(minionFrame, Is.EqualTo(new Rect(66f, 92f, 669f, 1007f)));
            Assert.That(RectOf(CardVisualSlot.Frame), Is.EqualTo(new Rect(66f, 150f, 669f, 947f)));
        }

        /// <summary>
        /// Every component is drawn at exactly its own pixel size. The renderer
        /// places them one to one on the canvas, so a rectangle that disagreed
        /// with its image would be stretching artwork nobody asked to stretch.
        /// </summary>
        [Test]
        public void No_component_is_stretched()
        {
            List<string> wrong = new List<string>();

            foreach (CardType type in new[] { CardType.Minion, CardType.Spell })
            {
                Compose(Card(type, Rarity.Legendary, rules: "Text.", tribe: Tribe.None));

                for (int index = 0; index < _plan.Layers.Count; index++)
                {
                    CardVisualPlannedLayer layer = _plan.Layers[index];

                    // Artwork is ours and is scaled into its window on purpose.
                    if (layer.IsText || layer.Sprite == null || layer.Slot == CardVisualSlot.Artwork)
                    {
                        continue;
                    }

                    // Only the imported components: scaffolding is a coloured
                    // rectangle at whatever size was convenient.
                    if (!layer.Sprite.name.StartsWith("Card_Inhand") &&
                        !layer.Sprite.name.EndsWith("texture"))
                    {
                        continue;
                    }

                    float width = layer.Sprite.rect.width;
                    float height = layer.Sprite.rect.height;

                    if (!Mathf.Approximately(width, layer.Rect.width) ||
                        !Mathf.Approximately(height, layer.Rect.height))
                    {
                        wrong.Add(
                            layer.Sprite.name + " is " + width + "x" + height +
                            " but is drawn into " + layer.Rect.width + "x" + layer.Rect.height);
                    }
                }
            }

            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        private Rect RectOf(CardVisualSlot slot)
        {
            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (!_plan.Layers[index].IsText && _plan.Layers[index].Slot == slot)
                {
                    return _plan.Layers[index].Rect;
                }
            }

            Assert.Fail("Nothing was drawn in the " + slot + " slot.\n" + _plan.DescribeResolution());
            return default;
        }

        // ------------------------------------------------------------------
        //  Falling back
        // ------------------------------------------------------------------

        /// <summary>
        /// A card type nobody downloaded a banner for still gets one. The rule
        /// has not changed now that real components exist: a real one wins, and
        /// scaffolding is what is left when there is no real one.
        /// </summary>
        [Test]
        public void A_type_with_no_component_of_its_own_still_draws_something()
        {
            Compose(Card(CardType.Weapon));

            Assert.That(DrawnIn(CardVisualSlot.Frame), Is.EqualTo("Card_Inhand_Weapon_Neutral"),
                "The weapon frame was downloaded and should be used.");

            // Nobody drew a weapon name banner, so the scaffolding one is still
            // standing in. That is the fallback working, not a fault.
            Assert.That(_plan.Draws(CardVisualSlot.NameBanner), Is.True);
            Assert.That(_plan.IsComplete, Is.True, _plan.DescribeResolution());
        }

        // ------------------------------------------------------------------
        //  The two commands compose
        // ------------------------------------------------------------------

        /// <summary>
        /// Rebuilding starts the catalog again from scaffolding. If it did not
        /// then lay the downloaded components back over the top, every rebuild
        /// would silently undo an import — and the first anybody would know is a
        /// hand full of grey rectangles.
        ///
        /// Checked by reading the code rather than by running it. The obvious
        /// test — call Rebuild, then compose — regenerates sixteen images, two
        /// assets and an AssetDatabase refresh in the middle of a test run, and
        /// every other test in this assembly reads those same assets. It passed
        /// or failed depending on what order NUnit happened to pick, which is
        /// the one thing a test must never do.
        ///
        /// So the guarantee is enforced where it actually lives: the last thing
        /// Rebuild does is call Import. Delete that line and this fails.
        /// </summary>
        [Test]
        public void Rebuilding_the_visuals_ends_by_putting_the_real_components_back()
        {
            string path = "Assets/_Project/Editor/CardVisualSetup.cs";

            Assert.That(File.Exists(path), Is.True, "Missing " + path);

            string source = File.ReadAllText(path);

            // Rebuild is now a one-line entry point onto the shared body,
            // because there are two ways in: the safe maintenance command and
            // the explicitly destructive one. The guarantee belongs to the body
            // they share.
            int run = source.IndexOf("private static void Run(bool replaceAuthored)", StringComparison.Ordinal);
            Assert.That(run, Is.GreaterThan(-1), "The shared setup body is gone.");

            // The end of the method, taken as the next method that follows it.
            int next = source.IndexOf("private static ", run + 1, StringComparison.Ordinal);
            string body = next > run ? source.Substring(run, next - run) : source.Substring(run);

            Assert.That(body, Does.Contain("CardVisualImport.Import()"),
                "Setup no longer reapplies the downloaded components, so it silently undoes an import.");

            // And both ways in reach it, so neither can quietly skip the step.
            Assert.That(source, Does.Contain("public static void Rebuild() => Run(replaceAuthored: false)"),
                "The safe command no longer goes through the shared body.");
        }

        /// <summary>
        /// And the catalog really is holding imported components rather than
        /// scaffolding, which is the outcome that test is about.
        /// </summary>
        [Test]
        public void The_catalog_is_holding_imported_components()
        {
            CardVisualCatalogAsset catalog = Factory().Catalog;

            Assert.That(catalog, Is.Not.Null);

            int imported = 0;

            for (int index = 0; index < catalog.Entries.Count; index++)
            {
                if (catalog.Entries[index] != null &&
                    catalog.Entries[index].notes != null &&
                    catalog.Entries[index].notes.StartsWith("Imported from", StringComparison.Ordinal))
                {
                    imported++;
                }
            }

            Assert.That(imported, Is.GreaterThan(0),
                "Every row is still scaffolding. Run Create Missing Card Visual Assets, or fetch the components.");
        }

        // ------------------------------------------------------------------
        //  Still no card ids anywhere
        // ------------------------------------------------------------------

        /// <summary>
        /// Two cards of the same kind compose identically. Which is the whole
        /// claim, restated against real assets: appearance follows from what a
        /// card is, never from which card it is.
        /// </summary>
        [Test]
        public void Two_cards_of_the_same_kind_compose_to_the_same_pictures()
        {
            Compose(Card(CardType.Minion, Rarity.Rare));

            List<string> first = new List<string>();

            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (_plan.Layers[index].Sprite != null)
                {
                    first.Add(_plan.Layers[index].Sprite.name);
                }
            }

            Compose(Card(CardType.Minion, Rarity.Rare));

            List<string> second = new List<string>();

            for (int index = 0; index < _plan.Layers.Count; index++)
            {
                if (_plan.Layers[index].Sprite != null)
                {
                    second.Add(_plan.Layers[index].Sprite.name);
                }
            }

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.Not.Empty);
        }
    }
}
