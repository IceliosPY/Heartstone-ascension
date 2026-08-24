using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Where things belong on screen: a near side and a far side.
    ///
    /// Deliberately not "player one" and "player two". In hotseat the person
    /// holding the mouse is whoever has the turn, so the comfortable half of
    /// the screen, the big readable hand at the bottom, has to follow the turn
    /// rather than belong to a seat. Naming these near and far is what stops
    /// anything quietly treating seat one as the permanent human.
    ///
    /// Nothing here but empty transforms. Re-theming the board later means
    /// swapping decorative meshes around these anchors while every computed
    /// position stays exactly where it was.
    /// </summary>
    public sealed class BoardAnchors : MonoBehaviour
    {
        [Header("Near side, the player currently acting")]
        [SerializeField] private Transform nearHand;
        [SerializeField] private Transform nearBoard;
        [SerializeField] private Transform nearHero;

        [Header("Far side, their opponent")]
        [SerializeField] private Transform farHand;
        [SerializeField] private Transform farBoard;
        [SerializeField] private Transform farHero;

        public Transform Hand(bool near) => near ? nearHand : farHand;

        public Transform Board(bool near) => near ? nearBoard : farBoard;

        public Transform Hero(bool near) => near ? nearHero : farHero;
    }

    /// <summary>
    /// The area a player drops a card onto to play it.
    ///
    /// Marked by side rather than by seat, for the same reason as the anchors:
    /// the acting player always plays onto the near half.
    /// </summary>
    public sealed class BoardDropZone : MonoBehaviour
    {
        [SerializeField] private bool isNearSide = true;

        public bool IsNearSide => isNearSide;

        public void SetNearSide(bool near) => isNearSide = near;
    }
}
