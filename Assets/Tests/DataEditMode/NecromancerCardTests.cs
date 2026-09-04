using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Data;
using NUnit.Framework;

namespace CoH.Tests.DataEditMode
{
    /// <summary>
    /// The Necromancer's authored cards, read out of the real assets.
    ///
    /// The engine tests build these five cards in C# so that a Core test never
    /// touches a ScriptableObject. That leaves exactly one thing unchecked -
    /// whether the assets the game actually loads say the same - and this is
    /// where it is checked. Every number the design locked down is asserted
    /// here against the file on disk.
    /// </summary>
    public sealed class NecromancerCardTests
    {
        private const string Folder = "Assets/_Project/Data/Cards/";

        private static CardCatalog Catalog() => AuthoredCards.Catalog().BuildRuntimeCatalog();

        private static CardDefinition Get(string cardId)
        {
            CardCatalog catalog = Catalog();

            Assert.That(catalog.TryGet(new CardId(cardId), out CardDefinition definition), Is.True,
                cardId + " is not in the starter catalog, so no match could ever summon it.");

            return definition;
        }

        // ==================================================================
        //  The four servants
        // ==================================================================

        private static void AssertServant(
            CardDefinition card,
            string cardId,
            string name,
            int attack,
            int health,
            CardKeywords keywords)
        {
            Assert.That(card.Id.Value, Is.EqualTo(cardId));
            Assert.That(card.Name, Is.EqualTo(name));
            Assert.That(card.Type, Is.EqualTo(CardType.Minion));
            Assert.That(card.Class, Is.EqualTo(CardClass.Necromancer));
            Assert.That(card.ManaCost, Is.EqualTo(1), name + " must have a printed cost of one.");
            Assert.That(card.Attack, Is.EqualTo(attack));
            Assert.That(card.Health, Is.EqualTo(health));
            Assert.That(card.Keywords, Is.EqualTo(keywords));
            Assert.That(card.Collectible, Is.False, name + " must not be deck-buildable.");
        }

        [Test]
        public void Skeletal_warrior_is_a_one_mana_one_one_with_rush()
        {
            AssertServant(
                Get("necromancer_skeletal_warrior"), "necromancer_skeletal_warrior",
                "Skeletal Warrior", 1, 1, CardKeywords.Rush);
        }

        [Test]
        public void Skeletal_rogue_is_a_one_mana_zero_one_with_stealth_and_nothing_else()
        {
            CardDefinition rogue = Get("necromancer_skeletal_rogue");

            AssertServant(
                rogue, "necromancer_skeletal_rogue", "Skeletal Rogue", 0, 1, CardKeywords.Stealth);

            // An earlier design gave this card a battlecry. It does not have
            // one, and this is what stops it coming back by accident.
            Assert.That(rogue.HasEffects, Is.False,
                "Skeletal Rogue has no battlecry, no damage and no summon trigger.");

            Assert.That(rogue.Text, Is.EqualTo("Camouflage"),
                "Stealth is shown to the player as Camouflage.");
        }

        [Test]
        public void Crypt_fiend_is_a_plain_one_mana_one_two()
        {
            CardDefinition fiend = Get("necromancer_crypt_fiend");

            AssertServant(
                fiend, "necromancer_crypt_fiend", "Crypt Fiend", 1, 2, CardKeywords.None);

            Assert.That(fiend.HasEffects, Is.False, "Crypt Fiend does nothing beyond being a body.");
            Assert.That(fiend.Text, Is.Empty);
        }

        [Test]
        public void Abomination_is_a_one_mana_zero_two_with_taunt()
        {
            CardDefinition abomination = Get("necromancer_abomination");

            AssertServant(
                abomination, "necromancer_abomination", "Abomination", 0, 2, CardKeywords.Taunt);

            Assert.That(abomination.Text, Is.EqualTo("Provocation"),
                "Taunt is shown to the player as Provocation.");
        }

        [Test]
        public void Skeletal_warriors_rules_text_names_its_keyword()
        {
            Assert.That(Get("necromancer_skeletal_warrior").Text, Is.EqualTo("Rush"));
        }

        // ==================================================================
        //  The hero power
        // ==================================================================

        [Test]
        public void The_hero_power_is_authored_as_a_one_mana_necromancer_hero_power()
        {
            CardDefinition power = Get("necromancer_choose_your_weapons");

            Assert.That(power.Type, Is.EqualTo(CardType.HeroPower));
            Assert.That(power.Class, Is.EqualTo(CardClass.Necromancer));
            Assert.That(power.ManaCost, Is.EqualTo(1));
            Assert.That(power.Collectible, Is.False);
            Assert.That(power.Name, Is.EqualTo("Raise"));
        }

        [Test]
        public void The_authored_hero_power_offers_the_four_servants_in_order()
        {
            IReadOnlyList<EffectDefinition> options =
                HeroPowerOptions.Of(Get("necromancer_choose_your_weapons"));

            string[] expected =
            {
                "necromancer_skeletal_warrior",
                "necromancer_skeletal_rogue",
                "necromancer_crypt_fiend",
                "necromancer_abomination"
            };

            Assert.That(options.Count, Is.EqualTo(expected.Length));

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(options[index].Action.Kind, Is.EqualTo(EffectActionKind.Summon));
                Assert.That(options[index].Action.SummonCount, Is.EqualTo(1));
                Assert.That(options[index].Action.SummonCardId.Value, Is.EqualTo(expected[index]));
            }
        }

        /// <summary>
        /// The assets and the engine tests describe the same five cards.
        ///
        /// Without this the two could drift: the C# definitions would keep
        /// passing every Core test while the game loaded something else
        /// entirely.
        /// </summary>
        [Test]
        public void Every_authored_option_names_a_card_the_catalog_holds()
        {
            CardCatalog catalog = Catalog();

            IReadOnlyList<EffectDefinition> options =
                HeroPowerOptions.Of(Get("necromancer_choose_your_weapons"));

            for (int index = 0; index < options.Count; index++)
            {
                CardId summoned = options[index].Action.SummonCardId;

                Assert.That(catalog.TryGet(summoned, out CardDefinition servant), Is.True,
                    "Option " + index + " summons " + summoned + ", which the catalog does not hold.");

                Assert.That(servant.Type, Is.EqualTo(CardType.Minion));
            }
        }

        // ==================================================================
        //  Collectibility
        // ==================================================================

        [Test]
        public void The_authored_assets_validate_cleanly()
        {
            List<string> problems = new List<string>();
            AuthoredCards.Catalog().Validate(problems);

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Every_necromancer_card_is_non_collectible()
        {
            string[] all =
            {
                "necromancer_skeletal_warrior",
                "necromancer_skeletal_rogue",
                "necromancer_crypt_fiend",
                "necromancer_abomination",
                "necromancer_choose_your_weapons"
            };

            for (int index = 0; index < all.Length; index++)
            {
                Assert.That(Get(all[index]).Collectible, Is.False, all[index] + " is collectible.");
            }
        }

        [Test]
        public void The_assets_exist_where_the_project_expects_them()
        {
            string[] files =
            {
                "Card_NecromancerSkeletalWarrior",
                "Card_NecromancerSkeletalRogue",
                "Card_NecromancerCryptFiend",
                "Card_NecromancerAbomination",
                "Card_NecromancerChooseYourWeapons"
            };

            for (int index = 0; index < files.Length; index++)
            {
                string path = Folder + files[index] + ".asset";

                Assert.That(
                    UnityEditor.AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(path),
                    Is.Not.Null, "Missing authored asset at " + path);
            }
        }
    }
}
