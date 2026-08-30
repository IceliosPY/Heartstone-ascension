using System;
using System.Collections.Generic;
using CoH.Data;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// The one control that picks a real card, or leaves the preview made up.
    ///
    /// A searchable dropdown rather than a growing panel, because a panel that
    /// lists every card is fine at a dozen and useless at a thousand. Unity
    /// already has a searchable dropdown built for exactly this - the same
    /// control the Add Component button opens - so nothing here reimplements
    /// search UI; it only supplies the list and reads back what was chosen.
    /// </summary>
    public sealed class CardPickerDropdown : AdvancedDropdown
    {
        private const int SyntheticId = -1;
        private const int RootId = -2;
        private const int EmptyId = -3;

        private readonly IReadOnlyList<CardDefinitionAsset> _cards;
        private readonly Action<CardDefinitionAsset> _picked;

        public CardPickerDropdown(
            AdvancedDropdownState state,
            IReadOnlyList<CardDefinitionAsset> cards,
            Action<CardDefinitionAsset> picked)
            : base(state)
        {
            _cards = cards;
            _picked = picked;

            minimumSize = new Vector2(280f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new AdvancedDropdownItem("Card") { id = RootId };

            root.AddChild(new AdvancedDropdownItem("Made up card (synthetic preview)") { id = SyntheticId });

            if (_cards.Count == 0)
            {
                root.AddChild(new AdvancedDropdownItem("No CardDefinitionAsset found in the project")
                {
                    id = EmptyId,
                    enabled = false
                });
            }

            for (int index = 0; index < _cards.Count; index++)
            {
                CardDefinitionAsset card = _cards[index];

                if (card != null)
                {
                    root.AddChild(new AdvancedDropdownItem(card.DisplayName) { id = index });
                }
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            _picked(item.id >= 0 && item.id < _cards.Count ? _cards[item.id] : null);
        }
    }
}
