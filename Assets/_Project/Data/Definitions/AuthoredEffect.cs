using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Data
{
    /// <summary>
    /// One effect, as authored in the Unity inspector.
    ///
    /// A flat record with a trigger, a selector and an action rather than a
    /// hierarchy of polymorphic objects. Unity can serialise polymorphism with
    /// SerializeReference, but it brings duplication hazards, an inspector that
    /// needs custom drawers to be usable, and a shape that is harder to compare
    /// and fingerprint. Enums and a few numbers give an inspector that works out
    /// of the box, values that fingerprint as themselves, and a conversion that
    /// is written out field by field like every other one in this layer.
    ///
    /// The cost is a handful of fields that are meaningless for some actions.
    /// That is a cheaper price than the alternative, and the validation below
    /// says so when one of them is filled in by mistake.
    /// </summary>
    [System.Serializable]
    public sealed class AuthoredEffect
    {
        [Tooltip("When this happens. Battlecry is a minion arriving from a hand; OnPlay is a spell resolving.")]
        [SerializeField] private EffectTrigger trigger = EffectTrigger.Battlecry;

        [Header("Who it reaches")]
        [SerializeField] private SelectorKind selector = SelectorKind.ChosenTarget;

        [Tooltip("Only read for ChosenTarget: what the player is allowed to point at.")]
        [SerializeField] private TargetFilter targetFilter = TargetFilter.AnyCharacter;

        [Header("What it does")]
        [SerializeField] private EffectActionKind action = EffectActionKind.DealDamage;

        [Tooltip("Damage dealt, cards drawn, or mana gained, depending on the action. " +
                 "Ignored when Amount Source below is not Fixed.")]
        [SerializeField] private int amount = 1;

        [Tooltip(
            "Where Amount actually comes from. Fixed is the number above, unchanged. Spell " +
            "Damage reads the controller's current Spell Damage instead, live, at resolution - " +
            "Huntress Shot's mana restoration is what this exists for.")]
        [SerializeField] private EffectValueSource amountSource = EffectValueSource.Fixed;

        [Header("Modify statistics")]
        [SerializeField] private int attackDelta;
        [SerializeField] private int healthDelta;

        [Header("Summon")]
        [Tooltip("The card to summon. Must be a minion the catalog knows.")]
        [SerializeField] private string summonCardId = string.Empty;

        [SerializeField] private int summonCount = 1;
        [SerializeField] private SummonPlacement placement = SummonPlacement.Rightmost;

        public EffectTrigger Trigger => trigger;

        public SelectorKind Selector => selector;

        public EffectActionKind Action => action;

        /// <summary>The plain, engine-facing effect. No Unity type crosses over.</summary>
        public EffectDefinition ToDefinition() =>
            new EffectDefinition(
                trigger,
                new SelectorDefinition(selector, targetFilter),
                new EffectActionDefinition(
                    action,
                    amount,
                    attackDelta,
                    healthDelta,
                    string.IsNullOrEmpty(summonCardId) ? default : new CardId(summonCardId),
                    summonCount,
                    placement,
                    amountSource));

        /// <summary>
        /// Says what is wrong with this effect, in sentences.
        ///
        /// Worth being fussy here. An effect that says nothing is a card that
        /// does nothing for no visible reason, and the authoring inspector is
        /// the last place it can be caught before a match is confusing.
        /// </summary>
        public void Validate(string cardLabel, int index, CardType cardType, List<string> problems)
        {
            string where = cardLabel + " effect [" + index + "]";

            if (trigger == EffectTrigger.None)
            {
                problems.Add(where + ": no trigger is set, so it would never happen.");
            }

            if (action == EffectActionKind.None)
            {
                problems.Add(where + ": no action is set, so it would do nothing.");
            }

            if (trigger == EffectTrigger.Battlecry && cardType != CardType.Minion)
            {
                problems.Add(where + ": only a minion can have a battlecry, and this is a " + cardType + ".");
            }

            if (trigger == EffectTrigger.Deathrattle && cardType != CardType.Minion)
            {
                problems.Add(where + ": only a minion can have a deathrattle, and this is a " + cardType + ".");
            }

            if (trigger == EffectTrigger.OnPlay && cardType == CardType.Minion)
            {
                problems.Add(
                    where + ": a minion played from a hand uses Battlecry. OnPlay is for spells.");
            }

            if (trigger == EffectTrigger.HeroPower && cardType != CardType.HeroPower)
            {
                problems.Add(
                    where + ": only a hero power can carry a HeroPower effect, and this is a " +
                    cardType + ".");
            }

            if (cardType == CardType.HeroPower && trigger != EffectTrigger.HeroPower)
            {
                problems.Add(
                    where + ": a hero power's effects are its options and must use the HeroPower " +
                    "trigger. " + trigger + " would never fire.");
            }

            if (selector == SelectorKind.None)
            {
                problems.Add(where + ": no selector is set, so it would reach nobody.");
            }

            if (trigger == EffectTrigger.Deathrattle && selector == SelectorKind.ChosenTarget)
            {
                problems.Add(
                    where + ": a deathrattle cannot use ChosenTarget. It resolves long after " +
                    "anybody could point at anything.");
            }

            ValidateAction(where, problems);
        }

        private void ValidateAction(string where, List<string> problems)
        {
            switch (action)
            {
                case EffectActionKind.DealDamage:
                    if (amountSource == EffectValueSource.Fixed && amount <= 0)
                    {
                        problems.Add(where + ": DealDamage needs a positive amount (" + amount + ").");
                    }

                    break;

                case EffectActionKind.DrawCards:
                    if (amount <= 0)
                    {
                        problems.Add(where + ": DrawCards needs a positive count (" + amount + ").");
                    }

                    break;

                case EffectActionKind.GainTemporaryMana:
                    if (amount <= 0)
                    {
                        problems.Add(where + ": GainTemporaryMana needs a positive amount (" + amount + ").");
                    }

                    break;

                case EffectActionKind.Summon:
                    if (string.IsNullOrEmpty(summonCardId))
                    {
                        problems.Add(where + ": Summon has no card id.");
                    }
                    else if (!CardId.IsWellFormed(summonCardId))
                    {
                        problems.Add(where + ": the summoned card id must be lower_snake_case.");
                    }

                    if (summonCount <= 0)
                    {
                        problems.Add(where + ": Summon needs a positive count (" + summonCount + ").");
                    }

                    break;

                case EffectActionKind.ModifyStats:
                    if (attackDelta == 0 && healthDelta == 0)
                    {
                        problems.Add(where + ": ModifyStats changes nothing.");
                    }

                    break;

                case EffectActionKind.GrantSpellDamage:
                    if (amount <= 0)
                    {
                        problems.Add(where + ": GrantSpellDamage needs a positive amount (" + amount + ").");
                    }

                    break;

                case EffectActionKind.RestoreMana:
                    if (amountSource == EffectValueSource.Fixed && amount <= 0)
                    {
                        problems.Add(where + ": RestoreMana needs a positive amount (" + amount + ").");
                    }

                    break;
            }
        }

        /// <summary>
        /// Checks this effect against the rest of the catalog, which the card on
        /// its own cannot do: whether a summoned card exists, and whether it is
        /// something that can stand on a board.
        /// </summary>
        public void ValidateAgainstCatalog(
            string cardLabel, int index, IReadOnlyDictionary<string, CardType> knownCards, List<string> problems)
        {
            if (action != EffectActionKind.Summon || string.IsNullOrEmpty(summonCardId))
            {
                return;
            }

            string where = cardLabel + " effect [" + index + "]";

            if (!knownCards.TryGetValue(summonCardId, out CardType summonedType))
            {
                problems.Add(where + ": Summon names '" + summonCardId + "', which is not in the catalog.");
                return;
            }

            if (summonedType != CardType.Minion)
            {
                problems.Add(
                    where + ": Summon names '" + summonCardId + "', which is a " + summonedType +
                    " rather than a minion.");
            }
        }
    }
}
