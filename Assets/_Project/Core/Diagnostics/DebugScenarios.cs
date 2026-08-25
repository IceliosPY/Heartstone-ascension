using System;
using System.Collections.Generic;
using CoH.Core.Identifiers;

namespace CoH.Core.Diagnostics
{
    /// <summary>
    /// The prepared positions worth starting from.
    ///
    /// Every one of them exists because reaching it by playing takes several
    /// minutes and a certain amount of luck. They are built only from what the
    /// current cards can already be: a two for three that has taken a point of
    /// damage is a two for two, which is how a mutual kill is set up without
    /// inventing a card to set it up with.
    ///
    /// None of them adds a rule, and none of them can express a position the
    /// engine could not have reached on its own.
    /// </summary>
    public static class DebugScenarios
    {
        public const string TestSoldier = "test_soldier";
        public const string TheCoin = "the_coin";
        public const string TestToken = "test_token";
        public const string TestBattlecryDamage = "test_battlecry_damage";
        public const string TestDeathrattleDraw = "test_deathrattle_draw";
        public const string TestSummoner = "test_summoner";
        public const string TestBuff = "test_buff";
        public const string TestAoe = "test_aoe";

        public const string ReadyCombatId = "ready_combat";
        public const string BothSurviveId = "both_survive";
        public const string DoubleDeathId = "double_death";
        public const string HeroLethalId = "hero_lethal";
        public const string FullHandId = "full_hand";
        public const string FatigueId = "fatigue";
        public const string SevenMinionBoardId = "seven_minion_board";

        public const string CoinId = "coin";
        public const string BattlecryTargetId = "battlecry_target";
        public const string DeathrattleId = "deathrattle";
        public const string SummonId = "summon";
        public const string BuffId = "buff";
        public const string AoeId = "aoe";

        private static readonly DebugScenario[] Catalogue =
        {
            ReadyCombat,
            BothSurvive,
            DoubleDeath,
            HeroLethal,
            FullHand,
            Fatigue,
            SevenMinionBoard,
            Coin,
            BattlecryTarget,
            Deathrattle,
            Summon,
            Buff,
            AreaDamage
        };

        /// <summary>Everything on offer, in a fixed order.</summary>
        public static IReadOnlyList<DebugScenario> All => Catalogue;

        public static bool TryFind(string id, out DebugScenario scenario)
        {
            for (int index = 0; index < Catalogue.Length; index++)
            {
                if (string.Equals(Catalogue[index].Id, id, StringComparison.Ordinal))
                {
                    scenario = Catalogue[index];
                    return true;
                }
            }

            scenario = null;
            return false;
        }

        public static DebugScenario Find(string id)
        {
            if (!TryFind(id, out DebugScenario scenario))
            {
                throw new ArgumentException("There is no debug scenario called '" + id + "'.", nameof(id));
            }

            return scenario;
        }

        // ------------------------------------------------------------------

