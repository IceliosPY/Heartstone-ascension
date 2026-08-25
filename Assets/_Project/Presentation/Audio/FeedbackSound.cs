namespace CoH.Presentation
{
    /// <summary>
    /// The things a match can sound like.
    ///
    /// Named after what happened, never after what it sounds like. An animation
    /// asks for <c>Impact</c>, not for a particular file, so replacing the
    /// placeholder tone with a recorded hit later touches nothing but the
    /// assignment.
    /// </summary>
    public enum FeedbackSound
    {
        None = 0,
        CardDraw = 1,
        CardBurn = 2,
        CardPlay = 3,
        Summon = 4,
        Attack = 5,
        Impact = 6,
        Death = 7,
        TurnStart = 8,
        GameEnd = 9
    }
}
