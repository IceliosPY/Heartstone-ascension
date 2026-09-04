using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Effects;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Carries out one card's effects for one trigger.
    ///
    /// All of that card's effects for that trigger, in the order they were
    /// written, inside a single action. Which means no death phase interrupts a
    /// battlecry halfway: a card that damages and then draws does both before
    /// anything is removed from the board, and a card that damages every enemy
    /// minion damages all of them before any of them dies.
    ///
    /// Queued rather than called. A deathrattle that summons a minion that dies
    /// and has its own deathrattle is a chain the resolution queue already knows
    /// how to walk, one flat step at a time, with its own loop protection.
    /// Resolving effects by calling into effects would build a second, worse
    /// version of that with a stack instead of a queue.
    ///
    /// It applies nothing itself. Damage goes through DamageRules, draws through
    /// DrawSystem, summons through SummonRules: the same paths a turn uses, so
    /// an effect cannot reach an outcome the rules would not have allowed.
    /// </summary>
    internal sealed class ResolveEffectsAction : ResolutionAction
    {
        private readonly IReadOnlyList<EffectDefinition> _effects;
        private readonly EffectContext _context;
        private readonly List<EntityId> _targets = new List<EntityId>();

        public ResolveEffectsAction(IReadOnlyList<EffectDefinition> effects, EffectContext context)
        {
            _effects = effects;
            _context = context;
        }

        public override void Resolve(ResolutionContext context)
        {
            if (_effects == null || _context == null)
            {
                return;
            }

            for (int index = 0; index < _effects.Count; index++)
            {
                Apply(context, _effects[index]);
            }
        }

        private void Apply(ResolutionContext context, EffectDefinition effect)
        {
            // Resolved once, up front. The set an effect acts on is decided when
            // it starts, not re-asked between one victim and the next, so an
            // effect that kills its first target does not thereby change who its
            // second was going to be.
            SelectorResolver.Resolve(context.State, effect.Selector, _context, _targets);

            int amount = ResolveAmount(context, effect.Action);

            switch (effect.Action.Kind)
            {
                case EffectActionKind.DealDamage:
                    DealDamage(context, effect.Trigger, amount);
                    break;

                case EffectActionKind.DrawCards:
                    DrawCards(context, amount);
                    break;

                case EffectActionKind.Summon:
                    Summon(context, effect.Action);
                    break;

                case EffectActionKind.GainTemporaryMana:
                    GainTemporaryMana(context, amount);
                    break;

                case EffectActionKind.ModifyStats:
                    ModifyStats(context, effect.Action);
                    break;

                case EffectActionKind.GrantSpellDamage:
                    GrantSpellDamage(context, amount);
                    break;

                case EffectActionKind.RestoreMana:
                    RestoreMana(context, amount);
                    break;
            }
        }

        /// <summary>
        /// What an action's own <see cref="EffectActionDefinition.Amount"/>
        /// actually resolves to right now. Fixed is the authored number,
        /// unchanged; every other source reads a live number off the caster
        /// instead - Huntress Shot's mana restoration is the first card to
        /// use one, scaling with <see cref="Player.SpellDamageBonus"/>
        /// rather than an authored constant.
        /// </summary>
        private int ResolveAmount(ResolutionContext context, EffectActionDefinition action) =>
            action.AmountSource switch
            {
                EffectValueSource.SpellDamage =>
                    context.State.GetPlayer(_context.Controller).SpellDamageBonus,
                _ => action.Amount
            };

        /// <summary>
        /// Every target is hit inside this one action, so a sweep that finishes
        /// several minions produces one grouped death phase afterwards rather
        /// than one death at a time.
        ///
        /// Spell Damage is folded in here, once per row, rather than at
        /// DamageRules itself: <paramref name="trigger"/> tells us whether
        /// this damage is a spell's own (<see cref="EffectTrigger.OnPlay"/>
        /// is the trigger a spell's effects resolve under - see
        /// <c>EffectResolver.TriggerOnPlay</c>) as opposed to a hero power's,
        /// a battlecry's or a deathrattle's, none of which Spell Damage is
        /// allowed to touch. A card with two separate DealDamage rows - two
        /// missiles rather than one bigger hit - gets the bonus applied once
        /// per row, which is what makes a multi-hit spell scale with Spell
        /// Damage the way Hearthstone's own does.
        /// </summary>
        private void DealDamage(ResolutionContext context, EffectTrigger trigger, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (trigger == EffectTrigger.OnPlay)
            {
                amount = SpellDamageSystem.Apply(context.State.GetPlayer(_context.Controller), amount);
            }

            for (int index = 0; index < _targets.Count; index++)
            {
                DamageRules.Deal(context, _context.SourceEntityId, _targets[index], amount);
            }
        }

        /// <summary>
        /// Draws for whoever the selected characters belong to, through the
        /// ordinary draw. Which means an effect gets deck order, a full hand
        /// burning a card, fatigue and everything fatigue can lead to, for free.
        /// </summary>
        private void DrawCards(ResolutionContext context, int count)
        {
            if (count <= 0)
            {
                return;
            }

            for (int index = 0; index < _targets.Count; index++)
            {
                if (!context.State.TryGetEntity(_targets[index], out Entity entity))
                {
                    continue;
                }

                Player player = context.State.GetPlayer(entity.Controller);

                for (int card = 0; card < count; card++)
                {
                    DrawSystem.Draw(context, player);
                }
            }
        }

        private void Summon(ResolutionContext context, EffectActionDefinition action)
        {
            if (action.SummonCardId.IsNone || action.SummonCount <= 0)
            {
                return;
            }

            Player controller = context.State.GetPlayer(_context.Controller);

            for (int index = 0; index < action.SummonCount; index++)
            {
                // A full board simply takes no more. Summoning three with one
                // slot free puts one down, which is the Hearthstone answer and
                // needs no special case: the summon rules already refuse.
                if (SummonRules.Summon(context, controller, action.SummonCardId, SummonRules.Rightmost) == null)
                {
                    return;
                }
            }
        }

        private void GainTemporaryMana(ResolutionContext context, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            for (int index = 0; index < _targets.Count; index++)
            {
                if (!context.State.TryGetEntity(_targets[index], out Entity entity))
                {
                    continue;
                }

                ManaSystem.GrantTemporaryMana(context, context.State.GetPlayer(entity.Controller), amount);
            }
        }

        /// <summary>
        /// A lasting change to a minion's statistics. Heroes are skipped: they
        /// have no printed body to modify, and giving one a stat buff is a
        /// weapon or an armour effect rather than this.
        /// </summary>
        private void ModifyStats(ResolutionContext context, EffectActionDefinition action)
        {
            if (action.AttackDelta == 0 && action.HealthDelta == 0)
            {
                return;
            }

            for (int index = 0; index < _targets.Count; index++)
            {
                if (context.State.TryGetEntity(_targets[index], out Entity entity) &&
                    entity is Minion minion &&
                    minion.IsInPlay)
                {
                    minion.AddModifier(action.AttackDelta, action.HealthDelta);
                }
            }
        }

        /// <summary>
        /// A player-level grant, not an entity effect: it belongs to
        /// whoever controls this effect, not to whatever the Self selector
        /// resolved as an entity id, so it reads
        /// <see cref="EffectContext.Controller"/> directly rather than
        /// walking the resolved target list.
        /// </summary>
        private void GrantSpellDamage(ResolutionContext context, int amount) =>
            SpellDamageSystem.Grant(context, context.State.GetPlayer(_context.Controller), amount);

        /// <summary>
        /// A player-level effect, exactly like <see cref="GrantSpellDamage"/>
        /// and for the same reason: mana belongs to the controller, not to
        /// whatever the selector resolved as an entity, so this reads
        /// <see cref="EffectContext.Controller"/> directly.
        /// </summary>
        private void RestoreMana(ResolutionContext context, int amount) =>
            ManaSystem.Restore(context, context.State.GetPlayer(_context.Controller), amount);
    }
}
