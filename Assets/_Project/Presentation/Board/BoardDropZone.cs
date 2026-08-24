using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// The area a player drops a card onto to play it.
    ///
    /// Marked by side rather than by seat, for the same reason as
    /// <see cref="BoardAnchors"/>: the acting player always plays onto the near
    /// half of the screen, whichever seat they hold.
    ///
    /// This lives in its own file, and must keep living in its own file. Unity
    /// only creates an addressable script asset for the type whose name matches
    /// the file name, so a component class declared as a second type somewhere
    /// else has no asset for a scene to point at. Adding it from an editor
    /// script then appears to work, because the type is resolved in memory, but
    /// saving writes a reference that resolves to nothing and the component
    /// comes back missing the next time the scene is loaded. That is exactly
    /// what happened here: this class used to sit at the bottom of
    /// BoardAnchors.cs, and both drop zones were dead in every build.
    /// </summary>
    public sealed class BoardDropZone : MonoBehaviour
    {
        [SerializeField] private bool isNearSide = true;

        public bool IsNearSide => isNearSide;

        public void SetNearSide(bool near) => isNearSide = near;
    }
}