        /// <summary>A minion each, the acting player's free to swing.</summary>
        public static DebugScenario ReadyCombat => new DebugScenario(
            ReadyCombatId,
            "One Test Soldier each. Player one is active and free to attack.",
            one: Side(board: new[] { Soldier() }),
            two: Side(board: new[] { Soldier() }),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>
        /// Two full health soldiers. Two attack against three health, so the
        /// exchange hurts both and kills neither.
        /// </summary>
        public static DebugScenario BothSurvive => new DebugScenario(
            BothSurviveId,
            "Two undamaged Test Soldiers. Attacking trades damage and kills nothing.",
            one: Side(board: new[] { Soldier() }),
            two: Side(board: new[] { Soldier() }),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>
        /// Both soldiers already down to two health, so two attack finishes
        /// each of them and the death phase has to handle a pair.
        /// </summary>
        public static DebugScenario DoubleDeath => new DebugScenario(
            DoubleDeathId,
            "Two Test Soldiers on two health. Attacking kills both at once.",
            one: Side(board: new[] { Soldier(damage: 1) }),
            two: Side(board: new[] { Soldier(damage: 1) }),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>One swing from over.</summary>
        public static DebugScenario HeroLethal => new DebugScenario(
            HeroLethalId,
            "Player two is on two health with an empty board. One attack ends the match.",
            one: Side(board: new[] { Soldier() }),
            two: Side(heroHealth: 2),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>A hand at its cap, with cards still to draw.</summary>
        public static DebugScenario FullHand => new DebugScenario(
            FullHandId,
            "Player one holds ten cards with a deck left to draw from, so the next draw burns.",
            one: Side(hand: Repeat(TestSoldier, 10), deck: Repeat(TestSoldier, 5)),
            two: Side(deck: Repeat(TestSoldier, 5)),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>Nothing left to draw.</summary>
        public static DebugScenario Fatigue => new DebugScenario(
            FatigueId,
            "Player one has an empty deck. Ending the turn twice takes fatigue damage.",
            one: Side(deck: Array.Empty<string>(), fatigueCounter: 0),
            two: Side(deck: Repeat(TestSoldier, 5)),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>A row with no space left in it.</summary>
        public static DebugScenario SevenMinionBoard => new DebugScenario(
            SevenMinionBoardId,
            "Player one has a full board, so nothing else can be played onto it.",
            one: Side(board: Soldiers(7), hand: Repeat(TestSoldier, 3), deck: Repeat(TestSoldier, 5)),
            two: Side(deck: Repeat(TestSoldier, 5)),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        // ------------------------------------------------------------------
        //  Effects
        // ------------------------------------------------------------------

        /// <summary>
        /// Two crystals and a Coin. Playing it has to reach three spendable
        /// mana while leaving the crystals owned at two.
        /// </summary>
        public static DebugScenario Coin => new DebugScenario(
            CoinId,
            "Player one holds The Coin on two crystals. Playing it gives a third for this turn only.",
            one: Side(maxMana: 2, availableMana: 2, hand: new[] { TheCoin, TestSoldier, TestSoldier }),
            two: Side(),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>A targeted battlecry with something worth aiming it at.</summary>
        public static DebugScenario BattlecryTarget => new DebugScenario(
            BattlecryTargetId,
            "Player one holds a targeted battlecry. Player two has a minion and a hero to aim it at.",
            one: Side(hand: new[] { TestBattlecryDamage, TestBattlecryDamage }),
            two: Side(board: new[] { Soldier() }),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>
        /// A minion whose death draws a card, standing in front of something
        /// that will finish it in one exchange.
        /// </summary>
        public static DebugScenario Deathrattle => new DebugScenario(
            DeathrattleId,
            "Player one has a minion that draws when it dies, and an attack that will kill it.",
            one: Side(board: new[] { Minion(TestDeathrattleDraw) }),
            two: Side(board: new[] { Soldier() }),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>A battlecry that puts two more bodies down beside it.</summary>
        public static DebugScenario Summon => new DebugScenario(
            SummonId,
            "Player one holds a minion whose battlecry summons two tokens.",
            one: Side(hand: new[] { TestSummoner, TestSummoner }, board: new[] { Soldier() }),
            two: Side(),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>A targeted buff and a friendly minion to put it on.</summary>
        public static DebugScenario Buff => new DebugScenario(
            BuffId,
            "Player one holds a buff and has a minion to give it to.",
            one: Side(hand: new[] { TestBuff, TestBuff }, board: new[] { Soldier() }),
            two: Side(),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        /// <summary>
        /// A sweep and three minions on one health, so one damage finishes all
        /// three and the death phase has to handle them together.
        /// </summary>
        public static DebugScenario AreaDamage => new DebugScenario(
            AoeId,
            "Player one holds a sweep. Player two has three minions on one health, so all three die at once.",
            one: Side(hand: new[] { TestAoe, TestAoe }),
            two: Side(board: new[] { Soldier(damage: 2), Soldier(damage: 2), Soldier(damage: 2) }),
            turnNumber: 5,
            activePlayer: PlayerId.One);

        // ------------------------------------------------------------------

        private static ScenarioPlayer Side(
            int heroHealth = 30,
            int armor = 0,
            int maxMana = 10,
            int availableMana = 10,
            int fatigueCounter = 0,
            IReadOnlyList<string> hand = null,
            IReadOnlyList<string> deck = null,
            IReadOnlyList<ScenarioMinion> board = null) =>
            new ScenarioPlayer(
                heroHealth, armor, maxMana, availableMana, fatigueCounter,
                hand ?? Repeat(TestSoldier, 2),
                deck ?? Repeat(TestSoldier, 10),
                board ?? Array.Empty<ScenarioMinion>());

        /// <summary>Summoned two turns ago, so it is never summoning sick.</summary>
        private static ScenarioMinion Soldier(int damage = 0, int attacksThisTurn = 0) =>
            new ScenarioMinion(TestSoldier, damage, attacksThisTurn, summonedOnTurn: 3);

        /// <summary>Any other minion, also old enough to act.</summary>
        private static ScenarioMinion Minion(string cardId, int damage = 0) =>
            new ScenarioMinion(cardId, damage, attacksThisTurn: 0, summonedOnTurn: 3);

        private static ScenarioMinion[] Soldiers(int count)
        {
            ScenarioMinion[] minions = new ScenarioMinion[count];

            for (int index = 0; index < count; index++)
            {
                minions[index] = Soldier();
            }

            return minions;
        }

        private static string[] Repeat(string cardId, int count)
        {
            string[] cards = new string[count];

            for (int index = 0; index < count; index++)
            {
                cards[index] = cardId;
            }

            return cards;
        }
    }
}
