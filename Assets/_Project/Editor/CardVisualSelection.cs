using CoH.Core.Cards;
using CoH.Data;
using CoH.Presentation.CardVisuals;

namespace CoH.Editor
{
    /// <summary>
    /// Turns a chosen real card into what the rest of the editor needs: the
    /// descriptor to compose, and the sparse adjustments to edit.
    ///
    /// Pulled out of the window for the same reason <see cref="HandPresentation"/>
    /// is: so "the card the picker chose is the card being edited" can be
    /// tested without opening a window, rather than trusted by inspection.
    /// </summary>
    public static class CardVisualSelection
    {
        /// <summary>What the composer should draw for this real card.</summary>
        public static CardVisualDescriptor Describe(CardDefinitionAsset card, CardVisualLibraryAsset library)
        {
            CardDefinition definition = card.ToDefinition();

            return new CardVisualDescriptor(
                definition.Type,
                definition.Class,
                definition.Rarity,
                definition.Tribe,
                library != null ? library.ArtworkFor(definition.Id) : null,
                definition.Name,
                definition.Text,
                definition.ManaCost,
                definition.Attack,
                definition.Health,
                showsCost: true,
                showsStatistics: definition.Type == CardType.Minion || definition.Type == CardType.Weapon,
                style: library != null ? library.StyleFor(definition.Id) : default,
                secondaryClass: CardClass.Neutral,
                expansion: string.Empty,
                faceDown: false,
                overrides: library != null ? library.OverridesFor(definition.Id) : null);
        }

        /// <summary>
        /// This card's own sparse adjustments, created the first time something
        /// asks. The row this card and no other writes to when "This card"
        /// scope is active.
        /// </summary>
        public static CardVisualOverrides Adjustments(CardDefinitionAsset card, CardVisualLibraryAsset library) =>
            library.EstablishOverrides(card.RawId);
    }
}
