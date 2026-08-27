using System.Collections.Generic;
using UnityEngine;

namespace CoH.Presentation.CardVisuals
{
    /// <summary>One finished layer: a picture or a label, and where it goes.</summary>
    public struct CardVisualPlannedLayer
    {
        public CardVisualSlot Slot;
        public CardVisualTextSlot TextSlot;

        /// <summary>The picture, or null for a text layer.</summary>
        public Sprite Sprite;

        /// <summary>The words, or null for a picture layer.</summary>
        public string Text;

        public int SortingOrder;

        /// <summary>Left, top, width and height on the 800 by 1100 card canvas.</summary>
        public Rect Rect;

        public float Rotation;
        public CardVisualFill Fill;

        /// <summary>The shape this layer is clipped to, or null.</summary>
        public Sprite Mask;

        public float FontSize;
        public float FontSizeMin;
        public bool Bold;
        public bool Wrap;
        public CardVisualAlignment Alignment;
        public Color Tint;

        /// <summary>
        /// How this label is set: its face, its outline and the shape of its
        /// baseline. Meaningless on a picture layer.
        ///
        /// Resolved and copied rather than pointed at, so that a plan describes
        /// a card completely and keeps describing it while somebody edits the
        /// recipe it came from.
        /// </summary>
        public CardTextStyle TextStyle;

        /// <summary>
        /// Which layer of the recipe produced this, and why it applied.
        ///
        /// Carried for the reports and the preview only. Nothing about how a
        /// card draws reads it, and a plan composed without it would look
        /// exactly the same — but "the frame came from somewhere" is a much
        /// worse answer than "the frame came from the layer named Frame
        /// (spell), because the card is a spell".
        /// </summary>
        public string LayerName;

        public string Reason;

        public bool IsText => TextSlot != CardVisualTextSlot.None;
    }

    /// <summary>A layer that wanted a picture the catalog does not have.</summary>
    public readonly struct CardVisualGap
    {
        public CardVisualGap(CardVisualSlot slot, string layerName, string wanted)
        {
            Slot = slot;
            LayerName = layerName;
            Wanted = wanted;
        }

        public CardVisualSlot Slot { get; }

        public string LayerName { get; }

        /// <summary>The card that asked, described the way the catalog would match it.</summary>
        public string Wanted { get; }

        public string Describe() => Slot + " for " + Wanted + " (layer '" + LayerName + "')";
    }

    /// <summary>
    /// A composed card, before anything has been drawn.
    ///
    /// The point of stopping here is that this is testable without a scene, and
    /// that the editor preview and the running game share every decision that
    /// led to it. Whatever draws this — a stack of sprite renderers today,
    /// something else later — is downstream of the only interesting part.
    ///
    /// Reused rather than allocated, because a hand re-composing on every change
    /// would otherwise produce garbage at a steady rate for no reason.
    /// </summary>
    public sealed class CardVisualPlan
    {
        private readonly List<CardVisualPlannedLayer> _layers = new List<CardVisualPlannedLayer>();
        private readonly List<CardVisualGap> _gaps = new List<CardVisualGap>();

        public IReadOnlyList<CardVisualPlannedLayer> Layers => _layers;

        /// <summary>
        /// Every required picture that was not found.
        ///
        /// Empty on a card that composed completely. Not empty is not a crash:
        /// the card still draws, with the missing layers absent, and this is the
        /// list that tells somebody which files are still needed.
        /// </summary>
        public IReadOnlyList<CardVisualGap> Gaps => _gaps;

        public bool IsComplete => _gaps.Count == 0;

        public void Clear()
        {
            _layers.Clear();
            _gaps.Clear();
        }

        internal void Add(in CardVisualPlannedLayer layer) => _layers.Add(layer);

        internal void ReportGap(in CardVisualGap gap) => _gaps.Add(gap);

        /// <summary>
        /// Sorts the layers back to front.
        ///
        /// A stable sort, so two layers sharing an order keep the order they
        /// were authored in rather than swapping about between runs. The recipe
        /// validator already reports that as a mistake; this makes it at least a
        /// repeatable one.
        /// </summary>
        internal void SortByDepth()
        {
            for (int index = 1; index < _layers.Count; index++)
            {
                CardVisualPlannedLayer layer = _layers[index];
                int position = index - 1;

                while (position >= 0 && _layers[position].SortingOrder > layer.SortingOrder)
                {
                    _layers[position + 1] = _layers[position];
                    position--;
                }

                _layers[position + 1] = layer;
            }
        }

        /// <summary>Whether anything was drawn in that slot.</summary>
        public bool Draws(CardVisualSlot slot) => SpriteIn(slot) != null;

        /// <summary>The picture drawn in that slot, or null.</summary>
        public Sprite SpriteIn(CardVisualSlot slot)
        {
            for (int index = 0; index < _layers.Count; index++)
            {
                if (!_layers[index].IsText && _layers[index].Slot == slot)
                {
                    return _layers[index].Sprite;
                }
            }

            return null;
        }

        /// <summary>What was printed in that text slot, or null if nothing was.</summary>
        public string TextIn(CardVisualTextSlot slot)
        {
            for (int index = 0; index < _layers.Count; index++)
            {
                if (_layers[index].TextSlot == slot)
                {
                    return _layers[index].Text;
                }
            }

            return null;
        }

        /// <summary>
        /// Every layer that was drawn, and why. One line each:
        ///
        ///     090  RarityGem   Card_Inhand_Minion_Gem_Rare   347,663 122x92   CardType Equals 1 and Rarity NotEquals 0
        ///
        /// This is what turns "the wrong gem is showing" into "the wrong gem is
        /// showing because this layer applied and resolved to that file".
        /// </summary>
        public string DescribeResolution()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();

            for (int index = 0; index < _layers.Count; index++)
            {
                CardVisualPlannedLayer layer = _layers[index];

                text.Append(layer.SortingOrder.ToString("D3"));
                text.Append("  ");
                text.Append((layer.IsText ? layer.TextSlot.ToString() : layer.Slot.ToString()).PadRight(16));

                text.Append((layer.IsText
                    ? "\"" + layer.Text + "\""
                    : layer.Sprite != null ? layer.Sprite.name : "(none)").PadRight(44));

                text.Append(layer.Rect.x.ToString("0") + "," + layer.Rect.y.ToString("0") + " " +
                            layer.Rect.width.ToString("0") + "x" + layer.Rect.height.ToString("0"));

                if (!string.IsNullOrEmpty(layer.Reason))
                {
                    text.Append("   [" + layer.Reason + "]");
                }

                text.Append('\n');
            }

            for (int index = 0; index < _gaps.Count; index++)
            {
                text.Append("MISSING  ");
                text.Append(_gaps[index].Describe());
                text.Append('\n');
            }

            return text.ToString();
        }

        public string Describe()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();

            for (int index = 0; index < _layers.Count; index++)
            {
                CardVisualPlannedLayer layer = _layers[index];

                text.Append(layer.SortingOrder.ToString("D3"));
                text.Append("  ");
                text.Append(layer.IsText ? layer.TextSlot + " \"" + layer.Text + "\"" : layer.Slot.ToString());
                text.Append('\n');
            }

            for (int index = 0; index < _gaps.Count; index++)
            {
                text.Append("MISSING  ");
                text.Append(_gaps[index].Describe());
                text.Append('\n');
            }

            return text.ToString();
        }
    }
}
