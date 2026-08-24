namespace CoH.Core
{
    /// <summary>
    /// Point d'ancrage de l'assembly du moteur de regles.
    ///
    /// Ce type ne contient volontairement aucune logique de jeu. Il existe
    /// uniquement pour donner aux tests d'architecture une reference stable
    /// sur l'assembly CoH.Core, sans dependre d'un type de gameplay qui
    /// n'existe pas encore (Phase 0).
    ///
    /// Contrainte fondamentale du projet : cette assembly est declaree avec
    /// noEngineReferences = true. Aucun type UnityEngine ou UnityEditor ne
    /// peut y etre reference, ce qui garantit mecaniquement la separation
    /// entre le moteur de regles et la couche de presentation Unity.
    /// </summary>
    public static class CoreAssembly
    {
        /// <summary>Nom de l'assembly, tel que declare dans CoH.Core.asmdef.</summary>
        public const string Name = "CoH.Core";
    }
}
