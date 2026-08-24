namespace CoH.Core.Events
{
    /// <summary>
    /// Something the engine did, described after the fact.
    ///
    /// Events are returned as an ordered list rather than pushed through an
    /// observer bus. A list is simpler, its order is guaranteed, it serialises
    /// as-is for a future server, and replaying it is exactly what the
    /// presentation layer needs in order to animate a resolution that the
    /// engine has already finished.
    ///
    /// Hidden information: events that name a card carry a CardId, and CardId
    /// already has a None value. A future server-side view layer redacts an
    /// opponent's private cards by handing out the same event with CardId.None,
    /// so no extra machinery is needed here today.
    /// </summary>
    public abstract class GameEvent
    {
    }
}
