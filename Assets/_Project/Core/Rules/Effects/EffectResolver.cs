using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Actions;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Effects
{
    /// <summary>
    /// The one place a trigger becomes work.
    ///
    /// Everything that fires an effect, playing a card, a minion dying, comes
    /// through here, so the engine never grows the giant switch over every
    /// card in the game that this whole design exists to avoid. What a card does
    /// is data; this only decides when to queue it.
    ///
    /// It queues and returns. Nothing resolves inline, so a deathrattle that
    /// summons a minion that dies and has a deathrattle of its own is walked by
    /// the resolution queue one flat step at a time, under the loop protection
    /// that is already there.
    /// </summary>
    internal static class EffectResolver
    {
        /// <summary>
        /// Queues a card's effects for one trigger, if it has any.
        ///
        /// The context is built now rather than looked up later, which is what
        /// lets a deathrattle resolve long after its minion has left the board.
        /// </summary>
        public static void Trigger(
            ResolutionContext context,
            CardDefinition definition,
            EffectTrigger trigger,
            EffectContext effectContext)
        {
            if (definition == null || effectContext == null)
            {
                return;
            }

            IReadOnlyList<EffectDefinition> effects =
                EffectQueries.WithTrigger(definition.Effects, trigger);

            if (effects.Count == 0)
            {
                return;
            }

            context.Enqueue(new ResolveEffectsAction(effects, effectContext));
        }

        /// <summary>Queues a battlecry for a minion that has just arrived.</summary>
        public static void TriggerBattlecry(
            ResolutionContext context,
            CardDefinition definition,
            Minion minion,
            EntityId cardInstanceId,
            EntityId chosenTargetId,
            int boardPosition)
        {
            Trigger(context, definition, EffectTrigger.Battlecry, new EffectContext(
                minion.Id, cardInstanceId, minion.CardId,
                minion.Owner, minion.Controller, chosenTargetId, boardPosition));
        }

        /// <summary>Queues a spell's effects. The card itself is the source.</summary>
        public static void TriggerOnPlay(
            ResolutionContext context,
            CardDefinition definition,
            CardInstance card,
            PlayerId controller,
            EntityId chosenTargetId)
        {
            Trigger(context, definition, EffectTrigger.OnPlay, new EffectContext(
                card.Id, card.Id, card.CardId, card.Owner, controller, chosenTargetId));
        }

        /// <summary>
        /// Queues a deathrattle for a minion a death phase has just removed.
        ///
        /// Everything it will need is captured here, while the minion is still
        /// describable: it is off the board by now, and by the time this
        /// resolves the board will have moved on further still.
        /// </summary>
        public static void TriggerDeathrattle(
            ResolutionContext context, GameState state, Minion minion, int boardPosition)
        {
            if (!state.Catalog.TryGet(minion.CardId, out CardDefinition definition))
            {
                return;
            }

            Trigger(context, definition, EffectTrigger.Deathrattle, new EffectContext(
                minion.Id, EntityId.None, minion.CardId,
                minion.Owner, minion.Controller, EntityId.None, boardPosition));
        }
    }
}
