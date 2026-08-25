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

            switch (effect.Action.Kind)
            {
                case EffectActionKind.DealDamage:
                    DealDamage(context, effect.Action.Amount);
                    break;

                case EffectActionKind.DrawCards:
                    DrawCards(context, effect.Action.Amount);
                    break;

                case EffectActionKind.Summon:
                    Summon(context, effect.Action);
                    break;

                case EffectActionKind.GainTemporaryMana:
                    GainTemporaryMana(context, effect.Action.Amount);
                    break;

                case EffectActionKind.ModifyStats:
                    ModifyStats(context, effect.Action);
                    break;
            }
        }

        /// <summary>
        /// Every target is hit inside this one action, so a sweep that finishes
        /// several minions produces one grouped death phase afterwards rather
        /// than one death at a time.
        /// </summary>
        private void DealDamage(ResolutionContext context, int amount)
        {
            if (amount <= 0)
            {
                return;
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
    }
}
