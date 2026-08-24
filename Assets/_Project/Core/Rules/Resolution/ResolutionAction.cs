namespace CoH.Core.Rules.Resolution
{
    /// <summary>
    /// A unit of work the engine still has to carry out.
    ///
    /// Deliberately not a <see cref="Events.GameEvent"/>. An action is an
    /// intention the engine holds internally; an event is an observable result
    /// it hands out afterwards. One action may produce several events, none at
    /// all, or further actions. Conflating the two would push internal
    /// bookkeeping into the presentation layer and make the event stream
    /// impossible to keep meaningful.
    ///
    /// Actions are internal on purpose: nothing outside the rules engine may
    /// build one, so a caller can never bypass validation by handing the engine
    /// a ready-made piece of work.
    /// </summary>
    internal abstract class ResolutionAction
    {
        /// <summary>
        /// Carries out this action: mutates state, emits events, and queues any
        /// follow-up work through the context.
        /// </summary>
        public abstract void Resolve(ResolutionContext context);
    }
}
