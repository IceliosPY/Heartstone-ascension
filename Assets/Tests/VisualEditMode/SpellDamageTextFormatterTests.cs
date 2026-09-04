using System.Collections.Generic;
using CoH.Core.Effects;
using CoH.Presentation.CardVisuals;
using NUnit.Framework;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// The presentation-only rewrite that shows a damaging spell's boosted
    /// number in place of its printed one - Hearthstone's own Spell Damage
    /// treatment, never a change to the card's own data.
    ///
    /// Every test builds its own effect list from scratch rather than
    /// loading a real card, so each one proves exactly one boundary in
    /// isolation: which trigger counts, which action kind counts, which
    /// number in the text is a candidate and which is not.
    /// </summary>
    public sealed class SpellDamageTextFormatterTests
    {
        private static EffectDefinition DealDamage(EffectTrigger trigger, int amount) =>
            new EffectDefinition(
                trigger,
                new SelectorDefinition(SelectorKind.ChosenTarget, TargetFilter.AnyMinion),
                new EffectActionDefinition(EffectActionKind.DealDamage, amount));

        private static EffectDefinition DrawCards(EffectTrigger trigger, int amount) =>
            new EffectDefinition(
                trigger,
                new SelectorDefinition(SelectorKind.FriendlyHero),
                new EffectActionDefinition(EffectActionKind.DrawCards, amount));

        [Test]
        public void With_no_spell_damage_the_text_is_returned_unchanged()
        {
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage to a minion.", effects, spellDamageBonus: 0);

            Assert.That(result, Is.EqualTo("Deal 3 damage to a minion."));
        }

        [Test]
        public void With_no_spell_damage_the_exact_same_string_reference_is_returned()
        {
            string original = "Deal 3 damage to a minion.";
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            string result = SpellDamageTextFormatter.Format(original, effects, spellDamageBonus: 0);

            Assert.That(result, Is.SameAs(original),
                "A card with nothing to highlight should cost nothing beyond the check itself.");
        }

        [Test]
        public void With_spell_damage_the_printed_number_is_replaced_by_the_effective_one()
        {
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage to a minion.", effects, spellDamageBonus: 1);

            Assert.That(result, Does.Not.Contain("3 damage"),
                "The base printed number must not still be visible once boosted.");
            Assert.That(result, Does.Contain("4"),
                "The effective amount (3 base + 1 Spell Damage) must appear.");
        }

        [Test]
        public void The_boosted_number_is_marked_with_the_hearthstone_style_asterisks()
        {
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage to a minion.", effects, spellDamageBonus: 1);

            Assert.That(result, Does.Contain("*4*"),
                "The reference visual format is *n*, asterisks included, not a bare number.");
        }

        [Test]
        public void The_boosted_number_is_wrapped_in_the_highlight_colour()
        {
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage to a minion.", effects, spellDamageBonus: 1);

            Assert.That(result, Does.Contain("<color=" + SpellDamageTextFormatter.HighlightColorHex + ">*4*</color>"));
        }

        [Test]
        public void Two_spell_damage_scales_the_printed_number_by_two()
        {
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage to a minion.", effects, spellDamageBonus: 2);

            Assert.That(result, Does.Contain("*5*"));
        }

        /// <summary>
        /// The exact regression the brief called out: a damage number and
        /// an unrelated number of the same or different value in the same
        /// string. Only the one attached to a real DealDamage effect - and
        /// only the one immediately followed by the word "damage" - may
        /// change.
        /// </summary>
        [Test]
        public void Only_the_damage_number_changes_not_unrelated_numbers_in_the_same_text()
        {
            EffectDefinition[] effects =
            {
                DealDamage(EffectTrigger.OnPlay, 3),
                DrawCards(EffectTrigger.OnPlay, 1)
            };

            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage. Draw 1 card.", effects, spellDamageBonus: 1);

            Assert.That(result, Does.Contain("*4*"));
            Assert.That(result, Does.Contain("Draw 1 card."),
                "The draw count is not a damage number and must be left exactly as printed.");
            Assert.That(result, Does.Not.Contain("2 card"),
                "Spell Damage must never leak into a non-damage numeric effect.");
        }

        /// <summary>
        /// Mirrors ResolveEffectsAction.DealDamage's own boundary exactly:
        /// only EffectTrigger.OnPlay is a damaging spell. A hero power, a
        /// battlecry or a deathrattle can carry a DealDamage action too, but
        /// none of them may ever be highlighted.
        /// </summary>
        [TestCase(EffectTrigger.HeroPower)]
        [TestCase(EffectTrigger.Battlecry)]
        [TestCase(EffectTrigger.Deathrattle)]
        public void Damage_from_a_non_spell_trigger_is_never_highlighted(EffectTrigger trigger)
        {
            EffectDefinition[] effects = { DealDamage(trigger, 2) };

            string result = SpellDamageTextFormatter.Format(
                "Deal 2 damage to an enemy character.", effects, spellDamageBonus: 3);

            Assert.That(result, Is.EqualTo("Deal 2 damage to an enemy character."));
        }

        [Test]
        public void A_non_damage_action_is_never_highlighted()
        {
            EffectDefinition[] effects = { DrawCards(EffectTrigger.OnPlay, 2) };

            string result = SpellDamageTextFormatter.Format(
                "Draw 2 cards.", effects, spellDamageBonus: 5);

            Assert.That(result, Is.EqualTo("Draw 2 cards."));
        }

        [Test]
        public void No_effects_leaves_the_text_unchanged()
        {
            string result = SpellDamageTextFormatter.Format(
                "Deal 3 damage.", new List<EffectDefinition>(), spellDamageBonus: 1);

            Assert.That(result, Is.EqualTo("Deal 3 damage."));
        }

        [Test]
        public void Empty_or_null_text_is_returned_as_is()
        {
            EffectDefinition[] effects = { DealDamage(EffectTrigger.OnPlay, 3) };

            Assert.That(SpellDamageTextFormatter.Format(string.Empty, effects, 1), Is.EqualTo(string.Empty));
            Assert.That(SpellDamageTextFormatter.Format(null, effects, 1), Is.Null);
        }
    }
}
