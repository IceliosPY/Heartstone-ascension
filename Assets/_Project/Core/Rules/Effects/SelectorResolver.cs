using System.Collections.Generic;
using CoH.Core.Effects;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Rules.Effects
{
    /// <summary>
    /// Turns a selector into the characters it means, right now.
    ///
    /// The order is written down rather than incidental: the controller's side
    /// first and then the opponent's, and within a side the hero before the
    /// board, left to right. Nothing is read out of a dictionary and nothing is
    /// sorted afterwards, so an effect that touches several minions touches them
    /// in an order that is the same on every machine and in every replay.
    ///
    /// It answers with ids rather than objects, because the caller holds the
    /// list across a whole action and an object it held could be off the board
    /// by the time it got to it.
    /// </summary>
    internal static class SelectorResolver
    {
        /// <summary>
        /// Collects the characters a selector means into <paramref name="destination"/>,
        /// which is cleared first.
        /// </summary>
        public static void Resolve(
            GameState state, SelectorDefinition selector, EffectContext effect, List<EntityId> destination)
        {
            destination.Clear();

            if (selector == null || effect == null)
            {
                return;
            }

            Player friendly = state.GetPlayer(effect.Controller);
            Player enemy = state.GetPlayer(effect.Opponent);

            switch (selector.Kind)
            {
                case SelectorKind.Self:
                    Add(destination, effect.SourceEntityId);
                    break;

                case SelectorKind.ChosenTarget:
                    // Whatever the player pointed at, if anything. A card whose
                    // target has gone simply reaches nobody.
                    Add(destination, effect.ChosenTargetId);
                    break;

                case SelectorKind.FriendlyHero:
                    Add(destination, friendly.Hero.Id);
                    break;

                case SelectorKind.EnemyHero:
                    Add(destination, enemy.Hero.Id);
                    break;

                case SelectorKind.AllFriendlyMinions:
                    AddBoard(destination, friendly);
                    break;

                case SelectorKind.AllEnemyMinions:
                    AddBoard(destination, enemy);
                    break;

                case SelectorKind.AllMinions:
                    AddBoard(destination, friendly);
                    AddBoard(destination, enemy);
                    break;

                case SelectorKind.AllCharacters:
                    Add(destination, friendly.Hero.Id);
                    AddBoard(destination, friendly);
                    Add(destination, enemy.Hero.Id);
                    AddBoard(destination, enemy);
                    break;
            }
        }

        /// <summary>
        /// Everything a player could legally point at for this selector.
        ///
        /// The same walk in the same order, so the highlighted list a player
        /// sees is the list the engine will check the answer against.
        /// </summary>
        public static void CollectLegalTargets(
            GameState state, SelectorDefinition selector, PlayerId controller, List<EntityId> destination)
        {
            destination.Clear();

            if (selector == null || !selector.NeedsChosenTarget || controller.IsNone)
            {
                return;
            }

            Player friendly = state.GetPlayer(controller);
            Player enemy = state.GetPlayer(controller.Opponent);

            bool wantsFriendly =
                selector.Filter == TargetFilter.AnyCharacter ||
                selector.Filter == TargetFilter.AnyMinion ||
                selector.Filter == TargetFilter.FriendlyCharacter ||
                selector.Filter == TargetFilter.FriendlyMinion;

            bool wantsEnemy =
                selector.Filter == TargetFilter.AnyCharacter ||
                selector.Filter == TargetFilter.AnyMinion ||
                selector.Filter == TargetFilter.EnemyCharacter ||
                selector.Filter == TargetFilter.EnemyMinion;

            bool wantsHeroes =
                selector.Filter == TargetFilter.AnyCharacter ||
                selector.Filter == TargetFilter.FriendlyCharacter ||
                selector.Filter == TargetFilter.EnemyCharacter;

            if (wantsFriendly)
            {
                if (wantsHeroes)
                {
                    Add(destination, friendly.Hero.Id);
                }

                AddBoard(destination, friendly);
            }

            if (!wantsEnemy)
            {
                return;
            }

            if (wantsHeroes)
            {
                Add(destination, enemy.Hero.Id);
            }

            AddBoard(destination, enemy);
        }

        private static void AddBoard(List<EntityId> destination, Player player)
        {
            for (int slot = 0; slot < player.Board.Count; slot++)
            {
                destination.Add(player.Board[slot].Id);
            }
        }

        private static void Add(List<EntityId> destination, EntityId id)
        {
            if (!id.IsNone)
            {
                destination.Add(id);
            }
        }
    }
}
