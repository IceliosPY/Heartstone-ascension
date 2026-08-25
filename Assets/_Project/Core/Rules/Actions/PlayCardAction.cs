using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules.Effects;
using CoH.Core.Rules.Resolution;
using CoH.Core.State;

namespace CoH.Core.Rules.Actions
{
    /// <summary>
    /// Plays a card from a hand.
    ///
    /// Resolution order, which is part of the engine's contract because the
    /// presentation replays it: pay, then the card leaves the hand, then
    /// whatever the card does.
    ///
    /// The checks are repeated here rather than trusted from validation. Today
    /// nothing can change between the two, but effects that make a player play
    /// a card will queue this action directly, and by then the board may have
    /// filled up or the card may be gone.
    /// </summary>
    internal sealed class PlayCardAction : ResolutionAction
    {
        private readonly PlayerId _playerId;
        private readonly EntityId _cardInstanceId;
        private readonly int _boardPosition;
        private readonly EntityId _targetId;

        public PlayCardAction(PlayerId playerId, EntityId cardInstanceId, int boardPosition, EntityId targetId)
        {
            _playerId = playerId;
            _cardInstanceId = cardInstanceId;
            _boardPosition = boardPosition;
            _targetId = targetId;
        }

        public override void Resolve(ResolutionContext context)
        {
            GameState state = context.State;
            Player player = state.GetPlayer(_playerId);

            CardInstance card = FindInHand(player, _cardInstanceId);
            if (card == null)
            {
                return;
            }

            if (!state.Catalog.TryGet(card.CardId, out CardDefinition definition))
            {
                return;
            }

            bool isMinion = definition.Type == CardType.Minion;

            if (!isMinion && definition.Type != CardType.Spell)
            {
                return;
            }

            if (isMinion && player.Board.IsFull)
            {
                return;
            }

            int cost = ManaSystem.GetPlayCost(state, card);
            if (!ManaSystem.CanPay(player, cost))
            {
                return;
            }

            ManaSystem.Pay(context, player, cost);

            player.Hand.Remove(card);
            card.Zone = ZoneType.Graveyard;
            player.Graveyard.TryAdd(card);

            context.Emit(new CardPlayedEvent(player.Id, card.Id, card.CardId, _targetId));

            if (!isMinion)
            {
                // A spell is nothing but its effects, so playing it and
                // resolving it are the same moment. It is already in the
                // graveyard, exactly as in Hearthstone, before it does anything.
                EffectResolver.TriggerOnPlay(context, definition, card, player.Id, _targetId);
                return;
            }

            Minion minion = SummonRules.Summon(context, player, card.CardId, _boardPosition);

            if (minion == null)
            {
                return;
            }

            // Queued after the minion is already standing on the board, which is
            // where Hearthstone resolves a battlecry from. It is why a battlecry
            // that damages every minion damages its own, and why one that counts
            // your minions counts itself.
            EffectResolver.TriggerBattlecry(
                context, definition, minion, card.Id, _targetId, player.Board.IndexOf(minion));
        }

        private static CardInstance FindInHand(Player player, EntityId cardInstanceId)
        {
            for (int index = 0; index < player.Hand.Count; index++)
            {
                if (player.Hand[index].Id == cardInstanceId)
                {
                    return player.Hand[index];
                }
            }

            return null;
        }
    }
}
