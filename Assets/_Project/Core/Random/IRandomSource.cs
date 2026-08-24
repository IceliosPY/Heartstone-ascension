namespace CoH.Core.Random
{
    /// <summary>
    /// The single source of randomness allowed inside the engine.
    ///
    /// Every shuffle, random target, discover and random card generation must
    /// go through this interface. Nothing in the engine may call
    /// System.Random (its algorithm is not guaranteed identical across .NET
    /// runtimes) nor UnityEngine.Random (which CoH.Core cannot even see).
    ///
    /// Keeping a single narrow entry point is what makes a match reproducible
    /// from its seed, which in turn gives us reproducible tests, bug replay
    /// and, later, server/client synchronisation.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// Returns a uniformly distributed value in [0, exclusiveMax).
        /// </summary>
        int NextInt(int exclusiveMax);
    }
}
