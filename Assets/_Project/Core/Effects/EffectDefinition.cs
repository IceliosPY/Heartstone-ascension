using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;

namespace CoH.Core.Effects
{
    /// <summary>
    /// When an effect happens.
    ///
    /// Deliberately few. Every trigger is a promise the engine has to keep
    /// forever, and one added because it might be useful is one more thing that
    /// has to keep working. The rest arrive when a card needs them.
    /// </summary>
    public enum EffectTrigger
    {
        None = 0,

        /// <summary>
        /// A card was played from a hand, whatever kind of card it was.
        ///
        /// This is what a spell does: a spell is nothing but its effects, so
        /// playing it and resolving it are the same moment.
        /// </summary>
        OnPlay = 1,

        /// <summary>
        /// A minion played from a hand has arrived on the board.
        ///
        /// Kept apart from <see cref="OnPlay"/> even though the two share their
        /// plumbing, because they answer different questions. A summoned token
        /// is not played and has no battlecry; a spell is played and never has
        /// one either.
        /// </summary>
        Battlecry = 2,

        /// <summary>
        /// A minion died and a death phase has taken it off the board.
        ///
        /// Distinct from "a minion left play". Being destroyed, returned to a
        /// hand or shuffled away are three different things, and only the first
        /// is a death.
        /// </summary>
        Deathrattle = 3,

        /// <summary>
        /// One of the things a hero power can do when it is used.
        ///
        /// Unlike the others this is not a moment: a hero power card's effects
        /// with this trigger are its menu, in authored order, and using the
        /// power resolves exactly one of them. A power with a single row offers
        /// no choice; a power with four rows offers four. See
        /// <see cref="CoH.Core.Rules.HeroPowerOptions"/>.
        /// </summary>
        HeroPower = 4
    }

    /// <summary>What an effect reaches.</summary>
    public enum SelectorKind
    {
        None = 0,

        /// <summary>The thing the effect belongs to.</summary>
        Self = 1,

        /// <summary>Whatever the player picked when playing the card.</summary>
        ChosenTarget = 2,

        FriendlyHero = 3,
        EnemyHero = 4,

        AllFriendlyMinions = 5,
        AllEnemyMinions = 6,

        /// <summary>Every minion in play, friendly side first.</summary>
        AllMinions = 7,

        /// <summary>Every character in play, heroes included.</summary>
        AllCharacters = 8
    }

    /// <summary>
    /// What a player is allowed to point at.
    ///
    /// Only meaningful for <see cref="SelectorKind.ChosenTarget"/>. Friendly and
    /// enemy are always relative to whoever controls the effect, never to a
    /// seat number.
    /// </summary>
    public enum TargetFilter
    {
        AnyCharacter = 0,
        AnyMinion = 1,
        FriendlyCharacter = 2,
        FriendlyMinion = 3,
        EnemyCharacter = 4,
        EnemyMinion = 5
    }

    /// <summary>What an effect does.</summary>
    public enum EffectActionKind
    {
        None = 0,
        DealDamage = 1,
        DrawCards = 2,
        Summon = 3,
        GainTemporaryMana = 4,
        ModifyStats = 5,

        /// <summary>
        /// Adds to the controller's Spell Damage for the rest of their
        /// current turn (see <see cref="CoH.Core.Rules.SpellDamageSystem"/>).
        /// A player-level modifier rather than something applied to an
        /// entity, which is why it needs no <see cref="SelectorKind"/> other
        /// than <see cref="SelectorKind.Self"/> - there is no target, only a
        /// controller.
        /// </summary>
        GrantSpellDamage = 6,

        /// <summary>
        /// Gives the controller back mana already spent this turn, up to
        /// the crystals they actually have - never a temporary crystal the
        /// way <see cref="GainTemporaryMana"/> is. A player-level effect
        /// exactly like <see cref="GrantSpellDamage"/>, for the same reason:
        /// there is no target, only a controller.
        /// </summary>
        RestoreMana = 7
    }

