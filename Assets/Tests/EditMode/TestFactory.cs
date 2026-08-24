using CoH.Core.Cards;
using CoH.Core.Identifiers;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The smallest set of helpers the domain tests actually need today.
    ///
    /// Deliberately not a scenario DSL: there are no rules yet to describe, so
    /// building a "Given a player with 3 mana who plays a 2 cost card" language
    /// would only be guessing at an API. It grows when real rules arrive.
    /// </summary>
    internal static class TestFactory
    {
        public const string MinionCardId = "test_minion";
        public const string SpellCardId = "test_spell";

        public static CardDefinition MinionDefinition(
            string id = MinionCardId,
            string name = "Test Minion",
            int manaCost = 2,
            int attack = 2,
            int health = 3) =>
            new CardDefinition(new CardId(id), name, CardType.Minion, manaCost, attack, health);

        public static CardDefinition SpellDefinition(
            string id = SpellCardId,
            string name = "Test Spell",
            int manaCost = 1) =>
            new CardDefinition(new CardId(id), name, CardType.Spell, manaCost);

        /// <summary>A catalog holding the standard test minion and spell.</summary>
        public static CardCatalog Catalog(params CardDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                definitions = new[] { MinionDefinition(), SpellDefinition() };
            }

            return new CardCatalog(definitions);
        }

        /// <summary>A freshly constructed match state: two heroes, four empty zones each.</summary>
        public static GameState Game(ulong seed = 1UL, params CardDefinition[] definitions) =>
            new GameState(GameConfig.Default, Catalog(definitions), seed);
    }

    /// <summary>
    /// A plain reference type for zone tests, used instead of string so that
    /// interned literals cannot make two logically distinct items compare as
    /// the same reference.
    /// </summary>
    internal sealed class TestItem
    {
        public TestItem(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString() => Name;
    }
}
