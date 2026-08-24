using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Data;
using NUnit.Framework;
using UnityEngine;

namespace CoH.Tests.DataEditMode
{
    /// <summary>
    /// The conversion from an authored asset to the plain definition the engine
    /// consumes. Every field has to survive the trip, and nothing belonging to
    /// Unity may make it across.
    /// </summary>
    public sealed class CardDefinitionAssetTests
    {
        [Test]
        public void Test_soldier_converts_field_for_field()
        {
            CardDefinition definition = AuthoredCards.TestSoldier().ToDefinition();

            Assert.That(definition.Id, Is.EqualTo(new CardId("test_soldier")));
            Assert.That(definition.Name, Is.EqualTo("Test Soldier"));
            Assert.That(definition.Type, Is.EqualTo(CardType.Minion));
            Assert.That(definition.Class, Is.EqualTo(CardClass.Neutral));
            Assert.That(definition.Rarity, Is.EqualTo(Rarity.Free));
            Assert.That(definition.Tribe, Is.EqualTo(Tribe.None));
            Assert.That(definition.ManaCost, Is.EqualTo(2));
            Assert.That(definition.Attack, Is.EqualTo(2));
            Assert.That(definition.Health, Is.EqualTo(3));
            Assert.That(definition.Collectible, Is.True);
            Assert.That(definition.Text, Is.Empty);
        }

        [Test]
        public void The_coin_converts_as_a_non_collectible_spell()
        {
            CardDefinition definition = AuthoredCards.TheCoin().ToDefinition();

            Assert.That(definition.Id, Is.EqualTo(new CardId("the_coin")));
            Assert.That(definition.Name, Is.EqualTo("The Coin"));
            Assert.That(definition.Type, Is.EqualTo(CardType.Spell));
            Assert.That(definition.ManaCost, Is.EqualTo(0));
            Assert.That(definition.Collectible, Is.False, "The Coin is never put in a deck.");
            Assert.That(definition.Attack, Is.EqualTo(0));
            Assert.That(definition.Health, Is.EqualTo(0));
        }

        [Test]
        public void The_coins_rules_text_is_carried_but_never_interpreted()
        {
            CardDefinition definition = AuthoredCards.TheCoin().ToDefinition();

            // The text says what the card will do. Nothing reads it to find out.
            Assert.That(definition.Text, Is.EqualTo("Gain 1 Mana Crystal this turn only."));
        }

        [Test]
        public void The_id_is_independent_of_the_asset_name_and_the_display_name()
        {
            CardDefinitionAsset asset = AuthoredCards.TestSoldier();

            Assert.That(asset.name, Is.EqualTo("Card_TestSoldier"), "The file is named for humans.");
            Assert.That(asset.DisplayName, Is.EqualTo("Test Soldier"));
            Assert.That(asset.RawId, Is.EqualTo("test_soldier"), "The id is neither of those.");
        }

        [Test]
        public void Artwork_stays_on_the_unity_side()
        {
            CardDefinitionAsset asset = AuthoredCards.TestSoldier();
            CardDefinition definition = asset.ToDefinition();

            // The asset exposes artwork for the future presentation layer.
            Assert.That(typeof(CardDefinitionAsset).GetProperty(nameof(CardDefinitionAsset.Artwork)), Is.Not.Null);

            // The definition has no way to carry one. If this ever fails, a
            // UnityEngine type has leaked into the rules engine.
            foreach (System.Reflection.PropertyInfo property in typeof(CardDefinition).GetProperties())
            {
                Assert.That(
                    property.PropertyType.Namespace ?? string.Empty,
                    Does.Not.StartWith("UnityEngine"),
                    "CardDefinition." + property.Name + " exposes a Unity type.");
            }
        }

