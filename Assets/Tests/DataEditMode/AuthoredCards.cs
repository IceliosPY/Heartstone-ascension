using CoH.Data;
using NUnit.Framework;
using UnityEditor;

namespace CoH.Tests.DataEditMode
{
    /// <summary>
    /// Where the authored assets live, in one place, so a moved file breaks one
    /// line rather than a dozen tests.
    /// </summary>
    internal static class AuthoredCards
    {
        public const string TestSoldierPath = "Assets/_Project/Data/Cards/Card_TestSoldier.asset";
        public const string TheCoinPath = "Assets/_Project/Data/Cards/Card_TheCoin.asset";
        public const string CatalogPath = "Assets/_Project/Data/Catalog/CardCatalog_Starter.asset";
        public const string DeckPath = "Assets/_Project/Data/Decks/Deck_TestSoldier.asset";

        public static CardDefinitionAsset TestSoldier() => Load<CardDefinitionAsset>(TestSoldierPath);

        public static CardDefinitionAsset TheCoin() => Load<CardDefinitionAsset>(TheCoinPath);

        public static CardCatalogAsset Catalog() => Load<CardCatalogAsset>(CatalogPath);

        public static DeckListAsset Deck() => Load<DeckListAsset>(DeckPath);

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Missing authored asset at " + path);
            return asset;
        }
    }
}
