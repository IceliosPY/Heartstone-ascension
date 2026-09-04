using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;

namespace CoH.Core.Rules
{
    /// <summary>
    /// The fixed, ordered list of things a hero power lets its owner choose
    /// between.
    ///
    /// This is the whole of the "choose one" mechanism, and it is deliberately
    /// nothing more than a reading of data the card already carries: the
    /// options are the card's <see cref="EffectTrigger.HeroPower"/> effects, in
    /// the order they were authored. Choosing is choosing an index into that
    /// list.
    ///
    /// Doing it this way means the engine never learns what the options are.
    /// The four Necromancer servants are four rows on one card asset; a hero
    /// power with a single option is a card with one row and needs no choice
    /// interaction at all; a hero power that deals damage instead of summoning
    /// is a different action on the same row. None of those cost a line here.
    ///
    /// There is no randomness anywhere near this. The order is the authored
    /// order, on every machine and in every replay.
    /// </summary>
    public static class HeroPowerOptions
    {
        /// <summary>
        /// The options a hero power offers, in authored order.
        ///
        /// Empty for a card that is not a hero power, or one that has no hero
        /// power effects - both of which are answered the same way, because
        /// "offers nothing to choose" is the honest description of both.
        /// </summary>
        public static IReadOnlyList<EffectDefinition> Of(CardDefinition definition)
        {
            if (definition == null || definition.Type != CardType.HeroPower)
            {
                return System.Array.Empty<EffectDefinition>();
            }

            return EffectQueries.WithTrigger(definition.Effects, EffectTrigger.HeroPower);
        }

        /// <summary>How many options there are.</summary>
        public static int CountOf(CardDefinition definition) => Of(definition).Count;

        /// <summary>
        /// Whether an index names one of them.
        ///
        /// The only thing standing between a submitted choice and the engine,
        /// so a client that sends option seventeen is refused rather than
        /// indexing off the end of a list.
        /// </summary>
        public static bool IsValidOption(CardDefinition definition, int optionIndex) =>
            optionIndex >= 0 && optionIndex < CountOf(definition);

        /// <summary>The chosen option, or null when the index names none.</summary>
        public static EffectDefinition Option(CardDefinition definition, int optionIndex)
        {
            IReadOnlyList<EffectDefinition> options = Of(definition);

            return optionIndex >= 0 && optionIndex < options.Count ? options[optionIndex] : null;
        }
    }
}
