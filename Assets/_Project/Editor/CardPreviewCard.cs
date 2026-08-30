using System;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// The card prefab an editor tool needs is not there.
    ///
    /// Its own type rather than a plain exception so a window can catch exactly
    /// this and show the reason in its own frame, instead of either swallowing
    /// everything or letting an unrelated bug pass for a missing prefab.
    /// </summary>
    public sealed class MissingCardPrefabException : Exception
    {
        public MissingCardPrefabException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// The card object every editor tool draws on: the real prefab, not a bare
    /// object with a painter bolted to it.
    ///
    /// This exists because building one by hand quietly produced a different
    /// card. A <see cref="CardVisualPainter"/> added to a new GameObject has
    /// every serialized field at its default — no title font, no rules font, no
    /// sprite material — so a still rendered that way showed the fallback face
    /// while the game showed the real one. Nothing failed and nothing was
    /// logged; the pictures were simply of a card nobody would ever see.
    ///
    /// So there is one way to make a card for a preview or a capture, and it is
    /// an instance of the prefab the game uses. A tool that needs a card asks
    /// here.
    /// </summary>
    public static class CardPreviewCard
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/P_Card.prefab";

        /// <summary>The card prefab, or null if the project has none.</summary>
        public static GameObject Load()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab != null)
            {
                return prefab;
            }

            // Moved, perhaps. Worth finding rather than failing, because the
            // alternative a caller falls back to is the very thing this is here
            // to prevent.
            string[] guids = AssetDatabase.FindAssets("P_Card t:Prefab");

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);

                if (System.IO.Path.GetFileNameWithoutExtension(path) != "P_Card")
                {
                    continue;
                }

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        /// <summary>
        /// An instance of the card prefab under a parent, and its painter.
        ///
        /// Throws rather than falling back. There used to be a fallback here: a
        /// bare GameObject with a painter bolted on, accompanied by an error in
        /// the console. That is the worst of both worlds for an authoring tool -
        /// the console message scrolls away, the window carries on drawing, and
        /// what it draws is a card in TextMeshPro's default face with no
        /// materials, which looks enough like a card to tune against. Hours have
        /// already been lost in this project to exactly that picture.
        ///
        /// So the preview either shows the real card or shows nothing and says
        /// why. A tool that cannot find the prefab is broken, and a broken tool
        /// should stop rather than lie quietly.
        /// </summary>
        /// <exception cref="MissingCardPrefabException">
        /// When the prefab cannot be found or carries no painter.
        /// </exception>
        public static CardVisualPainter Make(Transform parent, out GameObject card)
        {
            GameObject prefab = Load();

            if (prefab == null)
            {
                card = null;

                throw new MissingCardPrefabException(
                    "No card prefab at " + PrefabPath + ", and none named P_Card anywhere in the " +
                    "project. Every preview and capture draws on the real prefab because the " +
                    "fonts and materials a card is drawn with are serialised on it; there is " +
                    "nothing to draw on and no honest picture to show.");
            }

            card = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (card == null)
            {
                throw new MissingCardPrefabException(
                    "The card prefab at " + AssetDatabase.GetAssetPath(prefab) +
                    " could not be instantiated.");
            }

            card.hideFlags = HideFlags.HideAndDontSave;
            card.transform.SetParent(parent, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;

            CardVisualPainter painter = card.GetComponent<CardVisualPainter>();

            if (painter == null)
            {
                string path = AssetDatabase.GetAssetPath(prefab);

                UnityEngine.Object.DestroyImmediate(card);
                card = null;

                throw new MissingCardPrefabException(
                    "The card prefab at " + path + " has no CardVisualPainter, so it cannot draw " +
                    "a composed card. Adding a blank one would draw in the wrong fonts.");
            }

            return painter;
        }
    }
}