    /// <summary>
    /// Where an effect action's <see cref="EffectActionDefinition.Amount"/>
    /// actually comes from.
    ///
    /// Almost every action is authored with a fixed number, but a handful
    /// need to read a live number off the caster instead - Huntress Shot's
    /// mana restoration scaling with the caster's current Spell Damage is
    /// the first of these. One generic switch here is what lets that stay
    /// data: the alternative was a card-specific action, for one number, on
    /// one card.
    /// </summary>
    public enum EffectValueSource
    {
        /// <summary>The authored <see cref="EffectActionDefinition.Amount"/>, unchanged.</summary>
        Fixed = 0,

        /// <summary>The controller's current <see cref="CoH.Core.State.Player.SpellDamageBonus"/>.</summary>
        SpellDamage = 1
    }

    /// <summary>Where a summoned minion is placed.</summary>
    public enum SummonPlacement
    {
        /// <summary>At the right end of the controller's board.</summary>
        Rightmost = 0
    }

    /// <summary>
    /// Whether playing a card asks the player to point at something.
    ///
    /// Three values rather than a boolean, because a spell and a minion answer
    /// differently when nothing legal is in play. A spell is only its effect, so
    /// with nothing to aim at there is nothing to buy; a minion is also a body,
    /// so it goes down and the battlecry simply does not happen. That is how
    /// Hearthstone behaves, and it is why this is not a yes or no question.
    /// </summary>
    public enum PlayTargetRequirement
    {
        /// <summary>Nothing to point at.</summary>
        None = 0,

        /// <summary>A target is needed. With none available the card cannot be played.</summary>
        Required = 1,

        /// <summary>
        /// A target is taken when one exists, and the card is still playable
        /// when none does.
        /// </summary>
        Optional = 2
    }

    /// <summary>
    /// Who or what an effect reaches, as data.
    ///
    /// Kept apart from the action so that dealing damage does not have to be
    /// written once per kind of victim. One DealDamage plus a selector replaces
    /// the row of DealDamageToEnemyMinion, DealDamageToFriendlyHero and the rest
    /// that a game grows when the two are welded together.
    /// </summary>
    public sealed class SelectorDefinition
    {
        public static readonly SelectorDefinition Nothing = new SelectorDefinition(SelectorKind.None);

        public SelectorDefinition(SelectorKind kind, TargetFilter filter = TargetFilter.AnyCharacter)
        {
            Kind = kind;
            Filter = filter;
        }

        public SelectorKind Kind { get; }

        /// <summary>Only read for <see cref="SelectorKind.ChosenTarget"/>.</summary>
        public TargetFilter Filter { get; }

        /// <summary>True when playing the card means pointing at something.</summary>
        public bool NeedsChosenTarget => Kind == SelectorKind.ChosenTarget;

        public string Describe() =>
            Kind == SelectorKind.ChosenTarget ? Kind + "(" + Filter + ")" : Kind.ToString();

        public override string ToString() => Describe();
    }

    /// <summary>
    /// What an effect does, as data.
    ///
    /// One flat record with a kind and the numbers every kind might want, rather
    /// than a class per action shape. It costs a few unused fields and buys
    /// something worth much more: it serialises in Unity without ceremony, it
    /// compares and fingerprints as plain values, and adding an action is an
    /// enum entry plus a resolver rather than a new authoring type.
    /// </summary>
    public sealed class EffectActionDefinition
    {
        public static readonly EffectActionDefinition Nothing =
            new EffectActionDefinition(EffectActionKind.None);

        public EffectActionDefinition(
            EffectActionKind kind,
            int amount = 0,
            int attackDelta = 0,
            int healthDelta = 0,
            CardId summonCardId = default,
            int summonCount = 1,
            SummonPlacement placement = SummonPlacement.Rightmost,
            EffectValueSource amountSource = EffectValueSource.Fixed)
        {
            Kind = kind;
            Amount = amount;
            AttackDelta = attackDelta;
            HealthDelta = healthDelta;
            SummonCardId = summonCardId;
            SummonCount = summonCount;
            Placement = placement;
            AmountSource = amountSource;
        }

        public EffectActionKind Kind { get; }

        /// <summary>
        /// Damage dealt, cards drawn, or mana gained, depending on the kind -
        /// read only when <see cref="AmountSource"/> is
        /// <see cref="EffectValueSource.Fixed"/>. A source other than Fixed
        /// overrides it with a live number read off the caster instead.
        /// </summary>
        public int Amount { get; }

