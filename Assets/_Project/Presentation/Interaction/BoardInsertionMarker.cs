using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The empty slot shown on the board while a minion is being dragged over
    /// it.
    ///
    /// It stands exactly where the minion would land, and the minions already in
    /// the row step aside to make the space, so what the player sees during the
    /// drag is what the board looks like a moment after the drop. Anything less
    /// literal, an arrow or a line between two minions, leaves the player to
    /// guess which side of the gap they are on.
    ///
    /// Shown only when the engine has already said the card could be played
    /// there. It carries no opinion of its own about that.
    /// </summary>
    public sealed class BoardInsertionMarker : MonoBehaviour
    {
        [SerializeField] private GameObject visual;

        /// <summary>True while a slot is being held open.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Which slot the marker is standing in, or -1 when hidden.</summary>
        public int Slot { get; private set; } = -1;

        private void Awake() => Hide();

        /// <summary>Opens a slot at a position in the board row's own space.</summary>
        public void Show(Transform row, Vector3 localPosition, int slot)
        {
            IsVisible = true;
            Slot = slot;

            if (row != null && transform.parent != row)
            {
                transform.SetParent(row, false);
            }

            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (visual != null)
            {
                visual.SetActive(true);
            }
        }

        public void Hide()
        {
            IsVisible = false;
            Slot = -1;

            if (visual != null)
            {
                visual.SetActive(false);
            }
        }
    }
}
