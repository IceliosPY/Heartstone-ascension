using CoH.Core.Cards;
using CoH.Core.Commands;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Rules
{
    /// <summary>
    /// Whether a hero power may be used, and what it costs.
    ///
    /// One place, asked by both the command validator and the presentation, so
    /// a greyed-out button and a refused command can never disagree about why.
    ///
    /// Every check here runs before anything is spent. That ordering is the
    /// point rather than an optimisation: a player whose board is full must be
    /// refused while they still have their mana, not charged and then told the
    /// summon had nowhere to go.
    /// </summary>
    internal static class HeroPowerRules
    {
        /// <summary>
        /// Whether this player could use their hero power right now, ignoring
        /// which option they might pick.
        ///
        /// Split from the option check because the two are asked at different
        /// moments: this one decides whether the menu opens at all.
        /// </summary>
        public static RejectionReason CanActivate(
            GameState state, PlayerId playerId, out CardDefinition definition)
        {
            definition = null;

            if (state.Phase != GamePhase.Playing)
            {
                return RejectionReason.WrongPhase;
            }

            if (playerId.IsNone)
            {
                return RejectionReason.UnknownPlayer;
            }

            if (playerId != state.CurrentPlayer)
            {
                return RejectionReason.NotYourTurn;
            }

            Player player = state.GetPlayer(playerId);

            if (!player.Hero.HasHeroPower)
            {
                return RejectionReason.NoHeroPower;
            }

            if (!state.Catalog.TryGet(player.Hero.HeroPowerCardId, out definition))
            {
                return RejectionReason.NoHeroPower;
            }

            if (definition.Type != CardType.HeroPower)
            {
                return RejectionReason.NoHeroPower;
            }

            if (player.HasUsedHeroPowerThisTurn)
            {
                return RejectionReason.HeroPowerAlreadyUsed;
            }

            if (player.AvailableMana < definition.ManaCost)
            {
                return RejectionReason.NotEnoughMana;
            }

            // Asked here rather than discovered during resolution. A hero power
            // whose only option needs a board slot must not take a player's
            // mana and then quietly do nothing.
            if (NeedsBoardSpace(definition) && player.Board.IsFull)
            {
                return RejectionReason.BoardFull;
            }

            return RejectionReason.None;
        }

        /// <summary>The whole check, including the option that was chosen.</summary>
        public static RejectionReason Validate(
            GameState state, PlayerId playerId, int optionIndex, out CardDefinition definition)
        {
            RejectionReason reason = CanActivate(state, playerId, out definition);

            if (reason != RejectionReason.None)
            {
                return reason;
            }

            return HeroPowerOptions.IsValidOption(definition, optionIndex)
                ? RejectionReason.None
                : RejectionReason.InvalidHeroPowerOption;
        }

        /// <summary>
        /// Whether every option this power offers would need a free board slot.
        ///
        /// Every, not any: a power offering a summon and a spell is still
        /// usable on a full board, because the player can pick the spell. Only
        /// a power that can do nothing but summon is unusable, and that is the
        /// Necromancer case. Nothing here knows that.
        /// </summary>
        private static bool NeedsBoardSpace(CardDefinition definition)
        {
            System.Collections.Generic.IReadOnlyList<EffectDefinition> options =
                HeroPowerOptions.Of(definition);

            if (options.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].Action.Kind != EffectActionKind.Summon)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
