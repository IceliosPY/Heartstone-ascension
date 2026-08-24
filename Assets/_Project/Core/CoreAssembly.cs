namespace CoH.Core
{
    /// <summary>
    /// Anchor type for the rules engine assembly.
    ///
    /// This type deliberately contains no game logic. It exists only to give
    /// the architecture tests a stable handle on the CoH.Core assembly,
    /// without depending on a gameplay type that does not exist yet (Phase 0).
    ///
    /// Core project constraint: this assembly is declared with
    /// noEngineReferences = true. No UnityEngine or UnityEditor type can be
    /// referenced from it, which mechanically enforces the separation between
    /// the rules engine and the Unity presentation layer.
    /// </summary>
    public static class CoreAssembly
    {
        /// <summary>Assembly name, as declared in CoH.Core.asmdef.</summary>
        public const string Name = "CoH.Core";
    }
}