        /// <summary>Where <see cref="Amount"/> actually comes from at resolution.</summary>
        public EffectValueSource AmountSource { get; }

        public int AttackDelta { get; }

        public int HealthDelta { get; }

        public CardId SummonCardId { get; }

        public int SummonCount { get; }

        public SummonPlacement Placement { get; }

        public string Describe() => Kind switch
        {
            EffectActionKind.DealDamage => "DealDamage(" + Amount + ")",
            EffectActionKind.DrawCards => "DrawCards(" + Amount + ")",
            EffectActionKind.GainTemporaryMana => "GainTemporaryMana(" + Amount + ")",
            EffectActionKind.ModifyStats => "ModifyStats(" + Sign(AttackDelta) + "/" + Sign(HealthDelta) + ")",
            EffectActionKind.Summon => "Summon(" + SummonCardId.Value + " x" + SummonCount + ", " + Placement + ")",
            EffectActionKind.GrantSpellDamage => "GrantSpellDamage(" + Sign(Amount) + ")",
            EffectActionKind.RestoreMana => "RestoreMana(" + DescribeAmount() + ")",
            _ => "None"
        };

        private string DescribeAmount() =>
            AmountSource == EffectValueSource.Fixed ? Amount.ToString() : AmountSource.ToString();

        private static string Sign(int value) => (value >= 0 ? "+" : string.Empty) + value;

        public override string ToString() => Describe();
    }

    /// <summary>
    /// One thing a card does: when, to what, and what happens.
    ///
    /// A card is a list of these, and a card with an empty list is a plain body
    /// exactly as before. That is the whole point of the phase: a new card is a
    /// new row of data, not a new class.
    /// </summary>
    public sealed class EffectDefinition
    {
        public EffectDefinition(
            EffectTrigger trigger, SelectorDefinition selector, EffectActionDefinition action)
        {
            Trigger = trigger;
            Selector = selector ?? SelectorDefinition.Nothing;
            Action = action ?? EffectActionDefinition.Nothing;
        }

        public EffectTrigger Trigger { get; }

        public SelectorDefinition Selector { get; }

        public EffectActionDefinition Action { get; }

        public string Describe() => Trigger + ": " + Selector.Describe() + " -> " + Action.Describe();

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Reading a card's effects without every caller writing the same loop.
    ///
    /// Order is preserved everywhere: a card that damages and then draws must
    /// do so in that order, and nothing here sorts, groups or reorders.
    /// </summary>
    public static class EffectQueries
    {
        private static readonly EffectDefinition[] Nothing = Array.Empty<EffectDefinition>();

        /// <summary>Every effect of a card with this trigger, in definition order.</summary>
        public static IReadOnlyList<EffectDefinition> WithTrigger(
            IReadOnlyList<EffectDefinition> effects, EffectTrigger trigger)
        {
            if (effects == null || effects.Count == 0)
            {
                return Nothing;
            }

            List<EffectDefinition> matching = null;

            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index].Trigger != trigger)
                {
                    continue;
                }

                matching ??= new List<EffectDefinition>();
                matching.Add(effects[index]);
            }

            return matching ?? (IReadOnlyList<EffectDefinition>)Nothing;
        }

        public static bool HasTrigger(IReadOnlyList<EffectDefinition> effects, EffectTrigger trigger)
        {
            if (effects == null)
            {
                return false;
            }

            for (int index = 0; index < effects.Count; index++)
            {
                if (effects[index].Trigger == trigger)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The selector a card asks the player to fill in, or null.
        ///
        /// Only the effects that resolve when the card is played can ask: a
        /// deathrattle happens long after anybody could point at anything.
        /// </summary>
        public static SelectorDefinition FindPlayTargetSelector(IReadOnlyList<EffectDefinition> effects)
        {
            if (effects == null)
            {
                return null;
            }

            for (int index = 0; index < effects.Count; index++)
            {
                EffectDefinition effect = effects[index];

                bool resolvesOnPlay =
                    effect.Trigger == EffectTrigger.OnPlay || effect.Trigger == EffectTrigger.Battlecry;

                if (resolvesOnPlay && effect.Selector.NeedsChosenTarget)
                {
                    return effect.Selector;
                }
            }

            return null;
        }
    }
}
