using System.Globalization;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>
    /// Turns a description of a card into the stack of pictures and labels that
    /// draws it.
    ///
    /// This is the only road from "a two mana neutral minion called Test
    /// Soldier" to a finished card, and everything that shows a card walks it:
    /// the hand, and the preview tool in the editor. There is no second
    /// renderer, so a preview is not a guess about what the game will draw. It
    /// is what the game will draw.
    ///
    /// The whole of it is three steps, and none of them knows what a card is:
    ///
    ///   the recipe says which layers a card can have, and when;
    ///   the catalog says which picture each layer gets;
    ///   this puts them in order.
    ///
    /// It creates nothing, touches no GameObject and reads no game state, which
    /// is why it can be tested without a scene and why adding a card is adding
    /// data. There is no card id anywhere below, and there is no place to put
    /// one: the composer is never told which card it is drawing, only what that
    /// card is like.
    /// </summary>
    public static class CardVisualComposer
    {
        /// <summary>
        /// Composes a card into a plan, reusing the plan given to it.
        ///
        /// A missing picture is not an error and not an exception. Most layers
        /// are optional — a spell has no health gem, a card with no tribe has no
        /// plaque, and a frame that already draws its own name banner leaves
        /// that slot empty on purpose. Layers the recipe marks required are the
        /// exception, and those are collected into the plan rather than thrown,
        /// so an incomplete card can still be drawn and still be reported.
        /// </summary>
        public static void Compose(
            in CardVisualDescriptor card,
            CardVisualRecipeAsset recipe,
            CardVisualCatalogAsset catalog,
            CardVisualPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            plan.Clear();

            if (recipe == null)
            {
                return;
            }

            for (int index = 0; index < recipe.Layers.Count; index++)
            {
                CardVisualLayerDefinition layer = recipe.Layers[index];

                if (layer == null || !layer.AppliesTo(card))
                {
                    continue;
                }

                if (layer.IsText)
                {
                    AddText(card, layer, plan);
                    continue;
                }

                AddSprite(card, layer, catalog, plan);
            }

            plan.SortByDepth();
        }

        private static void AddSprite(
            in CardVisualDescriptor card,
            CardVisualLayerDefinition layer,
            CardVisualCatalogAsset catalog,
            CardVisualPlan plan)
        {
            // Artwork belongs to the card rather than to a kind of card, which
            // is the one reason a slot is ever answered from somewhere other
            // than the catalog. It is also what lets one frame serve any number
            // of paintings without a second entry anywhere.
            Sprite sprite = layer.slot == CardVisualSlot.Artwork
                ? card.Artwork
                : catalog != null ? catalog.Resolve(layer.slot, card).Sprite : null;

            if (sprite == null)
            {
                if (layer.required)
                {
                    plan.ReportGap(new CardVisualGap(layer.slot, layer.name, card.ToString()));
                }

                return;
            }

            plan.Add(new CardVisualPlannedLayer
            {
                Slot = layer.slot,
                TextSlot = CardVisualTextSlot.None,
                Sprite = sprite,
                Text = null,
                SortingOrder = layer.sortingOrder,
                Rect = new Rect(layer.x, layer.y, layer.width, layer.height),
                Rotation = layer.rotation,
                FontSize = 0f,
                Bold = false,
                Tint = layer.tint
            });
        }

        private static void AddText(
            in CardVisualDescriptor card,
            CardVisualLayerDefinition layer,
            CardVisualPlan plan)
        {
            string value = Read(card, layer.text);

            // An empty label is not drawn at all rather than drawn empty, so a
            // card with no rules text has no rules text rather than a blank
            // rectangle sitting in the middle of it.
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            plan.Add(new CardVisualPlannedLayer
            {
                Slot = CardVisualSlot.None,
                TextSlot = layer.text,
                Sprite = null,
                Text = value,
                SortingOrder = layer.sortingOrder,
                Rect = new Rect(layer.x, layer.y, layer.width, layer.height),
                Rotation = layer.rotation,
                FontSize = layer.fontSize,
                Bold = layer.bold,
                Tint = layer.tint
            });
        }

        private static string Read(in CardVisualDescriptor card, CardVisualTextSlot slot)
        {
            switch (slot)
            {
                case CardVisualTextSlot.Name:
                    return card.Name;

                case CardVisualTextSlot.RulesText:
                    return card.RulesText;

                case CardVisualTextSlot.ManaCost:
                    return card.ShowsCost
                        ? card.ManaCost.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                case CardVisualTextSlot.Attack:
                    return card.ShowsStatistics
                        ? card.Attack.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                case CardVisualTextSlot.Health:
                    return card.ShowsStatistics
                        ? card.Health.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                case CardVisualTextSlot.Tribe:
                    return card.HasTribe ? card.Tribe.ToString().ToUpperInvariant() : string.Empty;

                default:
                    return string.Empty;
            }
        }
    }
}
