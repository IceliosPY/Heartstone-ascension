using System;
using System.Linq;
using System.Reflection;
using CoH.Core;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Tests d'architecture (Phase 0).
    ///
    /// Ces tests ne verifient aucune regle de jeu : ils verrouillent la
    /// contrainte structurelle du projet, a savoir que CoH.Core reste une
    /// bibliotheque C# pure, compilable hors Unity et testable sans scene.
    ///
    /// Ils servent aussi de garde-fou : si quelqu'un desactive un jour
    /// noEngineReferences dans CoH.Core.asmdef, ce test echouera.
    /// </summary>
    public sealed class CoreAssemblyConstraintsTests
    {
        private static Assembly Core => typeof(CoreAssembly).Assembly;

        [Test]
        public void Core_assembly_is_loaded_and_correctly_named()
        {
            Assert.That(Core.GetName().Name, Is.EqualTo(CoreAssembly.Name));
        }

        [Test]
        public void Core_assembly_does_not_reference_Unity()
        {
            string[] unityReferences = Core
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .Where(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)
                            || name.StartsWith("UnityEditor", StringComparison.Ordinal)
                            || name.StartsWith("Unity.", StringComparison.Ordinal))
                .ToArray();

            Assert.That(
                unityReferences,
                Is.Empty,
                "CoH.Core doit rester du C# pur (moteur de regles decouple de Unity). "
                + "References Unity detectees : " + string.Join(", ", unityReferences));
        }
    }
}
