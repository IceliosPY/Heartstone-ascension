using System;
using System.Globalization;
using System.Text;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// The match written out for a person to read.
    ///
    /// Deliberately separate from <see cref="StateFingerprint"/>. That one is
    /// exhaustive because a comparison has to be; this one leaves out anything
    /// that is almost always zero, so what remains can be pasted into a bug
    /// report and understood at a glance. Making one text serve both purposes
    /// would mean either an unreadable report or a fingerprint that misses
    /// things.
    ///
    /// The order is the same in both, so a line here can always be found there.
    /// </summary>
    public static class StateDump
    {
        public static string Readable(GameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            StringBuilder text = new StringBuilder();

            text.Append("Game\n");
            text.Append("  Phase:  ").Append(state.Phase).Append('\n');
            text.Append("  Turn:   ").Append(Number(state.TurnNumber)).Append('\n');
            text.Append("  Active: ").Append(Name(state.CurrentPlayer)).Append('\n');
            text.Append("  Seed:   ").Append(state.Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');

            if (state.HasEnded)
            {
                text.Append("  Result: ").Append(state.Result).Append('\n');
            }

            text.Append('\n');

            Describe(text, state.GetPlayer(PlayerId.One));
            text.Append('\n');
            Describe(text, state.GetPlayer(PlayerId.Two));

            text.Append("\nEntities: ").Append(Number(state.EntityCount)).Append('\n');
            text.Append("State:    ").Append(StateFingerprint.Of(state)).Append('\n');

            return text.ToString();
        }

        private static void Describe(StringBuilder text, Player player)
        {
            Hero hero = player.Hero;

            text.Append(Name(player.Id)).Append('\n');

            text.Append("  Hero: ").Append(Number(hero.CurrentHealth)).Append(" hp");

            if (hero.Armor > 0)
            {
                text.Append(" + ").Append(Number(hero.Armor)).Append(" armor");
            }

            text.Append('\n');

            text.Append("  Mana: ").Append(Number(player.AvailableMana))
                .Append('/').Append(Number(player.MaxMana));

            if (player.TemporaryMana > 0)
            {
                text.Append(" (+").Append(Number(player.TemporaryMana)).Append(" temporary)");
            }

            text.Append('\n');

            text.Append("  Deck: ").Append(Number(player.Deck.Count)).Append(" cards");

            if (player.FatigueCounter > 0)
            {
                text.Append(", fatigue ").Append(Number(player.FatigueCounter));
            }

            text.Append('\n');

            text.Append("  Hand:\n");

            for (int index = 0; index < player.Hand.Count; index++)
            {
                CardInstance card = player.Hand[index];

                text.Append("    [").Append(Number(index)).Append("] #")
                    .Append(Number(card.Id.Value)).Append(' ').Append(card.CardId.Value);

                if (card.CostModifier != 0)
                {
                    text.Append(" cost").Append(Signed(card.CostModifier));
                }

                text.Append('\n');
            }

            text.Append("  Board:\n");

            for (int index = 0; index < player.Board.Count; index++)
            {
                Minion minion = player.Board[index];

                text.Append("    [").Append(Number(index)).Append("] #")
                    .Append(Number(minion.Id.Value)).Append(' ').Append(minion.CardId.Value)
                    .Append(' ').Append(Number(minion.Attack))
                    .Append('/').Append(Number(minion.CurrentHealth));

                if (minion.Damage > 0)
                {
                    text.Append(" damage=").Append(Number(minion.Damage));
                }

                if (minion.AttacksThisTurn > 0)
                {
                    text.Append(" attacked=").Append(Number(minion.AttacksThisTurn));
                }

                text.Append(" summoned=").Append(Number(minion.SummonedOnTurn));
                text.Append('\n');
            }

            if (player.Graveyard.Count > 0)
            {
                text.Append("  Graveyard: ").Append(Number(player.Graveyard.Count)).Append('\n');
            }
        }

        private static string Name(PlayerId id) => id.IsNone ? "-" : "P" + Number(id.Number);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Signed(int value) =>
            (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);
    }
}