        [Test]
        public void The_authored_cards_are_valid()
        {
            List<string> problems = new List<string>();

            AuthoredCards.TestSoldier().Validate(problems);
            AuthoredCards.TheCoin().Validate(problems);

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void An_empty_id_or_name_is_reported()
        {
            Assert.That(
                Problems(card => Set(card, "cardId", string.Empty)),
                Has.Some.Contains("card id is empty"));

            Assert.That(
                Problems(card => Set(card, "displayName", string.Empty)),
                Has.Some.Contains("display name is empty"));
        }

        [Test]
        public void A_correctly_authored_card_reports_nothing()
        {
            // The baseline the other cases start from must itself be clean,
            // otherwise those cases would pass for the wrong reason.
            Assert.That(Problems(card => { }), Is.Empty);
        }

        [Test]
        public void A_badly_shaped_id_is_reported()
        {
            foreach (string bad in new[] { "Test_Soldier", "test soldier", "test-soldier", "1soldier", "test__soldier", "soldier_" })
            {
                List<string> problems = Problems(card => Set(card, "cardId", bad));
                Assert.That(problems, Has.Some.Contains("lower_snake_case"), "Accepted a bad id: " + bad);
            }

            Assert.That(CardId.IsWellFormed("test_soldier"), Is.True);
            Assert.That(CardId.IsWellFormed("skeleton_2"), Is.True);
        }

        [Test]
        public void A_negative_cost_or_attack_is_reported()
        {
            Assert.That(
                Problems(card => Set(card, "manaCost", -1)),
                Has.Some.Contains("mana cost cannot be negative"));

            Assert.That(
                Problems(card => Set(card, "attack", -2)),
                Has.Some.Contains("attack cannot be negative"));
        }

        [Test]
        public void A_minion_without_health_is_reported()
        {
            Assert.That(
                Problems(card => Set(card, "health", 0)),
                Has.Some.Contains("needs at least 1 health"));
        }

        [Test]
        public void A_spell_carrying_statistics_is_reported()
        {
            List<string> problems = Problems(card =>
            {
                Set(card, "cardType", (int)CardType.Spell);
                Set(card, "attack", 3);
            });

            Assert.That(problems, Has.Some.Contains("spell should have no attack"));
        }

        [Test]
        public void A_card_with_no_type_is_reported()
        {
            Assert.That(
                Problems(card => Set(card, "cardType", (int)CardType.None)),
                Has.Some.Contains("has no type"));
        }

        /// <summary>
        /// Builds a valid throwaway card, applies a deliberate mistake, and
        /// returns what validation says about it.
        /// </summary>
        private static List<string> Problems(System.Action<SerializedCard> breakIt)
        {
            CardDefinitionAsset asset = ScriptableObject.CreateInstance<CardDefinitionAsset>();
            asset.name = "Card_UnderTest";

            SerializedCard card = new SerializedCard(asset);
            Set(card, "cardId", "valid_card");
            Set(card, "displayName", "Valid Card");
            Set(card, "cardType", (int)CardType.Minion);
            Set(card, "manaCost", 1);
            Set(card, "attack", 1);
            Set(card, "health", 1);

            breakIt(card);

            List<string> problems = new List<string>();
            asset.Validate(problems);

            Object.DestroyImmediate(asset);
            return problems;
        }

        private static void Set(SerializedCard card, string field, object value) => card.Set(field, value);

        /// <summary>
        /// Writes the asset's private serialized fields.
        ///
        /// Reflection is confined to this test helper: the point of the fields
        /// being private is that only the inspector writes them, and a test that
        /// needed a public setter would be asking the production type to grow an
        /// API for its own convenience.
        /// </summary>
        private sealed class SerializedCard
        {
            private readonly CardDefinitionAsset _asset;

            public SerializedCard(CardDefinitionAsset asset) => _asset = asset;

            public void Set(string field, object value)
            {
                System.Reflection.FieldInfo info = typeof(CardDefinitionAsset).GetField(
                    field,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Assert.That(info, Is.Not.Null, "No such field: " + field);

                if (info.FieldType.IsEnum)
                {
                    info.SetValue(_asset, System.Enum.ToObject(info.FieldType, value));
                    return;
                }

                info.SetValue(_asset, value);
            }
        }
    }
}
