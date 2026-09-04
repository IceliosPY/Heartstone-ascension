using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Core.Commands;
using CoH.Core.Events;
using CoH.Core.Identifiers;
using CoH.Core.Rules;
using CoH.Core.Rules.Actions;
using CoH.Core.Setup;
using CoH.Core.State;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// The smallest set of helpers the domain tests actually need today.
    ///
    /// Deliberately not a full scenario DSL: it grows only when it removes real
    /// noise from real tests. Right now that means building a catalog, a deck
    /// list, and a match already taken through setup and mulligan.
    /// </summary>
    internal static class TestFactory
    {
        /// <summary>The vanilla 2 mana 2/3 the whole project is bootstrapped on.</summary>
        public const string MinionCardId = "test_soldier";

        public const string SpellCardId = "test_spell";

        /// <summary>Id of the extra card the second player receives, as configured by default.</summary>
        public static CardId CoinCardId => GameConfig.DefaultSecondPlayerExtraCard;

        public static CardDefinition MinionDefinition(
            string id = MinionCardId,
            string name = "Test Soldier",
            int manaCost = 2,
            int attack = 2,
            int health = 3) =>
            new CardDefinition(new CardId(id), name, CardType.Minion, manaCost, attack, health);

        public static CardDefinition SpellDefinition(
            string id = SpellCardId,
            string name = "Test Spell",
            int manaCost = 1) =>
            new CardDefinition(new CardId(id), name, CardType.Spell, manaCost);

        /// <summary>The extra card given to the player going second. Never collectible.</summary>
        /// <summary>
        /// The Coin, carrying the effect that makes it work. Nothing recognises
        /// its id; this row of data is the whole of it.
        /// </summary>
        public static CardDefinition CoinDefinition() =>
            new CardDefinition(
                CoinCardId, "The Coin", CardType.Spell, 0, collectible: false,
                text: "Gain 1 Mana Crystal this turn only.",
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.OnPlay,
                        new SelectorDefinition(SelectorKind.FriendlyHero),
                        new EffectActionDefinition(EffectActionKind.GainTemporaryMana, 1))
                });

        /// <summary>
        /// A catalog holding the standard test cards, plus everything the debug
        /// scenarios name.
        ///
        /// The scenarios are part of the engine's own toolbox and describe
        /// positions built from real cards, so the catalog a test hands them has
        /// to contain those cards. Each is defined here rather than loaded from
        /// an asset, because a Core test may never depend on a ScriptableObject.
        /// </summary>
        public static CardCatalog Catalog(params CardDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                definitions = StandardCards();
            }

            return new CardCatalog(definitions);
        }

        /// <summary>Everything a scenario or a default test match may name.</summary>
        public static CardDefinition[] StandardCards() => new[]
        {
            MinionDefinition(),
            SpellDefinition(),
            CoinDefinition(),
            TokenDefinition(),
            BattlecryDamageDefinition(),
            DeathrattleDrawDefinition(),
            SummonerDefinition(),
            BuffDefinition(),
            AreaDamageDefinition(),
            TargetedSpellDefinition(),
            WeaponDefinition(),
            SkeletalWarriorDefinition(),
            SkeletalRogueDefinition(),
            CryptFiendDefinition(),
            AbominationDefinition(),
            ChooseYourWeaponsDefinition(),
            LunarPhaseDefinition(),
            HeroPowerDamageDefinition(),
            HuntressShotDefinition()
        };

        // ------------------------------------------------------------------
        //  Necromancer
        //
        //  Written out here rather than loaded from the authored assets,
        //  because a Core test may never depend on a ScriptableObject. A
        //  separate data test proves the assets say the same thing, which is
        //  the only place the two can be compared honestly.
        // ------------------------------------------------------------------

        public const string SkeletalWarriorCardId = "necromancer_skeletal_warrior";
        public const string SkeletalRogueCardId = "necromancer_skeletal_rogue";
        public const string CryptFiendCardId = "necromancer_crypt_fiend";
        public const string AbominationCardId = "necromancer_abomination";
        public const string ChooseYourWeaponsCardId = "necromancer_choose_your_weapons";

        /// <summary>The four servants, in the order the hero power offers them.</summary>
        public static readonly string[] ServantCardIds =
        {
            SkeletalWarriorCardId,
            SkeletalRogueCardId,
            CryptFiendCardId,
            AbominationCardId
        };

        public static CardDefinition SkeletalWarriorDefinition() =>
            new CardDefinition(
                new CardId(SkeletalWarriorCardId), "Skeletal Warrior", CardType.Minion,
                manaCost: 1, attack: 1, health: 1, collectible: false,
                cardClass: CardClass.Necromancer, text: "Rush",
                keywords: CardKeywords.Rush);

        public static CardDefinition SkeletalRogueDefinition() =>
            new CardDefinition(
                new CardId(SkeletalRogueCardId), "Skeletal Rogue", CardType.Minion,
                manaCost: 1, attack: 0, health: 1, collectible: false,
                cardClass: CardClass.Necromancer, text: "Camouflage",
                keywords: CardKeywords.Stealth);

        public static CardDefinition CryptFiendDefinition() =>
            new CardDefinition(
                new CardId(CryptFiendCardId), "Crypt Fiend", CardType.Minion,
                manaCost: 1, attack: 1, health: 2, collectible: false,
                cardClass: CardClass.Necromancer);

        public static CardDefinition AbominationDefinition() =>
            new CardDefinition(
                new CardId(AbominationCardId), "Abomination", CardType.Minion,
                manaCost: 1, attack: 0, health: 2, collectible: false,
                cardClass: CardClass.Necromancer, text: "Provocation",
                keywords: CardKeywords.Taunt);

        /// <summary>
        /// The hero power, whose four options are four rows of data. Nothing
        /// about "four" is written anywhere but here.
        /// </summary>
        public static CardDefinition ChooseYourWeaponsDefinition()
        {
            EffectDefinition[] options = new EffectDefinition[ServantCardIds.Length];

            for (int index = 0; index < ServantCardIds.Length; index++)
            {
                options[index] = new EffectDefinition(
                    EffectTrigger.HeroPower,
                    new SelectorDefinition(SelectorKind.Self),
                    new EffectActionDefinition(
                        EffectActionKind.Summon,
                        summonCardId: new CardId(ServantCardIds[index]),
                        summonCount: 1));
            }

            return new CardDefinition(
                new CardId(ChooseYourWeaponsCardId), "Raise", CardType.HeroPower,
                manaCost: 1, collectible: false, cardClass: CardClass.Necromancer,
                text: "Choose a minion to summon.", effects: options);
        }

        /// <summary>A configuration where seat one is a Necromancer and seat two is not.</summary>
        public static GameConfig NecromancerConfig() =>
            GameConfig.Default.WithHeroPowers(new CardId(ChooseYourWeaponsCardId), default);

        /// <summary>
        /// A started match in which player one has the Necromancer hero power,
        /// it is their turn, and they can afford to use it.
        /// </summary>
        public static GameEngine NecromancerMatch(ulong seed = 1UL, int mana = 10)
        {
            GameEngine engine = StartedMatch(seed, config: NecromancerConfig());

            // Seed one starts on some seeds and not others; the tests want the
            // Necromancer holding the turn, not a particular shuffle.
            if (engine.State.CurrentPlayer != PlayerId.One)
            {
                EndTurn(engine);
            }

            GiveMana(engine, PlayerId.One, mana);
            return engine;
        }

        /// <summary>Uses the active player's hero power, choosing one option by index.</summary>
        public static CommandResult UseHeroPower(GameEngine engine, int optionIndex) =>
            engine.Execute(new UseHeroPowerCommand(engine.State.CurrentPlayer, optionIndex));

        // ------------------------------------------------------------------
        //  Starcaller
        //
        //  Written out here for the same reason as the Necromancer's own
        //  cards above: a Core test may never depend on a ScriptableObject.
        //  The development match seats Starcaller on seat two specifically
        //  (see MatchBootstrap.DefaultDevelopmentHeroPowerSeatTwo), which
        //  DevelopmentConfig/DevelopmentMatch below mirror; StarcallerConfig/
        //  StarcallerMatch put it on seat two as well, for tests that only
        //  care about Lunar Phase in isolation but still want the mechanism
        //  exercised on the seat it is actually configured on.
        // ------------------------------------------------------------------

        public const string LunarPhaseCardId = "starcaller_lunar_phase";

        /// <summary>
        /// The hero power: a single option, granting Spell Damage rather
        /// than summoning. Nothing about "one option" is special-cased
        /// anywhere the option list is read - see
        /// <see cref="StarcallerHeroPowerTests"/>.
        /// </summary>
        public static CardDefinition LunarPhaseDefinition() =>
            new CardDefinition(
                new CardId(LunarPhaseCardId), "Lunar Phase", CardType.HeroPower,
                manaCost: 2, collectible: false, cardClass: CardClass.Starcaller,
                text: "Spell Damage +1 this turn.",
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.HeroPower,
                        new SelectorDefinition(SelectorKind.Self),
                        new EffectActionDefinition(EffectActionKind.GrantSpellDamage, amount: 1))
                });

        /// <summary>
        /// A hero power that deals damage directly - not part of any real
        /// class today, only here to prove Spell Damage does not leak into
        /// hero power damage merely because both are "damage" (see
        /// <see cref="EffectTrigger.HeroPower"/> vs <see cref="EffectTrigger.OnPlay"/>).
        /// </summary>
        public static CardDefinition HeroPowerDamageDefinition(int amount = 2) =>
            new CardDefinition(
                new CardId("test_hero_power_damage"), "Test Hero Power Damage", CardType.HeroPower,
                manaCost: 1, collectible: false,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.HeroPower,
                        new SelectorDefinition(SelectorKind.EnemyHero),
                        new EffectActionDefinition(EffectActionKind.DealDamage, amount))
                });

        public const string HuntressShotCardId = "starcaller_huntress_shot";

        /// <summary>
        /// Starcaller's first collectible spell: deal 1 to a chosen minion,
        /// then restore mana equal to the caster's current Spell Damage.
        /// Two OnPlay rows rather than one, because the two numbers answer
        /// different questions and are computed differently - the first
        /// grows with Spell Damage through <see cref="ResolveEffectsAction.DealDamage"/>'s
        /// own existing rule, the second reads Spell Damage directly as its
        /// own amount through <see cref="EffectValueSource.SpellDamage"/>,
        /// and neither derives from the other or from the damage actually
        /// dealt.
        /// </summary>
        public static CardDefinition HuntressShotDefinition() =>
            new CardDefinition(
                new CardId(HuntressShotCardId), "Huntress Shot", CardType.Spell,
                manaCost: 3, collectible: true, cardClass: CardClass.Starcaller,
                text: "Deal 1 damage to a minion.\nRestore 1 Mana for each Spell Damage you have.",
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.OnPlay,
                        new SelectorDefinition(SelectorKind.ChosenTarget, TargetFilter.AnyMinion),
                        new EffectActionDefinition(EffectActionKind.DealDamage, amount: 1)),
                    new EffectDefinition(
                        EffectTrigger.OnPlay,
                        new SelectorDefinition(SelectorKind.Self),
                        new EffectActionDefinition(
                            EffectActionKind.RestoreMana, amountSource: EffectValueSource.SpellDamage))
                });

        /// <summary>A configuration where seat two is a Starcaller and seat one is not.</summary>
        public static GameConfig StarcallerConfig() =>
            GameConfig.Default.WithHeroPowers(default, new CardId(LunarPhaseCardId));

        /// <summary>
        /// The real development match configuration: seat one Necromancer's
        /// Raise, seat two Starcaller's Lunar Phase - exactly
        /// <c>MatchBootstrap</c>'s own defaults for Match.unity, reproduced
        /// here so a Core test can prove the pairing without touching Unity.
        /// </summary>
        public static GameConfig DevelopmentConfig() =>
            GameConfig.Default.WithHeroPowers(
                new CardId(ChooseYourWeaponsCardId), new CardId(LunarPhaseCardId));

        /// <summary>
        /// A started match in which player two has the Starcaller hero
        /// power, it is their turn, and they can afford to use it.
        /// </summary>
        public static GameEngine StarcallerMatch(ulong seed = 1UL, int mana = 10)
        {
            GameEngine engine = StartedMatch(seed, config: StarcallerConfig());

            if (engine.State.CurrentPlayer != PlayerId.Two)
            {
                EndTurn(engine);
            }

            GiveMana(engine, PlayerId.Two, mana);
            return engine;
        }

        /// <summary>The real development match: player one is the active Necromancer, both seats fully mana'd.</summary>
        public static GameEngine DevelopmentMatch(ulong seed = 1UL, int mana = 10)
        {
            GameEngine engine = StartedMatch(seed, config: DevelopmentConfig());

            if (engine.State.CurrentPlayer != PlayerId.One)
            {
                EndTurn(engine);
            }

            GiveMana(engine, PlayerId.One, mana);
            GiveMana(engine, PlayerId.Two, mana);
            return engine;
        }

        /// <summary>
        /// A spell that must be aimed at a minion. With no minion in play there
        /// is nowhere to point it, which is the case a spell and a minion answer
        /// differently.
        /// </summary>
        public static CardDefinition TargetedSpellDefinition(int amount = 3) =>
            new CardDefinition(
                new CardId("test_targeted_spell"), "Test Bolt", CardType.Spell,
                manaCost: 2,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.OnPlay,
                        new SelectorDefinition(SelectorKind.ChosenTarget, TargetFilter.AnyMinion),
                        new EffectActionDefinition(EffectActionKind.DealDamage, amount))
                });

        /// <summary>A card type the rules have no support for yet.</summary>
        public static CardDefinition WeaponDefinition() =>
            new CardDefinition(
                new CardId("test_weapon"), "Test Blade", CardType.Weapon,
                manaCost: 2, attack: 2, health: 2);

        public static CardDefinition TokenDefinition() =>
            new CardDefinition(
                new CardId("test_token"), "Test Token", CardType.Minion,
                manaCost: 1, attack: 1, health: 1, collectible: false);

        /// <summary>Battlecry: deal two damage to a chosen enemy character.</summary>
        public static CardDefinition BattlecryDamageDefinition(int amount = 2) =>
            new CardDefinition(
                new CardId("test_battlecry_damage"), "Test Sharpshooter", CardType.Minion,
                manaCost: 3, attack: 2, health: 2,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.ChosenTarget, TargetFilter.EnemyCharacter),
                        new EffectActionDefinition(EffectActionKind.DealDamage, amount))
                });

        /// <summary>Deathrattle: draw a card.</summary>
        public static CardDefinition DeathrattleDrawDefinition(int count = 1) =>
            new CardDefinition(
                new CardId("test_deathrattle_draw"), "Test Scribe", CardType.Minion,
                manaCost: 2, attack: 1, health: 2,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Deathrattle,
                        new SelectorDefinition(SelectorKind.FriendlyHero),
                        new EffectActionDefinition(EffectActionKind.DrawCards, count))
                });

        /// <summary>Battlecry: summon two tokens.</summary>
        public static CardDefinition SummonerDefinition(int count = 2) =>
            new CardDefinition(
                new CardId("test_summoner"), "Test Summoner", CardType.Minion,
                manaCost: 4, attack: 2, health: 2,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.Self),
                        new EffectActionDefinition(
                            EffectActionKind.Summon,
                            summonCardId: new CardId("test_token"),
                            summonCount: count))
                });

        /// <summary>Battlecry: give a chosen friendly minion plus one, plus one.</summary>
        public static CardDefinition BuffDefinition() =>
            new CardDefinition(
                new CardId("test_buff"), "Test Quartermaster", CardType.Minion,
                manaCost: 2, attack: 1, health: 2,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.Battlecry,
                        new SelectorDefinition(SelectorKind.ChosenTarget, TargetFilter.FriendlyMinion),
                        new EffectActionDefinition(
                            EffectActionKind.ModifyStats, attackDelta: 1, healthDelta: 1))
                });

        /// <summary>A spell that deals one damage to every enemy minion.</summary>
        public static CardDefinition AreaDamageDefinition(int amount = 1) =>
            new CardDefinition(
                new CardId("test_aoe"), "Test Volley", CardType.Spell,
                manaCost: 2,
                effects: new[]
                {
                    new EffectDefinition(
                        EffectTrigger.OnPlay,
                        new SelectorDefinition(SelectorKind.AllEnemyMinions),
                        new EffectActionDefinition(EffectActionKind.DealDamage, amount))
                });

        /// <summary>A freshly constructed match state: two heroes, four empty zones each.</summary>
        public static GameState Game(ulong seed = 1UL, params CardDefinition[] definitions) =>
            new GameState(GameConfig.Default, Catalog(definitions), seed);

        public static DeckList Deck(int count = 30, string cardId = MinionCardId)
        {
            List<CardId> cards = new List<CardId>(count);
            for (int index = 0; index < count; index++)
            {
                cards.Add(new CardId(cardId));
            }

            return new DeckList(cards);
        }

        public static GameEngine Engine(ulong seed = 1UL, GameConfig config = null, ICardCatalog catalog = null) =>
            new GameEngine(config ?? GameConfig.Default, catalog ?? Catalog(), seed);

        /// <summary>An engine sitting in the mulligan phase, hands already dealt.</summary>
        public static GameEngine MatchInMulligan(
            ulong seed = 1UL,
            int deckSize = 30,
            GameConfig config = null,
            ICardCatalog catalog = null)
        {
            GameEngine engine = Engine(seed, config, catalog);
            engine.StartMatch(Deck(deckSize), Deck(deckSize));
            return engine;
        }

        /// <summary>
        /// An engine in the playing phase, both players having kept their whole
        /// opening hand. The first turn has already been started.
        /// </summary>
        public static GameEngine StartedMatch(
            ulong seed = 1UL,
            int deckSize = 30,
            GameConfig config = null,
            ICardCatalog catalog = null)
        {
            GameEngine engine = MatchInMulligan(seed, deckSize, config, catalog);
            engine.Execute(new MulliganCommand(PlayerId.One));
            engine.Execute(new MulliganCommand(PlayerId.Two));
            return engine;
        }

        /// <summary>Removes every card from a deck, so the next draw hits fatigue.</summary>
        public static void EmptyDeck(Player player)
        {
            while (player.Deck.Count > 0)
            {
                player.Deck.RemoveAt(0);
            }
        }

        /// <summary>Moves cards from deck to hand until the hand holds the requested number.</summary>
        public static void FillHandFromDeck(Player player, int targetHandSize)
        {
            while (player.Hand.Count < targetHandSize && player.Deck.Count > 0)
            {
                CardInstance card = player.Deck.RemoveAt(0);
                card.Zone = ZoneType.Hand;
                player.Hand.TryAdd(card);
            }
        }

        /// <summary>Ends the current turn, asserting nothing; callers check the result.</summary>
        public static CommandResult EndTurn(GameEngine engine) =>
            engine.Execute(new EndTurnCommand(engine.State.CurrentPlayer));

        /// <summary>
        /// Puts a minion straight onto a board, bypassing the summoning rules
        /// that do not exist yet. Stamps it with a play order like a real
        /// summon would, since death ordering depends on that stamp.
        /// </summary>
        /// <param name="ready">
        /// When true the minion is treated as having been in play since before
        /// this turn, so it is not summoning sick and can attack straight away.
        /// </param>
        /// <summary>
        /// Puts a named card's minion on the board, statistics and effects and
        /// all. The overload below is for a plain body with chosen numbers.
        /// </summary>
        public static Minion PutMinionOnBoard(
            GameEngine engine,
            PlayerId controller,
            string cardId,
            int position = -1,
            bool ready = false)
        {
            GameState state = engine.State;
            Minion minion = state.CreateMinion(new CardId(cardId), controller);

            minion.Zone = ZoneType.Play;
            minion.Timestamp = state.NextTimestamp();
            minion.SummonedOnTurn = ready ? 0 : state.TurnNumber;

            Player player = state.GetPlayer(controller);

            if (position < 0)
            {
                player.Board.TryAdd(minion);
            }
            else
            {
                player.Board.TryInsert(position, minion);
            }

            return minion;
        }

        public static Minion PutMinionOnBoard(
            GameEngine engine,
            PlayerId controller,
            int attack = 2,
            int health = 3,
            int position = -1,
            bool ready = false)
        {
            GameState state = engine.State;
            Minion minion = state.CreateMinion(new CardId(MinionCardId), controller);

            minion.BaseAttack = attack;
            minion.BaseHealth = health;
            minion.Zone = ZoneType.Play;
            minion.Timestamp = state.NextTimestamp();
            minion.SummonedOnTurn = ready ? 0 : state.TurnNumber;

            Player player = state.GetPlayer(controller);
            if (position < 0)
            {
                player.Board.TryAdd(minion);
            }
            else
            {
                player.Board.TryInsert(position, minion);
            }

            return minion;
        }

        /// <summary>Runs damage against one target through the pipeline.</summary>
        public static IReadOnlyList<GameEvent> Damage(GameEngine engine, EntityId target, int amount) =>
            engine.Resolve(new DealDamageAction(EntityId.None, target, amount));

        /// <summary>Destroys targets outright, all in the same death phase.</summary>
        public static IReadOnlyList<GameEvent> Destroy(GameEngine engine, params EntityId[] targets) =>
            engine.Resolve(new DestroyAction(targets));

        /// <summary>Damages several targets inside a single action, so they die together.</summary>
        public static IReadOnlyList<GameEvent> DamageTogether(
            GameEngine engine,
            params (EntityId Target, int Amount)[] hits) =>
            engine.Resolve(SimultaneousDamageAction.Against(hits));

        /// <summary>Gives a player crystals and fills their pool, so cards can be afforded.</summary>
        public static void GiveMana(GameEngine engine, PlayerId playerId, int amount)
        {
            Player player = engine.State.GetPlayer(playerId);
            player.MaxMana = amount;
            player.AvailableMana = amount;
        }

        /// <summary>Puts a fresh card straight into a hand, bypassing the deck.</summary>
        public static CardInstance PutCardInHand(GameEngine engine, PlayerId playerId, string cardId = MinionCardId)
        {
            CardInstance card = engine.State.CreateCardInstance(new CardId(cardId), playerId);
            card.Zone = ZoneType.Hand;
            engine.State.GetPlayer(playerId).Hand.TryAdd(card);
            return card;
        }

        /// <summary>Plays a card from the active player's hand.</summary>
        public static CommandResult PlayCard(
            GameEngine engine,
            EntityId cardInstanceId,
            int boardPosition = PlayCardCommand.Rightmost) =>
            engine.Execute(new PlayCardCommand(engine.State.CurrentPlayer, cardInstanceId, boardPosition));

        /// <summary>
        /// The active player, with the mana and the card in hand needed to play
        /// one Test Soldier. Returns the card that is ready to be played.
        /// </summary>
        public static CardInstance ReadyToPlay(GameEngine engine, int mana = 10)
        {
            PlayerId active = engine.State.CurrentPlayer;
            GiveMana(engine, active, mana);
            return PutCardInHand(engine, active);
        }

        /// <summary>Attacks with a minion belonging to the active player.</summary>
        public static CommandResult Attack(GameEngine engine, EntityId attackerId, EntityId targetId) =>
            engine.Execute(new AttackCommand(engine.State.CurrentPlayer, attackerId, targetId));

        /// <summary>
        /// Ends the current turn and keeps going until it is the given player's
        /// turn again. Always advances at least one turn, even when it is
        /// already that player's turn, which is what "their next turn" means.
        /// </summary>
        public static void AdvanceToNextTurnOf(GameEngine engine, PlayerId player)
        {
            if (engine.State.HasEnded)
            {
                return;
            }

            EndTurn(engine);

            int guard = 0;
            while (engine.State.CurrentPlayer != player && !engine.State.HasEnded && guard++ < 10)
            {
                EndTurn(engine);
            }
        }

        /// <summary>The enemy hero of the player currently holding the turn.</summary>
        public static Hero EnemyHero(GameEngine engine) =>
            engine.State.GetPlayer(engine.State.CurrentPlayer.Opponent).Hero;
    }

    /// <summary>
    /// A plain reference type for zone tests, used instead of string so that
    /// interned literals cannot make two logically distinct items compare as
    /// the same reference.
    /// </summary>
    internal sealed class TestItem
    {
        public TestItem(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString() => Name;
    }
}
