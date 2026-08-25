using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Core.Effects;
using CoH.Data;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Writes the development card set, the catalog and the deck from code.
    ///
    /// Generated for the same reason the scene is: every number is written down
    /// once and the whole set can be rebuilt after a change, instead of being
    /// clicked back into shape in the inspector. A card authored by hand is
    /// perfectly valid; this only makes the demonstration set reproducible.
    ///
    /// None of these cards is special anywhere in the code. Each is a row of
    /// data, and the engine reaches every one of them through the same generic
    /// trigger, selector and action.
    /// </summary>
    public static class DevCardSetup
    {
        private const string CardFolder = "Assets/_Project/Data/Cards";
        private const string CatalogPath = "Assets/_Project/Data/Catalog/CardCatalog_Starter.asset";
        private const string DevDeckPath = "Assets/_Project/Data/Decks/Deck_Development.asset";

        [MenuItem("Conquest of Hearthstone/Rebuild Development Cards")]
        public static void Rebuild()
        {
            List<CardDefinitionAsset> cards = new List<CardDefinitionAsset>
            {
                Soldier(),
                Coin(),
                Token(),
                BattlecryDamage(),
                DeathrattleDraw(),
                Summoner(),
                Buff(),
                AreaDamage()
            };

            WriteCatalog(cards);
            WriteDevDeck();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Development cards rebuilt: " + cards.Count + " cards.");
        }

        // ------------------------------------------------------------------
        //  The cards
        // ------------------------------------------------------------------

        private static CardDefinitionAsset Soldier() =>
            Card("Card_TestSoldier", "test_soldier", "Test Soldier", CardType.Minion,
                cost: 2, attack: 2, health: 3, text: string.Empty);

        /// <summary>
        /// The Coin, functional at last, and functional only because of the row
        /// of data below. Nothing anywhere recognises its id.
        /// </summary>
        private static CardDefinitionAsset Coin() =>
            Card("Card_TheCoin", "the_coin", "The Coin", CardType.Spell,
                cost: 0, attack: 0, health: 0,
                text: "Gain 1 Mana Crystal this turn only.",
                collectible: false,
                effects: new[]
                {
                    Effect(EffectTrigger.OnPlay, SelectorKind.FriendlyHero,
                        Act(EffectActionKind.GainTemporaryMana, amount: 1))
                });

        /// <summary>A real card rather than a bodiless minion, so a token is an entity like any other.</summary>
        private static CardDefinitionAsset Token() =>
            Card("Card_TestToken", "test_token", "Test Token", CardType.Minion,
                cost: 1, attack: 1, health: 1, text: string.Empty, collectible: false);

        private static CardDefinitionAsset BattlecryDamage() =>
            Card("Card_TestBattlecryDamage", "test_battlecry_damage", "Test Sharpshooter", CardType.Minion,
                cost: 3, attack: 2, health: 2,
                text: "Battlecry: Deal 2 damage to an enemy character.",
                effects: new[]
                {
                    Effect(EffectTrigger.Battlecry, SelectorKind.ChosenTarget,
                        Act(EffectActionKind.DealDamage, amount: 2),
                        TargetFilter.EnemyCharacter)
                });

        private static CardDefinitionAsset DeathrattleDraw() =>
            Card("Card_TestDeathrattleDraw", "test_deathrattle_draw", "Test Scribe", CardType.Minion,
                cost: 2, attack: 1, health: 2,
                text: "Deathrattle: Draw a card.",
                effects: new[]
                {
                    Effect(EffectTrigger.Deathrattle, SelectorKind.FriendlyHero,
                        Act(EffectActionKind.DrawCards, amount: 1))
                });

        private static CardDefinitionAsset Summoner() =>
            Card("Card_TestSummoner", "test_summoner", "Test Summoner", CardType.Minion,
                cost: 4, attack: 2, health: 2,
                text: "Battlecry: Summon two 1/1 Test Tokens.",
                effects: new[]
                {
                    Effect(EffectTrigger.Battlecry, SelectorKind.Self,
                        Summon("test_token", count: 2))
                });

        private static CardDefinitionAsset Buff() =>
            Card("Card_TestBuff", "test_buff", "Test Quartermaster", CardType.Minion,
                cost: 2, attack: 1, health: 2,
                text: "Battlecry: Give a friendly minion +1/+1.",
                effects: new[]
                {
                    Effect(EffectTrigger.Battlecry, SelectorKind.ChosenTarget,
                        Modify(1, 1),
                        TargetFilter.FriendlyMinion)
                });

        private static CardDefinitionAsset AreaDamage() =>
            Card("Card_TestAoe", "test_aoe", "Test Volley", CardType.Spell,
                cost: 2, attack: 0, health: 0,
                text: "Deal 1 damage to all enemy minions.",
                effects: new[]
                {
                    Effect(EffectTrigger.OnPlay, SelectorKind.AllEnemyMinions,
                        Act(EffectActionKind.DealDamage, amount: 1))
                });

        // ------------------------------------------------------------------
        //  Writing
        // ------------------------------------------------------------------

        private static CardDefinitionAsset Card(
            string assetName, string id, string displayName, CardType type,
            int cost, int attack, int health, string text,
            bool collectible = true, EffectSpec[] effects = null)
        {
            string path = CardFolder + "/" + assetName + ".asset";
            CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CardDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serialized = new SerializedObject(asset);

            serialized.FindProperty("cardId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("cardType").enumValueIndex = (int)type;
            serialized.FindProperty("collectible").boolValue = collectible;
            serialized.FindProperty("manaCost").intValue = cost;
            serialized.FindProperty("attack").intValue = attack;
            serialized.FindProperty("health").intValue = health;
            serialized.FindProperty("rulesText").stringValue = text;

            SerializedProperty list = serialized.FindProperty("effects");
            list.ClearArray();

            if (effects != null)
            {
                for (int index = 0; index < effects.Length; index++)
                {
                    list.InsertArrayElementAtIndex(index);
                    Write(list.GetArrayElementAtIndex(index), effects[index]);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            return asset;
        }

        private static void Write(SerializedProperty element, EffectSpec spec)
        {
            element.FindPropertyRelative("trigger").enumValueIndex = (int)spec.Trigger;
            element.FindPropertyRelative("selector").enumValueIndex = (int)spec.Selector;
            element.FindPropertyRelative("targetFilter").enumValueIndex = (int)spec.Filter;
            element.FindPropertyRelative("action").enumValueIndex = (int)spec.Action;
            element.FindPropertyRelative("amount").intValue = spec.Amount;
            element.FindPropertyRelative("attackDelta").intValue = spec.AttackDelta;
            element.FindPropertyRelative("healthDelta").intValue = spec.HealthDelta;
            element.FindPropertyRelative("summonCardId").stringValue = spec.SummonCardId;
            element.FindPropertyRelative("summonCount").intValue = spec.SummonCount;
            element.FindPropertyRelative("placement").enumValueIndex = (int)SummonPlacement.Rightmost;
        }

        private static void WriteCatalog(List<CardDefinitionAsset> cards)
        {
            CardCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<CardCatalogAsset>(CatalogPath);

            if (catalog == null)
            {
                Debug.LogError("DevCardSetup: no catalog at " + CatalogPath);
                return;
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("cards");

            list.ClearArray();

            for (int index = 0; index < cards.Count; index++)
            {
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = cards[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>
        /// A deck holding every demonstration card, so a match can reach all of
        /// them without a deck builder existing yet.
        /// </summary>
        private static void WriteDevDeck()
        {
            DeckListAsset deck = AssetDatabase.LoadAssetAtPath<DeckListAsset>(DevDeckPath);

            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<DeckListAsset>();
                AssetDatabase.CreateAsset(deck, DevDeckPath);
            }

            (string Asset, int Count)[] contents =
            {
                ("Card_TestSoldier", 8),
                ("Card_TestBattlecryDamage", 6),
                ("Card_TestDeathrattleDraw", 6),
                ("Card_TestSummoner", 4),
                ("Card_TestBuff", 4),
                ("Card_TestAoe", 2)
            };

            SerializedObject serialized = new SerializedObject(deck);
            SerializedProperty entries = serialized.FindProperty("entries");

            entries.ClearArray();

            for (int index = 0; index < contents.Length; index++)
            {
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);

                entry.FindPropertyRelative("card").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(
                        CardFolder + "/" + contents[index].Asset + ".asset");

                entry.FindPropertyRelative("count").intValue = contents[index].Count;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(deck);
        }

        // ------------------------------------------------------------------

        private static EffectSpec Effect(
            EffectTrigger trigger, SelectorKind selector, EffectSpec action,
            TargetFilter filter = TargetFilter.AnyCharacter)
        {
            action.Trigger = trigger;
            action.Selector = selector;
            action.Filter = filter;
            return action;
        }

        private static EffectSpec Act(EffectActionKind kind, int amount) =>
            new EffectSpec { Action = kind, Amount = amount, SummonCount = 1, SummonCardId = string.Empty };

        private static EffectSpec Modify(int attackDelta, int healthDelta) =>
            new EffectSpec
            {
                Action = EffectActionKind.ModifyStats,
                AttackDelta = attackDelta,
                HealthDelta = healthDelta,
                SummonCount = 1,
                SummonCardId = string.Empty
            };

        private static EffectSpec Summon(string cardId, int count) =>
            new EffectSpec
            {
                Action = EffectActionKind.Summon,
                SummonCardId = cardId,
                SummonCount = count
            };

        /// <summary>One authored effect, as plain values, before it is written out.</summary>
        private struct EffectSpec
        {
            public EffectTrigger Trigger;
            public SelectorKind Selector;
            public TargetFilter Filter;
            public EffectActionKind Action;
            public int Amount;
            public int AttackDelta;
            public int HealthDelta;
            public string SummonCardId;
            public int SummonCount;
        }
    }
}
