using System;
using System.Globalization;
using System.Text;
using CoH.Core.Identifiers;
using CoH.Core.State;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// A canonical description of a match, and a hash of it.
    ///
    /// The description is the real product; the hash is only a short way to
    /// compare two of them. When a replay diverges, what you want is not "the
    /// hashes differ" but the two descriptions side by side, which is why this
    /// is built as text first and hashed second.
    ///
    /// Everything is walked in a written-down order: players by seat, zones in
    /// a fixed sequence, and every zone by its own index, which is already part
    /// of the game state. Nothing is read out of a dictionary, nothing uses
    /// GetHashCode, and nothing depends on the order two objects happen to sit
    /// in memory.
    ///
    /// It is a diagnostic and nothing else. No rule reads it, and the engine
    /// does not know it exists.
    /// </summary>
    public static class StateFingerprint
    {
        /// <summary>Format marker, so an old dump is recognisable as one.</summary>
        public const string Version = "state-v1";

        /// <summary>The exhaustive canonical form. Two identical matches produce identical text.</summary>
        public static string Describe(GameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            StringBuilder text = new StringBuilder();

            text.Append(Version).Append('\n');
            text.Append("seed=").Append(state.Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("phase=").Append(state.Phase).Append('\n');
            text.Append("result=").Append(state.Result).Append('\n');
            text.Append("turn=").Append(Number(state.TurnNumber)).Append('\n');
            text.Append("current=").Append(Seat(state.CurrentPlayer)).Append('\n');
            text.Append("starting=").Append(Seat(state.StartingPlayer)).Append('\n');
            text.Append("entities=").Append(Number(state.EntityCount)).Append('\n');
            text.Append("draws=").Append(state.RandomSource.DrawCount.ToString(CultureInfo.InvariantCulture)).Append('\n');

            // Always by seat, never by turn order: the description of a match
            // must not change shape depending on who happens to be acting.
            DescribePlayer(text, state.GetPlayer(PlayerId.One));
            DescribePlayer(text, state.GetPlayer(PlayerId.Two));

            return text.ToString();
        }

        public static string Of(GameState state) => StableHash.Hex(Describe(state));

        private static void DescribePlayer(StringBuilder text, Player player)
        {
            text.Append("player ").Append(Seat(player.Id)).Append('\n');

            Hero hero = player.Hero;

            text.Append("  hero=").Append(Id(hero.Id))
                .Append(" hp=").Append(Number(hero.CurrentHealth))
                .Append('/').Append(Number(hero.MaxHealth))
                .Append(" damage=").Append(Number(hero.Damage))
                .Append(" armor=").Append(Number(hero.Armor))
                .Append(" attack=").Append(Number(hero.Attack))
                .Append(" attacks=").Append(Number(hero.AttacksThisTurn))
                .Append('/').Append(Number(hero.MaxAttacksPerTurn))
                .Append(" doomed=").Append(Flag(hero.IsMarkedForDestruction))
                .Append(" died=").Append(Flag(hero.HasDied))
                .Append(" ts=").Append(Number(hero.Timestamp))
                .Append('\n');

            text.Append("  mana=").Append(Number(player.AvailableMana))
                .Append('/').Append(Number(player.MaxMana))
                .Append(" temp=").Append(Number(player.TemporaryMana))
                .Append(" overload=").Append(Number(player.OverloadLocked))
                .Append('+').Append(Number(player.OverloadOwed))
                .Append('\n');

            text.Append("  fatigue=").Append(Number(player.FatigueCounter))
                .Append(" turns=").Append(Number(player.TurnsTaken))
                .Append(" heropower=").Append(Flag(player.HasUsedHeroPowerThisTurn))
                .Append(" mulliganed=").Append(Flag(player.HasConfirmedMulligan))
                .Append('\n');

            text.Append("  mulliganpick=");
            for (int index = 0; index < player.MulliganSelection.Count; index++)
            {
                text.Append(Id(player.MulliganSelection[index])).Append(' ');
            }

            text.Append('\n');

            DescribeCards(text, "deck", player.Deck);
            DescribeCards(text, "hand", player.Hand);
            DescribeBoard(text, player.Board);
            DescribeGraveyard(text, player.Graveyard);
        }

        private static void DescribeCards(StringBuilder text, string label, Zone<CardInstance> zone)
        {
            text.Append("  ").Append(label).Append('=').Append(Number(zone.Count)).Append('\n');

            for (int index = 0; index < zone.Count; index++)
            {
                CardInstance card = zone[index];

                text.Append("    [").Append(Number(index)).Append("] ")
                    .Append(Id(card.Id))
                    .Append(' ').Append(card.CardId.Value)
                    .Append(" zone=").Append(card.Zone)
                    .Append(" cost").Append(Signed(card.CostModifier))
                    .Append(" atk").Append(Signed(card.AttackModifier))
                    .Append(" hp").Append(Signed(card.HealthModifier))
                    .Append(" owner=").Append(Seat(card.Owner))
                    .Append(" controller=").Append(Seat(card.Controller))
                    .Append('\n');
            }
        }

        private static void DescribeBoard(StringBuilder text, Zone<Minion> board)
        {
            text.Append("  board=").Append(Number(board.Count)).Append('\n');

            for (int index = 0; index < board.Count; index++)
            {
                Minion minion = board[index];

                text.Append("    [").Append(Number(index)).Append("] ")
                    .Append(Id(minion.Id))
                    .Append(' ').Append(minion.CardId.Value)
                    .Append(' ').Append(Number(minion.Attack))
                    .Append('/').Append(Number(minion.CurrentHealth))
                    .Append(" max=").Append(Number(minion.MaxHealth))
                    .Append(" damage=").Append(Number(minion.Damage))
                    .Append(" base=").Append(Number(minion.BaseAttack))
                    .Append('/').Append(Number(minion.BaseHealth))
                    .Append(" mod=").Append(Signed(minion.AttackModifier))
                    .Append('/').Append(Signed(minion.HealthModifier))
                    .Append(" attacks=").Append(Number(minion.AttacksThisTurn))
                    .Append('/').Append(Number(minion.MaxAttacksPerTurn))
                    .Append(" summoned=").Append(Number(minion.SummonedOnTurn))
                    .Append(" zone=").Append(minion.Zone)
                    .Append(" doomed=").Append(Flag(minion.IsMarkedForDestruction))
                    .Append(" owner=").Append(Seat(minion.Owner))
                    .Append(" controller=").Append(Seat(minion.Controller))
                    .Append(" ts=").Append(Number(minion.Timestamp));

                // Written out one by one rather than as a total, because
                // two minions holding the same totals through different
                // buffs are two different positions the moment anything
                // removes one of them.
                text.Append(" mods=").Append(Number(minion.Modifiers.Count));

                for (int mod = 0; mod < minion.Modifiers.Count; mod++)
                {
                    StatModifier modifier = minion.Modifiers[mod];

                    text.Append(' ').Append(Number(modifier.Order))
                        .Append(':').Append(Signed(modifier.AttackDelta))
                        .Append('/').Append(Signed(modifier.HealthDelta))
                        .Append(':').Append(modifier.Source);
                }

                text.Append('\n');
            }
        }

        private static void DescribeGraveyard(StringBuilder text, Zone<Entity> graveyard)
        {
            text.Append("  graveyard=").Append(Number(graveyard.Count)).Append('\n');

            for (int index = 0; index < graveyard.Count; index++)
            {
                Entity entity = graveyard[index];

                text.Append("    [").Append(Number(index)).Append("] ")
                    .Append(Id(entity.Id))
                    .Append(' ').Append(CardIdOf(entity))
                    .Append(' ').Append(entity.GetType().Name)
                    .Append(" owner=").Append(Seat(entity.Owner))
                    .Append(" ts=").Append(Number(entity.Timestamp))
                    .Append('\n');
            }
        }

        private static string CardIdOf(Entity entity) => entity switch
        {
            Minion minion => minion.CardId.Value,
            CardInstance card => card.CardId.Value,
            _ => "-"
        };

        private static string Seat(PlayerId id) => id.IsNone ? "none" : id.Index.ToString(CultureInfo.InvariantCulture);

        private static string Id(EntityId id) => id.IsNone ? "-" : "#" + id.Value.ToString(CultureInfo.InvariantCulture);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Signed(int value) =>
            (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);

        private static string Flag(bool value) => value ? "1" : "0";
    }
}
