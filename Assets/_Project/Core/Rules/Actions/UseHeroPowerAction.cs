using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Uses a hero power: pays for it, spends it for the turn, and queues the
    /// one option that was chosen.
    ///
    /// The commitment and the consequence are separated on purpose. This action
    /// does the part that is certain - the cost, the once-per-turn mark and the
    /// event saying it happened - and queues the chosen effect behind it, so
    /// that whatever the option does travels the same road as every other
    /// effect in the game. A hero power that summons goes through SummonRules
    /// exactly as a battlecry that summons does, and cannot reach an outcome
    /// the ordinary rules would have refused.
    ///
    /// Nothing here knows what a Necromancer is, or that there are four of
    /// anything. It reads an index into a list of options the card carries.
    /// </summary>
    internal sealed class UseHeroPowerAction : ResolutionAction
    {
        private readonly PlayerId _playerId;
        private readonly int _optionIndex;

        public UseHeroPowerAction(PlayerId playerId, int optionIndex)
        {
            _playerId = playerId;
            _optionIndex = optionIndex;
        }

        public override void Resolve(ResolutionContext context)
        {
            GameState state = context.State;

            // Rechecked rather than trusted from validation, exactly as an
            // attack is: an effect could one day queue this directly, and by
            // then the board may have changed.
            if (HeroPowerRules.Validate(state, _playerId, _optionIndex, out CardDefinition definition)
                != RejectionReason.None)
            {
                return;
            }

            Player player = state.GetPlayer(_playerId);

            ManaSystem.Pay(context, player, definition.ManaCost);

            // Spent for the turn whatever the option then achieves. A hero
            // power is used by being used.
            player.HasUsedHeroPowerThisTurn = true;

            context.Emit(new HeroPowerUsedEvent(
                _playerId, definition.Id, _optionIndex, player.AvailableMana));

            EffectDefinition option = HeroPowerOptions.Option(definition, _optionIndex);

            if (option == null)
            {
                return;
            }

            // The hero is the source. That is what makes a summoned minion
            // belong to this player, and what damage from a hero power would be
            // attributed to.
            EffectContext effectContext = new EffectContext(
                player.Hero.Id,
                EntityId.None,
                definition.Id,
                _playerId,
                _playerId);

            context.Enqueue(new ResolveEffectsAction(
                new List<EffectDefinition> { option }, effectContext));
        }
    }
}
