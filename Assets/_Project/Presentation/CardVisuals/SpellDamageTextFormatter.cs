using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CoH.Core.Effects;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Rewrites a card's own printed rules text for display, the way
    /// Hearthstone's own client does for Spell Damage: the printed number is
    /// swapped for the effective one and highlighted - never the other way
    /// around.
    ///
    /// <see cref="CoH.Core.Cards.CardDefinition.Text"/> is read here and
    /// never written. A fresh string is produced every time this is called
    /// - once per card, every time <c>MatchPresenter</c> rebuilds a hand -
    /// and nothing it returns is ever fed back into a
    /// <see cref="CoH.Core.Cards.CardDefinition"/>, a data asset, or
    /// <see cref="CardViewModel.RulesText"/>'s own source. The printed
    /// value on the card stays exactly what was authored; only the
    /// momentary display string differs.
    ///
    /// Deliberately narrow. Only a damaging spell's own DealDamage amount is
    /// ever a candidate - the same boundary
    /// <see cref="CoH.Core.Rules.SpellDamageSystem"/> already enforces at
    /// the gameplay boundary (<see cref="EffectTrigger.OnPlay"/> only, never
    /// a hero power's, a battlecry's or a deathrattle's damage) - and even
    /// then only the one number that effect actually prints, found by
    /// requiring the digits to sit immediately before the word "damage" (or
    /// its French equivalent). Never a blind search-and-replace of every
    /// number in the text: "Deal 3 damage. Draw 1 card." with the bonus
    /// active becomes "Deal *4* damage. Draw 1 card." - the draw count is
    /// never a candidate, because nothing here ever looks at it.
    /// </summary>
    internal static class SpellDamageTextFormatter
    {
        /// <summary>
        /// Hearthstone's own "this number is better than printed" green. No
        /// colour convention already existed anywhere else in the project
        /// to match (checked before adding this) - this is the first one,
        /// chosen to read clearly against the rules panel's own background.
        /// </summary>
        internal const string HighlightColorHex = "#2FCB4A";

        private static readonly string[] DamageWords = { "damage", "dégâts", "degats" };

        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// The text to actually show, given the card's own printed text,
        /// its authored effects, and the controlling player's current
        /// Spell Damage.
        ///
        /// With no bonus, or nothing on the card Spell Damage could ever
        /// touch, this returns <paramref name="rulesText"/> completely
        /// unchanged - not a copy, the same reference - so a card with
        /// nothing to highlight costs nothing beyond the check itself.
        /// </summary>
        public static string Format(
            string rulesText, IReadOnlyList<EffectDefinition> effects, int spellDamageBonus)
        {
            if (string.IsNullOrEmpty(rulesText) || spellDamageBonus <= 0 ||
                effects == null || effects.Count == 0)
            {
                return rulesText;
            }

            string text = rulesText;

            for (int index = 0; index < effects.Count; index++)
            {
                EffectDefinition effect = effects[index];

                // Spell Damage's own boundary, mirrored from
                // ResolveEffectsAction.DealDamage: only a damaging spell's
                // own effect (OnPlay), never a hero power's, a battlecry's
                // or a deathrattle's - even though all four can carry a
                // DealDamage action.
                if (effect.Trigger != EffectTrigger.OnPlay ||
                    effect.Action.Kind != EffectActionKind.DealDamage)
                {
                    continue;
                }

                int baseAmount = effect.Action.Amount;

                if (baseAmount <= 0)
                {
                    continue;
                }

                text = ReplaceFirstDamageNumber(text, baseAmount, baseAmount + spellDamageBonus);
            }

            return text;
        }

        /// <summary>
        /// Finds the first standalone occurrence of <paramref name="baseAmount"/>
        /// that is immediately followed by a damage word, and swaps it for
        /// the highlighted, boosted number. A card with two DealDamage rows
        /// of the same printed amount still resolves correctly across two
        /// calls: the first call's replacement no longer contains a bare
        /// "N damage" substring (it is wrapped in a colour tag and
        /// asterisks by then), so a second call for the same base amount
        /// naturally finds the next real occurrence rather than the one
        /// already rewritten.
        /// </summary>
        private static string ReplaceFirstDamageNumber(string text, int baseAmount, int effectiveAmount)
        {
            foreach (string word in DamageWords)
            {
                string pattern = @"(?<!\d)" + baseAmount + @"(?!\d)(?=\s+" + word + @"\b)";
                Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase, MatchTimeout);

                if (match.Success)
                {
                    string replacement =
                        "<color=" + HighlightColorHex + ">*" + effectiveAmount + "*</color>";

                    return text.Substring(0, match.Index) + replacement +
                           text.Substring(match.Index + match.Length);
                }
            }

            return text;
        }
    }
}
