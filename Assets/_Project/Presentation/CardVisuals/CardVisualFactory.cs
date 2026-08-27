using System.Collections.Generic;
using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Everything needed to build a card's appearance, in one place.
    ///
    /// A factory in the sense that it produces a picture, and in no other sense:
    /// it creates no entity, knows no rule, and could be deleted without the
    /// engine noticing. A card exists because the Core says so; this decides
    /// what it looks like.
    ///
    /// Three pieces, each answering one question:
    ///
    ///   the recipe   — which layers a card has, and where they sit;
    ///   the catalog  — which picture each layer gets;
    ///   the library  — which painting belongs to this particular card.
    ///
    /// Anything that shows a card holds one of these and asks it. There is no
    /// second path, which is why the editor preview and the game cannot drift
    /// apart.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardVisualFactory",
        menuName = "Conquest of Hearthstone/Card Visual Factory",
        order = 33)]
    public sealed class CardVisualFactory : ScriptableObject
    {
        [Tooltip("One per card style. The first is used by any card whose style has no recipe.")]
        [SerializeField] private List<CardVisualRecipeAsset> recipes = new List<CardVisualRecipeAsset>();

        [SerializeField] private CardVisualCatalogAsset catalog;

        [SerializeField] private CardVisualLibraryAsset library;

        public CardVisualCatalogAsset Catalog => catalog;

        public CardVisualLibraryAsset Library => library;

        public IReadOnlyList<CardVisualRecipeAsset> Recipes => recipes;

        /// <summary>
        /// The recipe for a style.
        ///
        /// Falls back to the first recipe in the list rather than to nothing,
        /// because a card in an unknown style is better drawn in the standard
        /// one than not drawn at all. The fallback is a stated default, not an
        /// arbitrary pick: it is whichever recipe is listed first.
        /// </summary>
        public CardVisualRecipeAsset RecipeFor(CardVisualStyle style)
        {
            for (int index = 0; index < recipes.Count; index++)
            {
                if (recipes[index] != null && recipes[index].Style.Equals(style))
                {
                    return recipes[index];
                }
            }

            for (int index = 0; index < recipes.Count; index++)
            {
                if (recipes[index] != null)
                {
                    return recipes[index];
                }
            }

            return null;
        }

        /// <summary>Describes a card in a match, artwork and style included.</summary>
        public CardVisualDescriptor Describe(in CardViewModel model)
        {
            CardId id = model.CardId;

            return CardVisualDescriptor.FromViewModel(
                model,
                library != null ? library.ArtworkFor(id) : null,
                library != null ? library.StyleFor(id) : CardVisualStyle.Default,
                library != null ? library.ExpansionFor(id) : string.Empty,

                // The one place a card's identity is used for its appearance,
                // and it is used to fetch data rather than to decide anything.
                // What travels on is a set of optional numbers.
                library != null ? library.OverridesFor(id) : null);
        }

        /// <summary>Composes a described card into a plan, reusing the plan.</summary>
        public void Compose(in CardVisualDescriptor card, CardVisualPlan plan) =>
            CardVisualComposer.Compose(card, RecipeFor(card.Style), catalog, plan);

        public void Validate(List<string> problems)
        {
            if (problems == null)
            {
                return;
            }

            if (recipes.Count == 0)
            {
                problems.Add(name + " has no recipe, so it cannot compose anything.");
            }

            HashSet<string> styles = new HashSet<string>();

            for (int index = 0; index < recipes.Count; index++)
            {
                if (recipes[index] == null)
                {
                    problems.Add(name + ": recipe slot " + index + " is empty.");
                    continue;
                }

                if (!styles.Add(recipes[index].Style.Value))
                {
                    problems.Add(
                        name + ": two recipes claim the style '" + recipes[index].Style +
                        "', and only the first will ever be used.");
                }

                recipes[index].Validate(problems);
            }

            if (catalog == null)
            {
                problems.Add(name + " has no catalog, so every picture will be missing.");
            }
            else
            {
                catalog.Validate(problems);
            }

            if (library == null)
            {
                problems.Add(name + " has no artwork library, so no card will have a painting.");
            }
        }

#if UNITY_EDITOR
        internal void Wire(
            IReadOnlyList<CardVisualRecipeAsset> theRecipes,
            CardVisualCatalogAsset theCatalog,
            CardVisualLibraryAsset theLibrary)
        {
            recipes.Clear();

            if (theRecipes != null)
            {
                for (int index = 0; index < theRecipes.Count; index++)
                {
                    recipes.Add(theRecipes[index]);
                }
            }

            catalog = theCatalog;
            library = theLibrary;
        }
#endif
    }
}
