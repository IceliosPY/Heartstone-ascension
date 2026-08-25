using System.Collections.Generic;
using CoH.Core.Cards;
using CoH.Presentation.CardVisuals;
using UnityEngine;

namespace CoH.Tests.VisualEditMode
{
    /// <summary>
    /// Recipes, catalogs and sprites built in memory.
    ///
    /// Nothing here loads a project asset. A test that used the real catalog
    /// would pass or fail depending on what somebody last authored, and would
    /// stop being a test of the composer and start being a test of the art
    /// folder. These are small, complete and stated in full where they are used.
    /// </summary>
    internal static class VisualTestFactory
    {
        /// <summary>A one pixel sprite, distinguishable from every other one.</summary>
        public static Sprite Picture(string name)
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = name };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            sprite.name = name;
            return sprite;
        }

        public static CardVisualLayerDefinition Picture(
            string name,
            CardVisualSlot slot,
            int order,
            bool required = false,
            CardVisualFace face = CardVisualFace.FaceUp,
            params CardVisualCondition[] conditions) =>
            new CardVisualLayerDefinition
            {
                name = name,
                slot = slot,
                text = CardVisualTextSlot.None,
                face = face,
                sortingOrder = order,
                x = 0f,
                y = 0f,
                width = 100f,
                height = 100f,
                required = required,
                tint = Color.white,
                conditions = conditions ?? System.Array.Empty<CardVisualCondition>()
            };

        public static CardVisualLayerDefinition Label(
            string name,
            CardVisualTextSlot slot,
            int order,
            params CardVisualCondition[] conditions) =>
            new CardVisualLayerDefinition
            {
                name = name,
                slot = CardVisualSlot.None,
                text = slot,
                face = CardVisualFace.FaceUp,
                sortingOrder = order,
                x = 0f,
                y = 0f,
                width = 100f,
                height = 100f,
                fontSize = 3f,
                tint = Color.white,
                conditions = conditions ?? System.Array.Empty<CardVisualCondition>()
            };

        public static CardVisualRecipeAsset Recipe(params CardVisualLayerDefinition[] layers)
        {
            CardVisualRecipeAsset recipe = ScriptableObject.CreateInstance<CardVisualRecipeAsset>();
            recipe.name = "TestRecipe";
            recipe.Author(CardVisualStyle.Default, new List<CardVisualLayerDefinition>(layers));
            return recipe;
        }

        public static CardVisualCatalogAsset Catalog(params CardVisualEntry[] entries)
        {
            CardVisualCatalogAsset catalog = ScriptableObject.CreateInstance<CardVisualCatalogAsset>();
            catalog.name = "TestCatalog";
            catalog.ClearEntries();

            for (int index = 0; index < entries.Length; index++)
            {
                catalog.AddEntry(entries[index]);
            }

            return catalog;
        }

        public static CardVisualEntry Entry(
            CardVisualSlot slot,
            Sprite sprite,
            CardType? type = null,
            CardClass? cardClass = null,
            Rarity? rarity = null,
            CardVisualStyle style = default) =>
            new CardVisualEntry
            {
                slot = slot,
                sprite = sprite,
                match = new CardVisualMatch
                {
                    constrainType = type.HasValue,
                    type = type ?? CardType.None,
                    constrainClass = cardClass.HasValue,
                    cardClass = cardClass ?? CardClass.Neutral,
                    constrainRarity = rarity.HasValue,
                    rarity = rarity ?? Rarity.Free,
                    constrainTribe = false,
                    style = style
                }
            };

        /// <summary>A plain two mana 2/3 neutral minion, unless told otherwise.</summary>
        public static CardVisualDescriptor Card(
            CardType type = CardType.Minion,
            CardClass cardClass = CardClass.Neutral,
            Rarity rarity = Rarity.Common,
            Tribe tribe = Tribe.None,
            Sprite artwork = null,
            string name = "Test Soldier",
            string rules = "",
            int cost = 2,
            int attack = 2,
            int health = 3,
            bool faceDown = false) =>
            new CardVisualDescriptor(
                type,
                cardClass,
                rarity,
                tribe,
                artwork,
                name,
                rules,
                cost,
                attack,
                health,
                showsCost: true,
                showsStatistics: type == CardType.Minion || type == CardType.Weapon,
                faceDown: faceDown);

    }
}
