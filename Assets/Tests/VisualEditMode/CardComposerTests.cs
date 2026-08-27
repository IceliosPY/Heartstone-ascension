using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// Composing a card from data.
    ///
    /// Every test here builds its own recipe and its own catalog, so what is
    /// being tested is the composer rather than whatever somebody last authored
    /// in the project. None of them needs a scene, which is the point: the
    /// interesting half of a card's appearance is decided before anything is
    /// drawn, and that half is worth being able to test in milliseconds.
    /// </summary>
    public sealed class CardComposerTests
    {
        private readonly CardVisualPlan _plan = new CardVisualPlan();

        private void Compose(
            in CardVisualDescriptor card,
            CardVisualRecipeAsset recipe,
            CardVisualCatalogAsset catalog) =>
            CardVisualComposer.Compose(card, recipe, catalog, _plan);

        // ------------------------------------------------------------------
        //  A card comes out the other end
        // ------------------------------------------------------------------

        [Test]
        public void A_minion_composes_its_frame_its_gems_and_its_words()
        {
            Sprite frame = VisualTestFactory.Picture("frame");
            Sprite mana = VisualTestFactory.Picture("mana");
            Sprite attack = VisualTestFactory.Picture("attack");

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20, required: true),
                VisualTestFactory.Picture("ManaGem", CardVisualSlot.ManaGem, 60),
                VisualTestFactory.Picture("AttackGem", CardVisualSlot.AttackGem, 70,
                    conditions: CardVisualCondition.True(CardVisualField.ShowsStatistics)),
                VisualTestFactory.Label("Name", CardVisualTextSlot.Name, 130),
                VisualTestFactory.Label("Mana", CardVisualTextSlot.ManaCost, 140),
                VisualTestFactory.Label("Attack", CardVisualTextSlot.Attack, 160));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, frame, type: CardType.Minion),
                VisualTestFactory.Entry(CardVisualSlot.ManaGem, mana),
                VisualTestFactory.Entry(CardVisualSlot.AttackGem, attack));

            Compose(VisualTestFactory.Card(), recipe, catalog);

            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(frame));
            Assert.That(_plan.Draws(CardVisualSlot.ManaGem), Is.True);
            Assert.That(_plan.Draws(CardVisualSlot.AttackGem), Is.True);

            Assert.That(_plan.TextIn(CardVisualTextSlot.Name), Is.EqualTo("Test Soldier"));
            Assert.That(_plan.TextIn(CardVisualTextSlot.ManaCost), Is.EqualTo("2"));
            Assert.That(_plan.TextIn(CardVisualTextSlot.Attack), Is.EqualTo("2"));

            Assert.That(_plan.IsComplete, Is.True);
        }

        /// <summary>
        /// The same recipe and the same catalog, and a spell comes out. Nothing
        /// was switched on, nothing was branched: the type is a value the layers
        /// and the entries are matched against.
        /// </summary>
        [Test]
        public void The_same_recipe_composes_a_spell_with_no_statistics()
        {
            Sprite minionFrame = VisualTestFactory.Picture("minion");
            Sprite spellFrame = VisualTestFactory.Picture("spell");

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20, required: true),
                VisualTestFactory.Picture("AttackGem", CardVisualSlot.AttackGem, 70,
                    conditions: CardVisualCondition.True(CardVisualField.ShowsStatistics)),
                VisualTestFactory.Picture("HealthGem", CardVisualSlot.HealthGem, 80,
                    conditions: CardVisualCondition.True(CardVisualField.ShowsStatistics)),
                VisualTestFactory.Label("Attack", CardVisualTextSlot.Attack, 160),
                VisualTestFactory.Label("Health", CardVisualTextSlot.Health, 170));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, minionFrame, type: CardType.Minion),
                VisualTestFactory.Entry(CardVisualSlot.Frame, spellFrame, type: CardType.Spell),
                VisualTestFactory.Entry(CardVisualSlot.AttackGem, VisualTestFactory.Picture("attack")),
                VisualTestFactory.Entry(CardVisualSlot.HealthGem, VisualTestFactory.Picture("health")));

            Compose(VisualTestFactory.Card(CardType.Minion), recipe, catalog);
            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(minionFrame));
            Assert.That(_plan.Draws(CardVisualSlot.AttackGem), Is.True);

            Compose(VisualTestFactory.Card(CardType.Spell), recipe, catalog);

            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(spellFrame),
                "A spell should have found the spell frame.");
            Assert.That(_plan.Draws(CardVisualSlot.AttackGem), Is.False,
                "A spell has no attack.");
            Assert.That(_plan.Draws(CardVisualSlot.HealthGem), Is.False);
            Assert.That(_plan.TextIn(CardVisualTextSlot.Attack), Is.Null,
                "A spell printed an attack value.");
        }

        [Test]
        public void Layers_are_ordered_back_to_front_whatever_order_they_were_written_in()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Gem", CardVisualSlot.ManaGem, 60),
                VisualTestFactory.Picture("Backdrop", CardVisualSlot.Backdrop, 0),
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.ManaGem, VisualTestFactory.Picture("gem")),
                VisualTestFactory.Entry(CardVisualSlot.Backdrop, VisualTestFactory.Picture("back")),
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("frame")));

            Compose(VisualTestFactory.Card(), recipe, catalog);

            Assert.That(_plan.Layers.Count, Is.EqualTo(3));
            Assert.That(_plan.Layers[0].Slot, Is.EqualTo(CardVisualSlot.Backdrop));
            Assert.That(_plan.Layers[1].Slot, Is.EqualTo(CardVisualSlot.Frame));
            Assert.That(_plan.Layers[2].Slot, Is.EqualTo(CardVisualSlot.ManaGem));
        }

        // ------------------------------------------------------------------
        //  Overrides
        // ------------------------------------------------------------------

        /// <summary>
        /// Authoring a more specific entry is the whole of overriding. There is
        /// no inheritance to declare and no combination to enumerate: the more
        /// specific row simply wins.
        /// </summary>
        [Test]
        public void A_more_specific_entry_wins_over_a_general_one()
        {
            Sprite anyCard = VisualTestFactory.Picture("any");
            Sprite anyMinion = VisualTestFactory.Picture("minion");
            Sprite neutralMinion = VisualTestFactory.Picture("neutral minion");

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20, required: true));

            // Deliberately authored least specific first, so a test that passed
            // by accident of list order would be caught.
            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, anyCard),
                VisualTestFactory.Entry(CardVisualSlot.Frame, anyMinion, type: CardType.Minion),
                VisualTestFactory.Entry(CardVisualSlot.Frame, neutralMinion,
                    type: CardType.Minion, cardClass: CardClass.Neutral));

            Compose(VisualTestFactory.Card(), recipe, catalog);

            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(neutralMinion));
        }

        [Test]
        public void A_type_constraint_outranks_a_rarity_one()
        {
            Sprite anyMinion = VisualTestFactory.Picture("minion");
            Sprite anyLegendary = VisualTestFactory.Picture("legendary");

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, anyLegendary, rarity: Rarity.Legendary),
                VisualTestFactory.Entry(CardVisualSlot.Frame, anyMinion, type: CardType.Minion));

            Compose(VisualTestFactory.Card(rarity: Rarity.Legendary), recipe, catalog);

            // The type decides the shape of a card; a rarity decorates it. The
            // policy is written into the weights, and this is it in action.
            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(anyMinion));
        }

        // ------------------------------------------------------------------
        //  Fallbacks
        // ------------------------------------------------------------------

        /// <summary>
        /// Falling back is the specific entry not existing, and nothing else.
        /// The card still composes, and the resolution says it was not exact so
        /// a report can point at the gap.
        /// </summary>
        [Test]
        public void With_no_specific_entry_the_general_one_is_used_and_says_so()
        {
            Sprite anyMinion = VisualTestFactory.Picture("minion");

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, anyMinion, type: CardType.Minion));

            CardVisualResolution resolution =
                catalog.Resolve(CardVisualSlot.Frame, VisualTestFactory.Card(rarity: Rarity.Legendary));

            Assert.That(resolution.Found, Is.True);
            Assert.That(resolution.Sprite, Is.SameAs(anyMinion));
            Assert.That(resolution.IsExact, Is.False,
                "A general entry standing in for a specific one is a fallback, and should say so.");
        }

        [Test]
        public void Nothing_is_ever_chosen_when_nothing_applies()
        {
            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("spell"),
                    type: CardType.Spell));

            CardVisualResolution resolution =
                catalog.Resolve(CardVisualSlot.Frame, VisualTestFactory.Card(CardType.Minion));

            Assert.That(resolution.Found, Is.False,
                "A minion was handed a spell frame because it was the only one there.");
            Assert.That(resolution.Sprite, Is.Null);
        }

        // ------------------------------------------------------------------
        //  Missing pictures
        // ------------------------------------------------------------------

        /// <summary>
        /// Most layers are optional, and a missing one is silence rather than an
        /// error. A frame that already draws its own name banner leaves that
        /// slot empty on purpose, and a set symbol nobody has drawn yet is not a
        /// fault.
        /// </summary>
        [Test]
        public void An_optional_layer_with_no_picture_is_skipped_without_complaint()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Emblem", CardVisualSlot.ExpansionEmblem, 110),
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20, required: true));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("frame")));

            Compose(VisualTestFactory.Card(), recipe, catalog);

            Assert.That(_plan.Draws(CardVisualSlot.ExpansionEmblem), Is.False);
            Assert.That(_plan.IsComplete, Is.True, "An optional layer was reported as a gap.");
            Assert.That(_plan.Layers.Count, Is.EqualTo(1));
        }

        [Test]
        public void A_required_layer_with_no_picture_is_reported_and_the_card_still_draws()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20, required: true),
                VisualTestFactory.Picture("Gem", CardVisualSlot.ManaGem, 60));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.ManaGem, VisualTestFactory.Picture("gem")));

            Compose(VisualTestFactory.Card(), recipe, catalog);

            Assert.That(_plan.IsComplete, Is.False);
            Assert.That(_plan.Gaps.Count, Is.EqualTo(1));
            Assert.That(_plan.Gaps[0].Slot, Is.EqualTo(CardVisualSlot.Frame));

            // Still drawn, so a missing file is something somebody can see and
            // fix rather than an exception in the middle of a match.
            Assert.That(_plan.Draws(CardVisualSlot.ManaGem), Is.True);
        }

        [Test]
        public void An_empty_catalog_produces_no_exception()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20, required: true));

            Assert.DoesNotThrow(() => Compose(VisualTestFactory.Card(), recipe, null));
            Assert.That(_plan.Layers, Is.Empty);
            Assert.That(_plan.Gaps.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------
        //  Conditions
        // ------------------------------------------------------------------

        [Test]
        public void A_basic_card_wears_no_rarity_stone()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("RarityGem", CardVisualSlot.RarityGem, 90,
                    conditions: new CardVisualCondition(
                        CardVisualField.Rarity, CardVisualComparison.NotEquals, (int)Rarity.Free)));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.RarityGem, VisualTestFactory.Picture("common"),
                    rarity: Rarity.Common),
                VisualTestFactory.Entry(CardVisualSlot.RarityGem, VisualTestFactory.Picture("legendary"),
                    rarity: Rarity.Legendary));

            Compose(VisualTestFactory.Card(rarity: Rarity.Free), recipe, catalog);
            Assert.That(_plan.Draws(CardVisualSlot.RarityGem), Is.False);

            Compose(VisualTestFactory.Card(rarity: Rarity.Common), recipe, catalog);
            Assert.That(_plan.Draws(CardVisualSlot.RarityGem), Is.True);

            Compose(VisualTestFactory.Card(rarity: Rarity.Legendary), recipe, catalog);
            Assert.That(_plan.SpriteIn(CardVisualSlot.RarityGem).name, Is.EqualTo("legendary"));
        }

        [Test]
        public void A_card_with_no_tribe_draws_neither_the_plaque_nor_the_word()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("TribeBanner", CardVisualSlot.TribeBanner, 100,
                    conditions: CardVisualCondition.True(CardVisualField.HasTribe)),
                VisualTestFactory.Label("TribeText", CardVisualTextSlot.Tribe, 180));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.TribeBanner, VisualTestFactory.Picture("plaque")));

            Compose(VisualTestFactory.Card(tribe: Tribe.None), recipe, catalog);

            Assert.That(_plan.Draws(CardVisualSlot.TribeBanner), Is.False);
            Assert.That(_plan.TextIn(CardVisualTextSlot.Tribe), Is.Null,
                "An empty label was drawn rather than not drawn.");
        }

        [Test]
        public void An_empty_label_is_not_drawn_at_all()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Label("Rules", CardVisualTextSlot.RulesText, 150));

            Compose(VisualTestFactory.Card(rules: ""), recipe, VisualTestFactory.Catalog());
            Assert.That(_plan.Layers, Is.Empty);

            Compose(VisualTestFactory.Card(rules: "Battlecry: draw a card."), recipe, VisualTestFactory.Catalog());
            Assert.That(_plan.TextIn(CardVisualTextSlot.RulesText), Is.EqualTo("Battlecry: draw a card."));
        }

        [Test]
        public void Comparisons_other_than_equality_work()
        {
            CardVisualCondition rareOrBetter = new CardVisualCondition(
                CardVisualField.Rarity, CardVisualComparison.AtLeast, (int)Rarity.Rare);

            Assert.That(rareOrBetter.Matches(VisualTestFactory.Card(rarity: Rarity.Common)), Is.False);
            Assert.That(rareOrBetter.Matches(VisualTestFactory.Card(rarity: Rarity.Rare)), Is.True);
            Assert.That(rareOrBetter.Matches(VisualTestFactory.Card(rarity: Rarity.Legendary)), Is.True);
        }

        // ------------------------------------------------------------------
        //  Both sides of the card
        // ------------------------------------------------------------------

        [Test]
        public void A_face_down_card_composes_its_back_and_nothing_it_should_not_show()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20),
                VisualTestFactory.Picture("Back", CardVisualSlot.CardBack, 120,
                    face: CardVisualFace.FaceDown),
                VisualTestFactory.Label("Name", CardVisualTextSlot.Name, 130),
                VisualTestFactory.Label("Mana", CardVisualTextSlot.ManaCost, 140));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("frame")),
                VisualTestFactory.Entry(CardVisualSlot.CardBack, VisualTestFactory.Picture("back")));

            Compose(VisualTestFactory.Card(faceDown: true), recipe, catalog);

            Assert.That(_plan.Draws(CardVisualSlot.CardBack), Is.True);
            Assert.That(_plan.Draws(CardVisualSlot.Frame), Is.False, "The front showed through the back.");
            Assert.That(_plan.TextIn(CardVisualTextSlot.Name), Is.Null,
                "A face down card printed its name.");
            Assert.That(_plan.TextIn(CardVisualTextSlot.ManaCost), Is.Null,
                "A face down card printed its cost.");
        }

        // ------------------------------------------------------------------
        //  Runtime values
        // ------------------------------------------------------------------

        /// <summary>
        /// A buffed minion is the same pictures with different numbers, and the
        /// composer says so. That answer is what lets a match rewrite two labels
        /// instead of resolving a whole card every time anything changes.
        /// </summary>
        [Test]
        public void Changing_the_numbers_does_not_change_the_pictures()
        {
            CardVisualDescriptor printed = VisualTestFactory.Card(cost: 3, attack: 2, health: 3);
            CardVisualDescriptor buffed = VisualTestFactory.Card(cost: 2, attack: 4, health: 5);

            Assert.That(printed.LooksTheSameAs(buffed), Is.True);

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Label("Attack", CardVisualTextSlot.Attack, 160),
                VisualTestFactory.Label("Health", CardVisualTextSlot.Health, 170),
                VisualTestFactory.Label("Mana", CardVisualTextSlot.ManaCost, 140));

            Compose(buffed, recipe, VisualTestFactory.Catalog());

            Assert.That(_plan.TextIn(CardVisualTextSlot.Attack), Is.EqualTo("4"));
            Assert.That(_plan.TextIn(CardVisualTextSlot.Health), Is.EqualTo("5"));
            Assert.That(_plan.TextIn(CardVisualTextSlot.ManaCost), Is.EqualTo("2"));
        }

        [Test]
        public void Changing_what_the_card_is_does_change_the_pictures()
        {
            CardVisualDescriptor minion = VisualTestFactory.Card(CardType.Minion);

            Assert.That(minion.LooksTheSameAs(VisualTestFactory.Card(CardType.Spell)), Is.False);
            Assert.That(minion.LooksTheSameAs(VisualTestFactory.Card(rarity: Rarity.Legendary)), Is.False);
            Assert.That(minion.LooksTheSameAs(VisualTestFactory.Card(faceDown: true)), Is.False);
            Assert.That(
                minion.LooksTheSameAs(VisualTestFactory.Card(artwork: VisualTestFactory.Picture("art"))),
                Is.False);
        }

        // ------------------------------------------------------------------
        //  Artwork
        // ------------------------------------------------------------------

        /// <summary>
        /// Artwork is the one slot that does not come from the catalog, because
        /// it belongs to a card rather than to a kind of card. Two paintings on
        /// one frame is two descriptions, not two entries anywhere.
        /// </summary>
        [Test]
        public void The_same_frame_serves_any_number_of_paintings()
        {
            Sprite frame = VisualTestFactory.Picture("frame");
            Sprite first = VisualTestFactory.Picture("painting one");
            Sprite second = VisualTestFactory.Picture("painting two");

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Artwork", CardVisualSlot.Artwork, 10,
                    conditions: CardVisualCondition.True(CardVisualField.HasArtwork)),
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, frame, type: CardType.Minion));

            Compose(VisualTestFactory.Card(artwork: first), recipe, catalog);
            Assert.That(_plan.SpriteIn(CardVisualSlot.Artwork), Is.SameAs(first));
            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(frame));

            Compose(VisualTestFactory.Card(artwork: second), recipe, catalog);
            Assert.That(_plan.SpriteIn(CardVisualSlot.Artwork), Is.SameAs(second));
            Assert.That(_plan.SpriteIn(CardVisualSlot.Frame), Is.SameAs(frame),
                "Changing the painting changed the frame.");
        }

        [Test]
        public void The_painting_is_drawn_behind_the_frame()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20),
                VisualTestFactory.Picture("Artwork", CardVisualSlot.Artwork, 10,
                    conditions: CardVisualCondition.True(CardVisualField.HasArtwork)));

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("frame")));

            Compose(VisualTestFactory.Card(artwork: VisualTestFactory.Picture("art")), recipe, catalog);

            Assert.That(_plan.Layers[0].Slot, Is.EqualTo(CardVisualSlot.Artwork),
                "The frame is a window, so the painting goes behind it.");
            Assert.That(_plan.Layers[1].Slot, Is.EqualTo(CardVisualSlot.Frame));
        }

        // ------------------------------------------------------------------
        //  Authoring mistakes
        // ------------------------------------------------------------------

        [Test]
        public void Two_equally_specific_entries_for_one_slot_are_reported()
        {
            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("one"),
                    type: CardType.Minion),
                VisualTestFactory.Entry(CardVisualSlot.Frame, VisualTestFactory.Picture("two"),
                    type: CardType.Minion));

            List<string> problems = new List<string>();
            catalog.Validate(problems);

            Assert.That(problems, Is.Not.Empty,
                "A card whose appearance depends on list order should be reported.");
            Assert.That(problems[0], Does.Contain("twice"));
        }

        [Test]
        public void Artwork_in_the_catalog_is_reported()
        {
            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Artwork, VisualTestFactory.Picture("art")));

            List<string> problems = new List<string>();
            catalog.Validate(problems);

            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("Artwork belongs to a card"));
        }

        // ------------------------------------------------------------------
        //  Filling the catalog in
        // ------------------------------------------------------------------

        /// <summary>
        /// A real component landing on a row with the same constraints replaces
        /// the scaffolding that was standing in for it.
        /// </summary>
        [Test]
        public void An_arriving_component_replaces_the_scaffolding_with_the_same_constraints()
        {
            Sprite scaffolding = VisualTestFactory.Picture("grey rectangle");
            Sprite real = VisualTestFactory.Picture("the real frame");

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, scaffolding, type: CardType.Minion));

            catalog.SetSprite(
                CardVisualSlot.Frame,
                VisualTestFactory.Entry(CardVisualSlot.Frame, real, type: CardType.Minion).match,
                real);

            Assert.That(catalog.Entries.Count, Is.EqualTo(1), "A duplicate row was added.");
            Assert.That(catalog.Entries[0].sprite, Is.SameAs(real));
        }

        /// <summary>
        /// A component that is more specific than the scaffolding is added
        /// beside it, not over it.
        ///
        /// This is the case that actually happens. The scaffolding frame is
        /// authored for a card type; a real one is a particular class's frame.
        /// Collapsing them would make one class's artwork silently answer for
        /// every class, and would throw away the only fallback a class nobody
        /// has drawn yet has left.
        /// </summary>
        [Test]
        public void A_more_specific_component_is_added_beside_the_scaffolding_not_over_it()
        {
            Sprite scaffolding = VisualTestFactory.Picture("grey rectangle");
            Sprite neutral = VisualTestFactory.Picture("the neutral frame");

            CardVisualCatalogAsset catalog = VisualTestFactory.Catalog(
                VisualTestFactory.Entry(CardVisualSlot.Frame, scaffolding, type: CardType.Minion));

            catalog.SetSprite(
                CardVisualSlot.Frame,
                VisualTestFactory.Entry(
                    CardVisualSlot.Frame, neutral,
                    type: CardType.Minion, cardClass: CardClass.Neutral).match,
                neutral);

            Assert.That(catalog.Entries.Count, Is.EqualTo(2), "The scaffolding row was destroyed.");

            // A neutral minion gets the real one, because it is more specific.
            Assert.That(
                catalog.Resolve(CardVisualSlot.Frame, VisualTestFactory.Card(CardType.Minion)).Sprite,
                Is.SameAs(neutral));

            // And a spell still finds nothing, rather than a minion frame.
            Assert.That(
                catalog.Resolve(CardVisualSlot.Frame, VisualTestFactory.Card(CardType.Spell)).Found,
                Is.False);
        }

        [Test]
        public void Two_layers_at_the_same_depth_are_reported()
        {
            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(
                VisualTestFactory.Picture("Frame", CardVisualSlot.Frame, 20),
                VisualTestFactory.Picture("Banner", CardVisualSlot.NameBanner, 20));

            List<string> problems = new List<string>();
            recipe.Validate(problems);

            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("sorting order"));
        }

        [Test]
        public void A_layer_that_is_both_a_picture_and_a_label_is_reported()
        {
            CardVisualLayerDefinition confused = VisualTestFactory.Picture("Both", CardVisualSlot.ManaGem, 60);
            confused.text = CardVisualTextSlot.ManaCost;

            CardVisualRecipeAsset recipe = VisualTestFactory.Recipe(confused);

            List<string> problems = new List<string>();
            recipe.Validate(problems);

            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems[0], Does.Contain("Split it in two"));
        }
    }
}
