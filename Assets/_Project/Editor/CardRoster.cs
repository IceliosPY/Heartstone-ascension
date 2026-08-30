using System;
using System.Collections.Generic;
using CoH.Data;
using UnityEditor;

namespace CoH.Editor
{
    /// <summary>
    /// Every real card the project knows about.
    ///
    /// Found once, in a single pass over the AssetDatabase, and kept until
    /// told otherwise. A picker meant to reach a thousand cards cannot be the
    /// thing that scans the project on every repaint, so the scan and the
    /// picker are two different pieces of code, and this is the one that pays
    /// the cost - once, not per frame.
    /// </summary>
    public static class CardRoster
    {
        private static CardDefinitionAsset[] _cached;

        /// <summary>Everything found, kept until <see cref="Invalidate"/> is called.</summary>
        public static IReadOnlyList<CardDefinitionAsset> All()
        {
            _cached ??= Load();
            return _cached;
        }

        /// <summary>Forgets what was found. The next call to <see cref="All"/> scans again.</summary>
        public static void Invalidate() => _cached = null;

        /// <summary>
        /// Every real card whose name contains the search, case-insensitively.
        /// A blank search finds everything.
        /// </summary>
        public static List<CardDefinitionAsset> Search(string search)
        {
            IReadOnlyList<CardDefinitionAsset> all = All();
            List<CardDefinitionAsset> found = new List<CardDefinitionAsset>(all.Count);

            for (int index = 0; index < all.Count; index++)
            {
                CardDefinitionAsset candidate = all[index];

                if (candidate == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(search) ||
                    candidate.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found.Add(candidate);
                }
            }

            return found;
        }

        private static CardDefinitionAsset[] Load()
        {
            string[] guids = AssetDatabase.FindAssets("t:CardDefinitionAsset");
            List<CardDefinitionAsset> found = new List<CardDefinitionAsset>(guids.Length);

            for (int index = 0; index < guids.Length; index++)
            {
                CardDefinitionAsset asset = AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));

                if (asset != null)
                {
                    found.Add(asset);
                }
            }

            found.Sort((left, right) =>
                string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

            return found.ToArray();
        }
    }
}
