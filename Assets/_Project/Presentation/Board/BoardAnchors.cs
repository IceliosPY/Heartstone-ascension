using CoH.Core.Identifiers;
using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Where things belong in the world: one hand, one board row and one hero
    /// spot per side.
    ///
    /// Deliberately nothing but empty transforms. Re-theming the board later,
    /// crypt, workshop or anything else, means swapping the decorative meshes
    /// around these anchors while every position the layout code computes stays
    /// exactly where it was.
    /// </summary>
    public sealed class BoardAnchors : MonoBehaviour
    {
        [Header("Seat one, near side")]
        [SerializeField] private Transform playerOneHand;
        [SerializeField] private Transform playerOneBoard;
        [SerializeField] private Transform playerOneHero;

        [Header("Seat two, far side")]
        [SerializeField] private Transform playerTwoHand;
        [SerializeField] private Transform playerTwoBoard;
        [SerializeField] private Transform playerTwoHero;

        public Transform HandOf(PlayerId player) =>
            player == PlayerId.One ? playerOneHand : playerTwoHand;

        public Transform BoardOf(PlayerId player) =>
            player == PlayerId.One ? playerOneBoard : playerTwoBoard;

        public Transform HeroOf(PlayerId player) =>
            player == PlayerId.One ? playerOneHero : playerTwoHero;
    }

    /// <summary>
    /// The area a player drops a card onto to play it. Nothing but a collider
    /// with a seat written on it.
    /// </summary>
    public sealed class BoardDropZone : MonoBehaviour
    {
        [SerializeField] private bool isSeatOne = true;

        public PlayerId Owner => isSeatOne ? PlayerId.One : PlayerId.Two;

        public void SetOwner(PlayerId player) => isSeatOne = player == PlayerId.One;
    }
}
