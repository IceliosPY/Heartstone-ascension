using System.Collections.Generic;
using CoH.Data;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Checks every authored catalog and deck in the project and prints what is
    /// wrong.
    ///
    /// Deliberately a menu item and a console report, not a window: the point is
    /// to catch a bad number before a match is started, and a list of clear
    /// sentences does that better than any inspector could.
    /// </summary>
    public static class CardDataValidator
    {
        private const string MenuPath = "Conquest of Hearthstone/Validate Card Data";

        [MenuItem(MenuPath)]
        public static void ValidateAll()
        {
            List<string> problems = new List<string>();

            int catalogs = ValidateAllOfType<CardCatalogAsset>(problems, (asset, list) => asset.Validate(list));
            int decks = ValidateAllOfType<DeckListAsset>(problems, (asset, list) => asset.Validate(list));

            if (problems.Count == 0)
            {
                Debug.Log($"Card data is valid: {catalogs} catalog(s) and {decks} deck(s) checked.");
                return;
            }

            Debug.LogError(
                $"Card data has {problems.Count} problem(s) across {catalogs} catalog(s) and {decks} deck(s):\n"
                + "  - " + string.Join("\n  - ", problems));
        }

        private static int ValidateAllOfType<T>(List<string> problems, System.Action<T, List<string>> validate)
            where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset != null)
                {
                    validate(asset, problems);
                }
            }

            return guids.Length;
        }
    }
}
