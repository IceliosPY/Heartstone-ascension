namespace CoH.Presentation
{
    /// <summary>
    /// What the player is doing with the mouse right now.
    ///
    /// One field of this holds the whole interaction, instead of a handful of
    /// booleans that can contradict each other. Dragging a card while aiming an
    /// attack is not a state that needs guarding against here; it simply cannot
    /// be written down.
    ///
    /// Only the first four are things a player drives. <see cref="Resolving"/>
    /// and <see cref="GameEnded"/> are the two ways the match takes the mouse
    /// away, and both are entered by the controller rather than by a click.
    /// </summary>
    public enum InteractionState
    {
        /// <summary>Nothing held, nothing under the pointer.</summary>
        Idle = 0,

        /// <summary>A hand card is under the pointer and has risen to be read.</summary>
        HoveringHandCard = 1,

        /// <summary>A hand card has been picked up and follows the pointer.</summary>
        DraggingHandCard = 2,

        /// <summary>An attacker has been picked up and an arrow follows the pointer.</summary>
        TargetingAttack = 3,

        /// <summary>
        /// A card has been dropped on the board and is waiting for the player to
        /// say what it is aimed at.
        ///
        /// The same gesture as aiming an attack, and deliberately so: one arrow,
        /// one set of highlights, one way to pick a character. A second way to
        /// point at something would be a second thing to keep correct.
        /// </summary>
        TargetingPlay = 6,

        /// <summary>The queue is replaying events. No interaction may start.</summary>
        Resolving = 4,

        /// <summary>The match is over. Nothing is interactive again.</summary>
        GameEnded = 5
    }
}
